using System.Net.Http;
using System.Text.Json;
using TamaNotch.Helpers;
using TamaNotch.Models;
using TamaNotch.Services.Settings;

namespace TamaNotch.Services.Quran;

public interface IQuranService : IDisposable
{
    IReadOnlyList<QuranSurah> Surahs { get; }
    QuranSurah? CurrentSurah { get; }
    QuranPlaybackState PlaybackState { get; }
    string StatusMessage { get; }
    TimeSpan Position { get; }
    TimeSpan Duration { get; }
    double Volume { get; set; }
    bool IsMuted { get; set; }
    QuranAfterPlayback AfterPlayback { get; set; }

    event Action? StateChanged;

    /// <summary>Posisi atau durasi berubah saat audio berjalan (sekitar dua kali per detik).</summary>
    event Action? ProgressChanged;

    void PlaySurah(QuranSurah surah);
    void TogglePlayPause();
    void Stop();
    void Next();
    void Previous();
    void PlayRandom();
    void SeekToFraction(double fraction);
}

/// <summary>
/// Pemutar Al-Qur'an per surah. Daftar 114 surah dari <c>Assets/Data/quran-surahs.json</c>; alamat audio diambil
/// dari Quran.com API v4 (<c>chapter_recitations</c>) lalu diputar lewat <see cref="IQuranAudioPlayer"/>.
/// </summary>
public sealed class QuranService : IQuranService
{
    public const string ApiBase = "https://api.quran.com/api/v4";

    private static readonly HttpClient SharedClient = CreateClient();

    private readonly ISettingsService _settingsService;
    private readonly IQuranAudioPlayer _player;
    private readonly HttpClient _http;
    private readonly Random _random;
    private readonly List<QuranSurah> _surahs;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<(int Reciter, int Surah), Uri> _urlCache = new();
    private readonly System.Windows.Threading.DispatcherTimer? _progressTimer;
    private QuranSurah? _current;
    private QuranPlaybackState _state = QuranPlaybackState.Stopped;
    private string _status = "Siap memutar Al-Qur'an";
    private int _attempt;
    private bool _isDisposed;

    public QuranService(
        ISettingsService settingsService,
        IQuranAudioPlayer? player = null,
        HttpClient? http = null,
        IEnumerable<QuranSurah>? surahs = null,
        Random? random = null,
        bool useTimer = true)
    {
        _settingsService = settingsService;
        _player = player ?? new MediaQuranAudioPlayer();
        _http = http ?? SharedClient;
        _random = random ?? Random.Shared;
        _surahs = (surahs ?? CatalogLoader.Load<QuranSurah>("quran-surahs.json")).OrderBy(s => s.Number).ToList();

        _player.Volume = Volume;
        _player.IsMuted = IsMuted;
        _player.Opened += () => RunOnUiThread(OnOpened);
        _player.Ended += () => RunOnUiThread(OnEnded);
        _player.Failed += message => RunOnUiThread(() => OnFailed(message));

        if (useTimer)
        {
            _progressTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _progressTimer.Tick += (s, e) => ProgressChanged?.Invoke();
        }

        int last = _settingsService.CurrentSettings.LastQuranSurah;
        _current = _surahs.FirstOrDefault(s => s.Number == last);
        if (_current != null) _current.IsCurrent = true;
    }

    public IReadOnlyList<QuranSurah> Surahs => _surahs;
    public QuranSurah? CurrentSurah => _current;
    public QuranPlaybackState PlaybackState => _state;
    public string StatusMessage => _status;
    public TimeSpan Position => _player.Position;
    public TimeSpan Duration => _player.Duration;

    public double Volume
    {
        get => _settingsService.CurrentSettings.QuranVolume;
        set
        {
            var clamped = Math.Clamp(value, 0.0, 1.0);
            if (Math.Abs(_settingsService.CurrentSettings.QuranVolume - clamped) < 0.001) return;
            _settingsService.CurrentSettings.QuranVolume = clamped;
            _settingsService.Save();
            _player.Volume = clamped;
            RunOnUiThread(() => StateChanged?.Invoke());
        }
    }

    public bool IsMuted
    {
        get => _settingsService.CurrentSettings.QuranMuted;
        set
        {
            if (_settingsService.CurrentSettings.QuranMuted == value) return;
            _settingsService.CurrentSettings.QuranMuted = value;
            _settingsService.Save();
            _player.IsMuted = value;
            RunOnUiThread(() => StateChanged?.Invoke());
        }
    }

    public QuranAfterPlayback AfterPlayback
    {
        get => Enum.TryParse<QuranAfterPlayback>(_settingsService.CurrentSettings.QuranAfterPlayback, true, out var mode)
            ? mode
            : QuranAfterPlayback.Next;
        set
        {
            if (AfterPlayback == value) return;
            _settingsService.CurrentSettings.QuranAfterPlayback = value.ToString();
            _settingsService.Save();
            StateChanged?.Invoke();
        }
    }

    public event Action? StateChanged;
    public event Action? ProgressChanged;

    public void PlaySurah(QuranSurah surah)
    {
        if (surah == null) return;

        SetCurrent(surah);
        int attempt = Interlocked.Increment(ref _attempt);
        _player.Stop();
        SetState(QuranPlaybackState.Loading, $"Memuat {surah.Title}...");

        _settingsService.CurrentSettings.LastQuranSurah = surah.Number;
        _settingsService.Save();

        _ = ResolveAndOpenAsync(surah, attempt);
    }

    private async Task ResolveAndOpenAsync(QuranSurah surah, int attempt)
    {
        try
        {
            var uri = await ResolveAudioUriAsync(surah.Number).ConfigureAwait(false);
            RunOnUiThread(() =>
            {
                if (attempt != Volatile.Read(ref _attempt)) return;
                _player.Open(uri);
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            RunOnUiThread(() =>
            {
                if (attempt != Volatile.Read(ref _attempt)) return;
                SetState(QuranPlaybackState.Error, "Gagal mengambil audio. Periksa koneksi internet lalu coba lagi.");
            });
        }
    }

    private async Task<Uri> ResolveAudioUriAsync(int surahNumber)
    {
        int reciter = _settingsService.CurrentSettings.QuranReciterId;
        if (_urlCache.TryGetValue((reciter, surahNumber), out var cached)) return cached;

        using var response = await _http.GetAsync($"{ApiBase}/chapter_recitations/{reciter}/{surahNumber}").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        var uri = ParseAudioUri(json);
        _urlCache[(reciter, surahNumber)] = uri;
        return uri;
    }

    /// <summary>Membaca <c>audio_file.audio_url</c>; hanya alamat https yang diterima.</summary>
    public static Uri ParseAudioUri(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("audio_file", out var file) &&
            file.ValueKind == JsonValueKind.Object &&
            file.TryGetProperty("audio_url", out var url) &&
            url.ValueKind == JsonValueKind.String &&
            Uri.TryCreate(url.GetString(), UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps)
        {
            return uri;
        }

        throw new InvalidOperationException("Respons Quran.com tidak memuat alamat audio.");
    }

    public void TogglePlayPause()
    {
        switch (_state)
        {
            case QuranPlaybackState.Playing:
                _player.Pause();
                SetState(QuranPlaybackState.Paused, $"Dijeda • {_current?.Title}");
                break;
            case QuranPlaybackState.Paused:
                _player.Resume();
                SetState(QuranPlaybackState.Playing, $"Memutar • {_current?.Title}");
                break;
            case QuranPlaybackState.Loading:
                Stop();
                break;
            default:
                var target = _current ?? _surahs.FirstOrDefault();
                if (target != null) PlaySurah(target);
                break;
        }
    }

    public void Stop()
    {
        Interlocked.Increment(ref _attempt);
        _player.Stop();
        SetState(QuranPlaybackState.Stopped, _current != null ? $"Dihentikan • {_current.Title}" : "Siap memutar Al-Qur'an");
    }

    public void Next()
    {
        if (_surahs.Count == 0) return;
        int idx = _current == null ? -1 : _surahs.IndexOf(_current);
        PlaySurah(_surahs[(idx + 1) % _surahs.Count]);
    }

    public void Previous()
    {
        if (_surahs.Count == 0) return;
        int idx = _current == null ? 0 : Math.Max(0, _surahs.IndexOf(_current));
        PlaySurah(_surahs[(idx - 1 + _surahs.Count) % _surahs.Count]);
    }

    /// <summary>Surah acak yang berbeda dari yang sedang dipilih (bila ada lebih dari satu).</summary>
    public void PlayRandom()
    {
        if (_surahs.Count == 0) return;
        var pool = _current != null && _surahs.Count > 1 ? _surahs.Where(s => s != _current).ToList() : _surahs;
        PlaySurah(pool[_random.Next(pool.Count)]);
    }

    public void SeekToFraction(double fraction)
    {
        var duration = _player.Duration;
        if (duration <= TimeSpan.Zero || _state is not (QuranPlaybackState.Playing or QuranPlaybackState.Paused)) return;
        _player.Seek(TimeSpan.FromTicks((long)(duration.Ticks * Math.Clamp(fraction, 0.0, 1.0))));
        ProgressChanged?.Invoke();
    }

    private void OnOpened()
    {
        if (_state != QuranPlaybackState.Loading) return;
        SetState(QuranPlaybackState.Playing, $"Memutar • {_current?.Title}");
    }

    private void OnEnded()
    {
        if (_state is QuranPlaybackState.Stopped or QuranPlaybackState.Error) return;

        switch (AfterPlayback)
        {
            case QuranAfterPlayback.Next:
                Next();
                break;
            case QuranAfterPlayback.Random:
                PlayRandom();
                break;
            default:
                SetState(QuranPlaybackState.Stopped, $"Selesai • {_current?.Title}");
                break;
        }
    }

    private void OnFailed(string message)
    {
        if (_state is QuranPlaybackState.Stopped) return;
        SetState(QuranPlaybackState.Error, "Audio tidak dapat diputar. Periksa koneksi internet lalu coba lagi.");
        // Pesan galat mentah hanya berguna saat memeriksa masalah; tidak ditampilkan ke pengguna.
        System.Diagnostics.Debug.WriteLine($"[Quran] {message}");
    }

    private void SetCurrent(QuranSurah surah)
    {
        foreach (var s in _surahs)
        {
            s.IsCurrent = s == surah;
            s.IsPlaying = false;
        }
        _current = surah;
    }

    private void SetState(QuranPlaybackState state, string status)
    {
        _state = state;
        _status = status;

        if (_current != null) _current.IsPlaying = state == QuranPlaybackState.Playing;

        if (_progressTimer != null)
        {
            if (state == QuranPlaybackState.Playing) _progressTimer.Start();
            else _progressTimer.Stop();
        }

        StateChanged?.Invoke();
        ProgressChanged?.Invoke();
    }

    private static void RunOnUiThread(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess()) action();
        else dispatcher.BeginInvoke(action);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppVersion.UserAgentToken} (Windows; Quran.com API)");
        return client;
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        _progressTimer?.Stop();
        _player.Dispose();
    }
}
