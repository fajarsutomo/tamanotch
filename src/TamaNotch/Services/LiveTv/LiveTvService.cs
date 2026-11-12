using TamaNotch.Helpers;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using TamaNotch.Models;
using TamaNotch.Services.Settings;

namespace TamaNotch.Services.LiveTv;

public class LiveTvService : ILiveTvService
{
    private const string CatalogRawBaseUrl = "https://raw.githubusercontent.com/famelack/famelack-data/main/tv/raw";

    private readonly ISettingsService _settingsService;
    private readonly HttpClient _httpClient;
    private readonly List<LiveTvChannel> _channels = new();
    private readonly List<LiveTvCountryOption> _countries = new();
    private readonly Dictionary<string, List<LiveTvChannel>> _countryCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _cacheDir;

    private LiveTvChannel? _currentChannel;
    private LiveTvPlaybackState _playbackState = LiveTvPlaybackState.Stopped;
    private string _statusMessage = "Siap memutar siaran Live TV";
    private bool _isLoadingCatalog;
    private bool _isDisposed;

    public IReadOnlyList<LiveTvChannel> Channels => _channels;
    public IReadOnlyList<LiveTvCountryOption> Countries => _countries;
    public LiveTvChannel? CurrentChannel => _currentChannel;
    public LiveTvPlaybackState PlaybackState => _playbackState;
    public string StatusMessage => _statusMessage;
    public bool IsLoadingCatalog => _isLoadingCatalog;

    public double Volume
    {
        get => _settingsService.CurrentSettings.LiveTvVolume;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_settingsService.CurrentSettings.LiveTvVolume - clamped) > 0.001)
            {
                _settingsService.CurrentSettings.LiveTvVolume = clamped;
                _settingsService.Save();
                RunOnUiThread(() =>
                {
                    VolumeChanged?.Invoke(clamped, IsMuted);
                    StateChanged?.Invoke();
                });
            }
        }
    }

    public bool IsMuted
    {
        get => _settingsService.CurrentSettings.LiveTvMuted;
        set
        {
            if (_settingsService.CurrentSettings.LiveTvMuted != value)
            {
                _settingsService.CurrentSettings.LiveTvMuted = value;
                _settingsService.Save();
                RunOnUiThread(() =>
                {
                    VolumeChanged?.Invoke(Volume, value);
                    StateChanged?.Invoke();
                });
            }
        }
    }

    public event Action? StateChanged;
    public event Action<LiveTvChannel?>? ChannelChanged;
    public event Action<LiveTvChannel, string>? PlayStreamRequested;
    public event Action? TogglePlayPauseRequested;
    public event Action? StopStreamRequested;
    public event Action<double, bool>? VolumeChanged;
    public event Action? CatalogUpdated;

    public LiveTvService(ISettingsService settingsService, HttpClient? httpClient = null)
    {
        _settingsService = settingsService;
        _httpClient = httpClient ?? CreateHttpClient();
        _cacheDir = Path.Combine(_settingsService.GetAppDataPath(), "LiveTvCache");
        try { Directory.CreateDirectory(_cacheDir); } catch { }

        InitializeCountries();
        InitializeChannels();
        RestoreLastChannel();
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppVersion.UserAgentToken} (+https://github.com/fajarsutomo; LiveTV)");
        return client;
    }

    private void InitializeCountries()
    {
        _countries.Clear();
        _countries.AddRange(CatalogLoader.Load<LiveTvCountryOption>("livetv-countries.json"));
    }

    private void InitializeChannels()
    {
        _channels.Clear();

        var builtIn = GetBuiltInChannels();
        _builtInIds = new HashSet<string>(builtIn.Select(b => b.Id), StringComparer.OrdinalIgnoreCase);
        var favIds = _settingsService.CurrentSettings.FavoriteLiveTvChannelIds ?? new List<string>();
        var favSet = new HashSet<string>(favIds, StringComparer.OrdinalIgnoreCase);

        foreach (var ch in builtIn)
        {
            ch.IsFavorite = favSet.Contains(ch.Id);
            _channels.Add(ch);
        }

        if (_settingsService.CurrentSettings.CustomLiveTvChannels != null)
        {
            foreach (var custom in _settingsService.CurrentSettings.CustomLiveTvChannels)
            {
                custom.IsCustom = true;
                custom.IsFavorite = favSet.Contains(custom.Id) || custom.IsFavorite;
                _channels.Add(custom);
            }
        }

        // Saluran dari katalog/pencarian online yang difavoritkan dimuat kembali dari data lengkapnya.
        if (_settingsService.CurrentSettings.SavedOnlineLiveTvChannels != null)
        {
            foreach (var saved in _settingsService.CurrentSettings.SavedOnlineLiveTvChannels)
            {
                if (_channels.Any(c => string.Equals(c.Id, saved.Id, StringComparison.OrdinalIgnoreCase))) continue;
                saved.IsCustom = false;
                saved.IsFavorite = true;
                _channels.Add(saved);
            }
        }
    }

    private HashSet<string> _builtInIds = new(StringComparer.OrdinalIgnoreCase);

    private void RestoreLastChannel()
    {
        var lastId = _settingsService.CurrentSettings.LastLiveTvChannelId;
        if (!string.IsNullOrWhiteSpace(lastId))
        {
            _currentChannel = _channels.FirstOrDefault(c => string.Equals(c.Id, lastId, StringComparison.OrdinalIgnoreCase));
        }

        _currentChannel ??= _channels.FirstOrDefault();
        if (_currentChannel != null)
        {
            _currentChannel.IsCurrent = true;
        }
    }

    private static List<LiveTvChannel> GetBuiltInChannels() =>
        CatalogLoader.Load<LiveTvChannel>("livetv-channels.json");

    public void PlayChannel(LiveTvChannel channel, int sourceIndex = 0)
    {
        if (channel == null) return;

        if (!_channels.Any(c => string.Equals(c.Id, channel.Id, StringComparison.OrdinalIgnoreCase)))
        {
            _channels.Add(channel);
        }

        foreach (var c in _channels)
        {
            c.IsCurrent = string.Equals(c.Id, channel.Id, StringComparison.OrdinalIgnoreCase);
            c.IsPlaying = false;
        }

        _currentChannel = channel;
        _currentChannel.IsCurrent = true;
        int clampedIndex = Math.Clamp(sourceIndex, 0, Math.Max(0, channel.SourceCount - 1));
        _currentChannel.ActiveSourceIndex = clampedIndex;

        _settingsService.CurrentSettings.LastLiveTvChannelId = channel.Id;
        _settingsService.Save();

        var url = _currentChannel.ActiveStreamUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            _playbackState = LiveTvPlaybackState.Error;
            _statusMessage = $"Saluran {channel.Name} tidak memiliki sumber stream";
            ChannelChanged?.Invoke(_currentChannel);
            StateChanged?.Invoke();
            return;
        }

        _playbackState = LiveTvPlaybackState.Buffering;
        _statusMessage = $"Menghubungkan ke {channel.Name} ({_currentChannel.SourceBadgeText})...";
        ChannelChanged?.Invoke(_currentChannel);
        StateChanged?.Invoke();

        PlayStreamRequested?.Invoke(_currentChannel, url);
    }

    public void CycleNextSource()
    {
        if (_currentChannel == null) return;
        int total = _currentChannel.SourceCount;
        if (total <= 1)
        {
            PlayChannel(_currentChannel, 0);
            return;
        }

        int nextIdx = (_currentChannel.ActiveSourceIndex + 1) % total;
        PlayChannel(_currentChannel, nextIdx);
    }

    public bool TryNextFallbackSource()
    {
        if (_currentChannel == null) return false;
        int nextIdx = _currentChannel.ActiveSourceIndex + 1;
        if (nextIdx < _currentChannel.SourceCount)
        {
            _currentChannel.ActiveSourceIndex = nextIdx;
            var nextUrl = _currentChannel.ActiveStreamUrl;
            _playbackState = LiveTvPlaybackState.Buffering;
            _statusMessage = $"Mencoba server cadangan {_currentChannel.Name} ({_currentChannel.SourceBadgeText})...";
            StateChanged?.Invoke();
            PlayStreamRequested?.Invoke(_currentChannel, nextUrl);
            return true;
        }

        _currentChannel.IsPlaying = false;
        _playbackState = LiveTvPlaybackState.Error;
        _statusMessage = $"Siaran {_currentChannel.Name} sedang sibuk/offline";
        StateChanged?.Invoke();
        return false;
    }

    public void TogglePlayPause()
    {
        if (_currentChannel == null)
        {
            var first = _channels.FirstOrDefault();
            if (first != null)
            {
                PlayChannel(first);
            }
            return;
        }

        if (_playbackState == LiveTvPlaybackState.Stopped || _playbackState == LiveTvPlaybackState.Error)
        {
            PlayChannel(_currentChannel, _currentChannel.ActiveSourceIndex);
            return;
        }

        TogglePlayPauseRequested?.Invoke();
    }

    public void Stop()
    {
        if (_currentChannel != null)
        {
            _currentChannel.IsPlaying = false;
        }

        _playbackState = LiveTvPlaybackState.Stopped;
        _statusMessage = _currentChannel != null
            ? $"Siaran {_currentChannel.Name} dihentikan"
            : "Siap memutar siaran Live TV";
        StopStreamRequested?.Invoke();
        StateChanged?.Invoke();
    }

    public void NextChannel(IEnumerable<LiveTvChannel>? currentPool = null)
    {
        var list = (currentPool ?? _channels).ToList();
        if (list.Count == 0) return;

        int idx = _currentChannel != null
            ? list.FindIndex(c => string.Equals(c.Id, _currentChannel.Id, StringComparison.OrdinalIgnoreCase))
            : -1;

        int nextIdx = (idx + 1) % list.Count;
        PlayChannel(list[nextIdx]);
    }

    public void PreviousChannel(IEnumerable<LiveTvChannel>? currentPool = null)
    {
        var list = (currentPool ?? _channels).ToList();
        if (list.Count == 0) return;

        int idx = _currentChannel != null
            ? list.FindIndex(c => string.Equals(c.Id, _currentChannel.Id, StringComparison.OrdinalIgnoreCase))
            : 0;

        int prevIdx = (idx - 1 + list.Count) % list.Count;
        PlayChannel(list[prevIdx]);
    }

    public void ToggleFavorite(LiveTvChannel channel)
    {
        if (channel == null) return;
        channel.IsFavorite = !channel.IsFavorite;

        var favs = _settingsService.CurrentSettings.FavoriteLiveTvChannelIds ??= new List<string>();
        if (channel.IsFavorite)
        {
            if (!favs.Contains(channel.Id, StringComparer.OrdinalIgnoreCase))
            {
                favs.Add(channel.Id);
            }
        }
        else
        {
            favs.RemoveAll(id => string.Equals(id, channel.Id, StringComparison.OrdinalIgnoreCase));
        }

        PersistOnlineFavorite(channel);
        _settingsService.Save();
        StateChanged?.Invoke();
    }

    /// <summary>Saluran online (bukan bawaan dan bukan kustom) disimpan lengkap saat difavoritkan dan dibuang saat tidak.</summary>
    private void PersistOnlineFavorite(LiveTvChannel channel)
    {
        if (channel.IsCustom || _builtInIds.Contains(channel.Id)) return;

        var saved = _settingsService.CurrentSettings.SavedOnlineLiveTvChannels ??= new List<LiveTvChannel>();
        saved.RemoveAll(c => string.Equals(c.Id, channel.Id, StringComparison.OrdinalIgnoreCase));
        if (channel.IsFavorite) saved.Add(channel);
    }

    public LiveTvChannel AddCustomChannel(string name, string streamUrl, string category = "Custom", string countryCode = "id", string countryName = "Custom Stream")
    {
        var custom = new LiveTvChannel
        {
            Id = "custom-tv-" + Guid.NewGuid().ToString("N")[..8],
            Name = name.Trim(),
            StreamUrl = NormalizeYouTubeOrStreamUrl(streamUrl.Trim()),
            Category = string.IsNullOrWhiteSpace(category) ? "Custom" : category.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode) ? "id" : countryCode.Trim().ToLowerInvariant(),
            CountryName = string.IsNullOrWhiteSpace(countryName) ? "Custom Stream" : countryName.Trim(),
            IsCustom = true,
            IsFavorite = true
        };

        _channels.Insert(0, custom);
        _settingsService.CurrentSettings.CustomLiveTvChannels ??= new List<LiveTvChannel>();
        _settingsService.CurrentSettings.CustomLiveTvChannels.Add(custom);

        _settingsService.CurrentSettings.FavoriteLiveTvChannelIds ??= new List<string>();
        if (!_settingsService.CurrentSettings.FavoriteLiveTvChannelIds.Contains(custom.Id, StringComparer.OrdinalIgnoreCase))
        {
            _settingsService.CurrentSettings.FavoriteLiveTvChannelIds.Add(custom.Id);
        }

        _settingsService.Save();
        CatalogUpdated?.Invoke();
        StateChanged?.Invoke();
        return custom;
    }

    public void RemoveCustomChannel(LiveTvChannel channel)
    {
        if (channel == null || !channel.IsCustom) return;

        bool wasCurrent = _currentChannel != null && string.Equals(_currentChannel.Id, channel.Id, StringComparison.OrdinalIgnoreCase);
        if (wasCurrent)
        {
            Stop();
        }

        _channels.RemoveAll(c => string.Equals(c.Id, channel.Id, StringComparison.OrdinalIgnoreCase));
        _settingsService.CurrentSettings.CustomLiveTvChannels?.RemoveAll(c => string.Equals(c.Id, channel.Id, StringComparison.OrdinalIgnoreCase));
        _settingsService.CurrentSettings.FavoriteLiveTvChannelIds?.RemoveAll(id => string.Equals(id, channel.Id, StringComparison.OrdinalIgnoreCase));

        if (wasCurrent)
        {
            _currentChannel = _channels.FirstOrDefault();
            if (_currentChannel != null) _currentChannel.IsCurrent = true;
            ChannelChanged?.Invoke(_currentChannel);
        }

        _settingsService.Save();
        CatalogUpdated?.Invoke();
        StateChanged?.Invoke();
    }

    public void NotifyPlaybackPlaying()
    {
        RunOnUiThread(() =>
        {
            _playbackState = LiveTvPlaybackState.Playing;
            if (_currentChannel != null)
            {
                _currentChannel.IsPlaying = true;
                _statusMessage = $"LIVE • {_currentChannel.Name} ({_currentChannel.CountryName})";
            }
            StateChanged?.Invoke();
        });
    }

    public void NotifyPlaybackPaused()
    {
        RunOnUiThread(() =>
        {
            _playbackState = LiveTvPlaybackState.Paused;
            if (_currentChannel != null)
            {
                _currentChannel.IsPlaying = false;
                _statusMessage = $"Dijeda • {_currentChannel.Name}";
            }
            StateChanged?.Invoke();
        });
    }

    public void NotifyPlaybackBuffering()
    {
        RunOnUiThread(() =>
        {
            _playbackState = LiveTvPlaybackState.Buffering;
            if (_currentChannel != null)
            {
                _statusMessage = $"Memuat siaran {_currentChannel.Name}...";
            }
            StateChanged?.Invoke();
        });
    }

    public void NotifyPlaybackError(string? reason = null)
    {
        RunOnUiThread(() =>
        {
            if (!TryNextFallbackSource())
            {
                if (_currentChannel != null)
                {
                    _currentChannel.IsPlaying = false;
                    _statusMessage = string.IsNullOrWhiteSpace(reason)
                        ? $"Siaran {_currentChannel.Name} sedang sibuk/offline"
                        : $"Gagal memutar {_currentChannel.Name}: {reason}";
                }
                _playbackState = LiveTvPlaybackState.Error;
                StateChanged?.Invoke();
            }
        });
    }

    public async Task<List<LiveTvChannel>> LoadCountryChannelsAsync(string countryCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || string.Equals(countryCode, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            return _channels.ToList();
        }

        var code = countryCode.Trim().ToLowerInvariant();
        if (_countryCache.TryGetValue(code, out var cached) && cached.Count > 0)
        {
            MergeChannelsIntoCatalog(cached);
            return cached;
        }

        SetLoadingCatalog(true, $"Memuat saluran TV ({GetCountryDisplayName(code)})...");
        try
        {
            var url = $"{CatalogRawBaseUrl}/countries/{code}.json";
            var cacheFile = Path.Combine(_cacheDir, $"country_{code}.json");
            var json = await FetchJsonWithCacheAsync(url, cacheFile, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<LiveTvChannel>();
            }

            var parsed = ParseCatalogChannelsJson(json, defaultCountryCode: code);
            if (parsed.Count > 0)
            {
                _countryCache[code] = parsed;
                RunOnUiThread(() =>
                {
                    MergeChannelsIntoCatalog(parsed);
                    _statusMessage = $"Tersedia {parsed.Count} saluran TV dari {GetCountryDisplayName(code)}";
                    CatalogUpdated?.Invoke();
                    StateChanged?.Invoke();
                });
            }
            return parsed;
        }
        catch
        {
            return new List<LiveTvChannel>();
        }
        finally
        {
            SetLoadingCatalog(false);
        }
    }

    public async Task<List<LiveTvChannel>> LoadCategoryChannelsAsync(string categoryFilter, string? preferredCountryCode = null, CancellationToken cancellationToken = default)
    {
        var slug = MapCategoryToCatalogSlug(categoryFilter);
        if (string.IsNullOrWhiteSpace(slug))
        {
            return new List<LiveTvChannel>();
        }

        SetLoadingCatalog(true, $"Memuat kategori {categoryFilter}...");
        try
        {
            var url = $"{CatalogRawBaseUrl}/categories/{slug}.json";
            var cacheFile = Path.Combine(_cacheDir, $"cat_{slug}.json");
            var json = await FetchJsonWithCacheAsync(url, cacheFile, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<LiveTvChannel>();
            }

            var parsed = ParseCatalogChannelsJson(json, forcedCategory: categoryFilter);
            if (!string.IsNullOrWhiteSpace(preferredCountryCode) &&
                !string.Equals(preferredCountryCode, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                parsed = parsed
                    .OrderByDescending(c => string.Equals(c.CountryCode, preferredCountryCode, StringComparison.OrdinalIgnoreCase))
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(220)
                    .ToList();
            }
            else
            {
                parsed = parsed
                    .OrderByDescending(c => string.Equals(c.CountryCode, "id", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(220)
                    .ToList();
            }

            if (parsed.Count > 0)
            {
                RunOnUiThread(() =>
                {
                    MergeChannelsIntoCatalog(parsed);
                    _statusMessage = $"Memuat {parsed.Count} saluran kategori {categoryFilter}";
                    CatalogUpdated?.Invoke();
                    StateChanged?.Invoke();
                });
            }
            return parsed;
        }
        catch
        {
            return new List<LiveTvChannel>();
        }
        finally
        {
            SetLoadingCatalog(false);
        }
    }

    public async Task<List<LiveTvChannel>> SearchOnlineChannelsAsync(string query, string? countryCode = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new List<LiveTvChannel>();
        }

        var q = query.Trim();
        SetLoadingCatalog(true, $"Mencari '{q}' di katalog Live TV...");
        try
        {
            // Ensure target country & Indonesia are loaded first
            var targetCountry = !string.IsNullOrWhiteSpace(countryCode) && !string.Equals(countryCode, "ALL", StringComparison.OrdinalIgnoreCase)
                ? countryCode.Trim().ToLowerInvariant()
                : "id";

            await LoadCountryChannelsAsync(targetCountry, cancellationToken);

            // Also search all.json (cached locally) if needed for global search
            var allCacheFile = Path.Combine(_cacheDir, "cat_all.json");
            var allUrl = $"{CatalogRawBaseUrl}/categories/all.json";
            var json = await FetchJsonWithCacheAsync(allUrl, allCacheFile, cancellationToken);
            var results = new List<LiveTvChannel>();

            if (!string.IsNullOrWhiteSpace(json))
            {
                var allChannels = ParseCatalogChannelsJson(json);
                results = allChannels
                    .Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                c.CountryName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                c.CountryCode.Equals(q, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(c => string.Equals(c.CountryCode, targetCountry, StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(c => string.Equals(c.CountryCode, "id", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(80)
                    .ToList();

                if (results.Count > 0)
                {
                    RunOnUiThread(() =>
                    {
                        MergeChannelsIntoCatalog(results);
                        _statusMessage = $"Ditemukan {results.Count} saluran untuk '{q}'";
                        CatalogUpdated?.Invoke();
                        StateChanged?.Invoke();
                    });
                }
            }

            return results;
        }
        catch
        {
            return new List<LiveTvChannel>();
        }
        finally
        {
            SetLoadingCatalog(false);
        }
    }

    private async Task<string?> FetchJsonWithCacheAsync(string url, string cacheFilePath, CancellationToken cancellationToken)
    {
        try
        {
            if (File.Exists(cacheFilePath))
            {
                var age = DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFilePath);
                if (age < TimeSpan.FromHours(12))
                {
                    return await File.ReadAllTextAsync(cacheFilePath, cancellationToken);
                }
            }
        }
        catch { }

        try
        {
            using var resp = await _httpClient.GetAsync(url, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    try { await File.WriteAllTextAsync(cacheFilePath, json, cancellationToken); } catch { }
                    return json;
                }
            }
        }
        catch { }

        try
        {
            if (File.Exists(cacheFilePath))
            {
                return await File.ReadAllTextAsync(cacheFilePath, cancellationToken);
            }
        }
        catch { }

        return null;
    }

    private List<LiveTvChannel> ParseCatalogChannelsJson(string json, string? defaultCountryCode = null, string? forcedCategory = null)
    {
        var list = new List<LiveTvChannel>();
        var favIds = _settingsService.CurrentSettings.FavoriteLiveTvChannelIds ?? new List<string>();
        var favSet = new HashSet<string>(favIds, StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return list;

        foreach (var item in doc.RootElement.EnumerateArray())
        {
            try
            {
                var name = item.TryGetProperty("name", out var nProp) ? nProp.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var nanoid = item.TryGetProperty("nanoid", out var idProp) ? idProp.GetString() : null;
                var country = item.TryGetProperty("country", out var cProp) ? cProp.GetString() : defaultCountryCode;
                country = string.IsNullOrWhiteSpace(country) ? (defaultCountryCode ?? "id") : country.Trim().ToLowerInvariant();
                bool isGeoBlocked = item.TryGetProperty("isGeoBlocked", out var geoProp) && geoProp.ValueKind == JsonValueKind.True;

                var allUrls = new List<string>();
                if (item.TryGetProperty("sources", out var sourcesObj) && sourcesObj.ValueKind == JsonValueKind.Object)
                {
                    if (sourcesObj.TryGetProperty("streams", out var streamsArr) && streamsArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var s in streamsArr.EnumerateArray())
                        {
                            var u = s.GetString();
                            if (!string.IsNullOrWhiteSpace(u))
                            {
                                allUrls.Add(u.Trim());
                            }
                        }
                    }

                    if (sourcesObj.TryGetProperty("youtube", out var ytArr) && ytArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var y in ytArr.EnumerateArray())
                        {
                            var u = y.GetString();
                            if (!string.IsNullOrWhiteSpace(u))
                            {
                                allUrls.Add(NormalizeYouTubeOrStreamUrl(u.Trim()));
                            }
                        }
                    }
                }

                if (allUrls.Count == 0) continue;

                var id = !string.IsNullOrWhiteSpace(nanoid) ? nanoid : $"fl-{country}-{name.ToLowerInvariant().Replace(' ', '-')}";
                var countryName = GetCountryDisplayName(country);
                var category = !string.IsNullOrWhiteSpace(forcedCategory)
                    ? forcedCategory
                    : InferChannelCategory(name, country);

                var channel = new LiveTvChannel
                {
                    Id = id,
                    Name = name.Trim(),
                    CountryCode = country,
                    CountryName = countryName,
                    Category = category,
                    StreamUrl = allUrls[0],
                    FallbackUrls = allUrls.Skip(1).ToList(),
                    IsGeoBlocked = isGeoBlocked,
                    IsFavorite = favSet.Contains(id)
                };

                list.Add(channel);
            }
            catch { }
        }

        return list;
    }

    private void MergeChannelsIntoCatalog(IEnumerable<LiveTvChannel> incoming)
    {
        var favIds = _settingsService.CurrentSettings.FavoriteLiveTvChannelIds ?? new List<string>();
        var favSet = new HashSet<string>(favIds, StringComparer.OrdinalIgnoreCase);

        foreach (var ch in incoming)
        {
            var existing = _channels.FirstOrDefault(x =>
                string.Equals(x.Id, ch.Id, StringComparison.OrdinalIgnoreCase) ||
                (string.Equals(x.Name, ch.Name, StringComparison.OrdinalIgnoreCase) &&
                 string.Equals(x.CountryCode, ch.CountryCode, StringComparison.OrdinalIgnoreCase)));

            if (existing != null)
            {
                // Enrich existing channel with any additional fallback streams
                foreach (var url in ch.AllSources)
                {
                    if (!string.Equals(existing.StreamUrl, url, StringComparison.OrdinalIgnoreCase) &&
                        !existing.FallbackUrls.Contains(url, StringComparer.OrdinalIgnoreCase))
                    {
                        existing.FallbackUrls.Add(url);
                    }
                }
            }
            else
            {
                ch.IsFavorite = favSet.Contains(ch.Id) || ch.IsFavorite;
                _channels.Add(ch);
            }
        }
    }

    private static string InferChannelCategory(string name, string countryCode)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("news") || n.Contains("berita") || n.Contains("kompas") || n.Contains("metro") ||
            n.Contains("tvone") || n.Contains("cnbc") || n.Contains("cnn") || n.Contains("bloomberg") ||
            n.Contains("jazeera") || n.Contains("france 24") || n.Contains("bbc") || n.Contains("btv"))
        {
            return "Berita";
        }

        if (n.Contains("sport") || n.Contains("arena") || n.Contains("bola") || n.Contains("red bull") ||
            n.Contains("racing") || n.Contains("golf") || n.Contains("fight") || n.Contains("motor"))
        {
            return "Olahraga";
        }

        if (n.Contains("rodja") || n.Contains("quran") || n.Contains("qur'an") || n.Contains("sunnah") ||
            n.Contains("ahsan") || n.Contains("ashiil") || n.Contains("madani") || n.Contains("mqtv") ||
            n.Contains("islam") || n.Contains("muslim") || n.Contains("surau") || n.Contains("tarim") ||
            n.Contains("tv 9") || n.Contains("tv9") || n.Contains("salam") || n.Contains("al wafa") ||
            n.Contains("gazwah") || n.Contains("marhaban") || n.Contains("muhammadiyah") || n.Contains("dhamma") ||
            n.Contains("angel") || n.Contains("uchannel") || n.Contains("u channel") || n.Contains("ewtn"))
        {
            return "Religi";
        }

        if (n.Contains("music") || n.Contains("musik") || n.Contains("mtv") || n.Contains("vevo") ||
            n.Contains("hits") || n.Contains("pop") || n.Contains("aquarius") || n.Contains("radio"))
        {
            return "Musik";
        }

        if (n.Contains("kid") || n.Contains("cartoon") || n.Contains("anime") || n.Contains("nick") ||
            n.Contains("disney") || n.Contains("baby") || n.Contains("pbs kids") || n.Contains("anak"))
        {
            return "Anak";
        }

        if (string.Equals(countryCode, "id", StringComparison.OrdinalIgnoreCase))
        {
            return "Indonesia";
        }

        return "Internasional";
    }

    private static string? MapCategoryToCatalogSlug(string categoryFilter)
    {
        return categoryFilter?.Trim() switch
        {
            "Berita" => "news",
            "Olahraga" => "sports",
            "Hiburan" => "entertainment",
            "Religi" => "religious",
            "Musik" => "music",
            "Anak" => "kids",
            "Film" => "movies",
            "Dokumenter" => "documentary",
            _ => null
        };
    }

    public static string NormalizeYouTubeOrStreamUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return string.Empty;

        if (url.Contains("youtube.com/watch?v=", StringComparison.OrdinalIgnoreCase))
        {
            var uri = new Uri(url);
            var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in query)
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0] == "v" && !string.IsNullOrWhiteSpace(kv[1]))
                {
                    return $"https://www.youtube-nocookie.com/embed/{kv[1]}";
                }
            }
        }
        else if (url.Contains("youtu.be/", StringComparison.OrdinalIgnoreCase))
        {
            var idx = url.IndexOf("youtu.be/", StringComparison.OrdinalIgnoreCase);
            var id = url[(idx + "youtu.be/".Length)..].Split('?', '&', '/')[0];
            if (!string.IsNullOrWhiteSpace(id))
            {
                return $"https://www.youtube-nocookie.com/embed/{id}";
            }
        }

        return url;
    }

    private string GetCountryDisplayName(string countryCode)
    {
        var match = _countries.FirstOrDefault(c => string.Equals(c.Code, countryCode, StringComparison.OrdinalIgnoreCase));
        if (match != null) return match.Name;
        return countryCode.ToUpperInvariant();
    }

    private void SetLoadingCatalog(bool isLoading, string? status = null)
    {
        RunOnUiThread(() =>
        {
            _isLoadingCatalog = isLoading;
            if (!string.IsNullOrWhiteSpace(status))
            {
                _statusMessage = status;
            }
            StateChanged?.Invoke();
        });
    }

    private static void RunOnUiThread(Action action)
    {
        if (System.Windows.Application.Current?.Dispatcher != null &&
            !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        try { _httpClient.Dispose(); } catch { }
    }
}
