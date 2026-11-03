using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using NotchMusic.Models;
using NotchMusic.Services.LiveTv;

namespace NotchMusic.ViewModels;

public partial class MainViewModel
{
    private readonly ILiveTvService _liveTvService;

    // Live TV Fields (Komponen ke-6)
    private readonly ICollectionView _filteredLiveTvChannels;

    private string _selectedLiveTvCountryCode = "ALL";

    private string _liveTvCategoryFilter = "All";

    private string _liveTvSearchQuery = string.Empty;

    private bool _isSearchingOnlineLiveTv;

    private bool _isAddingCustomLiveTv;

    private string _customLiveTvName = string.Empty;

    private string _customLiveTvUrl = string.Empty;

    private CancellationTokenSource? _liveTvSearchCts;

    private bool _hasLoadedInitialLiveTvCatalog;

    public event Action? RequestOpenLiveTv;

    // ================= LIVE TV PROPERTIES =================
    public ILiveTvService LiveTvService => _liveTvService;

    public ObservableCollection<LiveTvChannel> LiveTvChannels { get; } = new();

    public ObservableCollection<LiveTvCountryOption> LiveTvCountries { get; } = new();

    public ICollectionView FilteredLiveTvChannels => _filteredLiveTvChannels;

    public bool IsLiveTvWindowOpen
    {
        get => Settings.IsLiveTvWindowOpen;
        set
        {
            if (Settings.IsLiveTvWindowOpen != value)
            {
                Settings.IsLiveTvWindowOpen = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsLiveTvWindowOpen));
                OnPropertyChanged(nameof(IsLiveTvActiveOrOpen));
                OnPropertyChanged(nameof(LiveTvNotchTooltip));
                OnPropertyChanged(nameof(IsNotchActive));
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool IsLiveTvWindowCompact
    {
        get => Settings.IsLiveTvWindowCompact;
        set
        {
            if (Settings.IsLiveTvWindowCompact != value)
            {
                Settings.IsLiveTvWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsLiveTvWindowCompact));
                OnPropertyChanged(nameof(IsLiveTvWindowExpanded));
            }
        }
    }

    public bool IsLiveTvWindowExpanded => !IsLiveTvWindowCompact;

    public LiveTvChannel? CurrentLiveTvChannel => _liveTvService.CurrentChannel;

    public string CurrentLiveTvChannelName => _liveTvService.CurrentChannel?.Name ?? "Pilih Saluran TV";

    public string CurrentLiveTvChannelSubtitle => _liveTvService.CurrentChannel?.Subtitle ?? "Live TV Indonesia & Dunia";

    public string CurrentLiveTvFavoriteSymbol => _liveTvService.CurrentChannel?.FavoriteSymbol ?? "☆";

    public string CurrentLiveTvSourceBadge => _liveTvService.CurrentChannel?.SourceBadgeText ?? "HLS";

    public bool IsLiveTvPlaying => _liveTvService.PlaybackState == LiveTvPlaybackState.Playing;

    public bool IsLiveTvBuffering => _liveTvService.PlaybackState == LiveTvPlaybackState.Buffering;

    public bool IsLiveTvActiveOrOpen => IsLiveTvPlaying || IsLiveTvBuffering || IsLiveTvWindowOpen;

    public string LiveTvStateBadgeText => _liveTvService.PlaybackState switch
    {
        LiveTvPlaybackState.Playing => "LIVE",
        LiveTvPlaybackState.Buffering => "BUFFERING",
        LiveTvPlaybackState.Paused => "PAUSED",
        LiveTvPlaybackState.Error => "OFFLINE",
        _ => "SIAP"
    };

    public string LiveTvStatusMessage => _liveTvService.StatusMessage;

    public string LiveTvNotchTooltip => _liveTvService.CurrentChannel != null
        ? $"Live TV: {_liveTvService.CurrentChannel.Name} ({LiveTvStateBadgeText})\nKlik untuk membuka pemutar & katalog saluran Live TV"
        : "Buka Live TV (Streaming TV Indonesia & Dunia)";

    public double LiveTvVolume
    {
        get => _liveTvService.Volume;
        set
        {
            _liveTvService.Volume = value;
            if (value > 0 && _liveTvService.IsMuted)
            {
                _liveTvService.IsMuted = false;
            }
            OnPropertyChanged(nameof(LiveTvVolume));
            OnPropertyChanged(nameof(LiveTvVolumePercentText));
            OnPropertyChanged(nameof(LiveTvVolumeIcon));
        }
    }

    public bool IsLiveTvMuted
    {
        get => _liveTvService.IsMuted;
        set
        {
            _liveTvService.IsMuted = value;
            OnPropertyChanged(nameof(IsLiveTvMuted));
            OnPropertyChanged(nameof(LiveTvVolumePercentText));
            OnPropertyChanged(nameof(LiveTvVolumeIcon));
        }
    }

    public string LiveTvVolumePercentText => IsLiveTvMuted ? "Mute" : $"{Math.Round(LiveTvVolume * 100)}%";

    public string LiveTvVolumeIcon => IsLiveTvMuted || LiveTvVolume <= 0.01 ? "🔇" : (LiveTvVolume < 0.5 ? "🔉" : "🔊");

    public string SelectedLiveTvCountryCode
    {
        get => _selectedLiveTvCountryCode;
        set
        {
            if (SetProperty(ref _selectedLiveTvCountryCode, value ?? "ALL"))
            {
                Settings.LastLiveTvCountryCode = _selectedLiveTvCountryCode;
                _settingsService.Save();
                _filteredLiveTvChannels?.Refresh();
                OnPropertyChanged(nameof(LiveTvCatalogCountText));
                _ = LoadSelectedLiveTvCountryAsync(_selectedLiveTvCountryCode);
            }
        }
    }

    public string LiveTvCategoryFilter
    {
        get => _liveTvCategoryFilter;
        set
        {
            if (SetProperty(ref _liveTvCategoryFilter, value ?? "All"))
            {
                _filteredLiveTvChannels?.Refresh();
                OnPropertyChanged(nameof(LiveTvCatalogCountText));
                _ = LoadSelectedLiveTvCategoryIfNeededAsync(_liveTvCategoryFilter);
            }
        }
    }

    public string LiveTvSearchQuery
    {
        get => _liveTvSearchQuery;
        set
        {
            if (SetProperty(ref _liveTvSearchQuery, value ?? string.Empty))
            {
                _filteredLiveTvChannels?.Refresh();
                OnPropertyChanged(nameof(LiveTvCatalogCountText));
            }
        }
    }

    public string LiveTvCatalogCountText
    {
        get
        {
            var count = _filteredLiveTvChannels?.Cast<object>().Count() ?? LiveTvChannels.Count;
            return $"{count} Saluran";
        }
    }

    public bool IsSearchingOnlineLiveTv
    {
        get => _isSearchingOnlineLiveTv;
        set
        {
            if (SetProperty(ref _isSearchingOnlineLiveTv, value))
            {
                OnPropertyChanged(nameof(SearchOnlineLiveTvButtonText));
            }
        }
    }

    public string SearchOnlineLiveTvButtonText => IsSearchingOnlineLiveTv ? "Mencari..." : "🌐 Cari Online";

    public bool IsAddingCustomLiveTv
    {
        get => _isAddingCustomLiveTv;
        set => SetProperty(ref _isAddingCustomLiveTv, value);
    }

    public string CustomLiveTvName
    {
        get => _customLiveTvName;
        set => SetProperty(ref _customLiveTvName, value);
    }

    public string CustomLiveTvUrl
    {
        get => _customLiveTvUrl;
        set => SetProperty(ref _customLiveTvUrl, value);
    }

    public ICommand OpenLiveTvCommand { get; }

    public ICommand ToggleLiveTvWindowCommand { get; }

    public ICommand CloseLiveTvWindowCommand { get; }

    public ICommand MinimizeLiveTvWindowCommand { get; }

    public ICommand ExpandLiveTvWindowCommand { get; }

    public ICommand ToggleLiveTvPlayPauseCommand { get; }

    public ICommand StopLiveTvCommand { get; }

    public ICommand NextLiveTvChannelCommand { get; }

    public ICommand PreviousLiveTvChannelCommand { get; }

    public ICommand PlayLiveTvChannelCommand { get; }

    public ICommand CycleLiveTvSourceCommand { get; }

    public ICommand SetLiveTvCategoryCommand { get; }

    public ICommand ToggleCurrentLiveTvFavoriteCommand { get; }

    public ICommand ToggleLiveTvChannelFavoriteCommand { get; }

    public ICommand ToggleLiveTvMuteCommand { get; }

    public ICommand SearchOnlineLiveTvCommand { get; }

    public ICommand ToggleAddCustomLiveTvCommand { get; }

    public ICommand SaveCustomLiveTvCommand { get; }

    public ICommand DeleteCustomLiveTvChannelCommand { get; }

    // ================= LIVE TV HELPER METHODS =================
    public async Task EnsureInitialLiveTvCatalogLoadedAsync()
    {
        if (_hasLoadedInitialLiveTvCatalog) return;
        _hasLoadedInitialLiveTvCatalog = true;

        try
        {
            await _liveTvService.LoadCountryChannelsAsync("id");
        }
        catch { }
    }

    private async Task LoadSelectedLiveTvCountryAsync(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode) || string.Equals(countryCode, "ALL", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            await _liveTvService.LoadCountryChannelsAsync(countryCode);
        }
        catch { }
    }

    private async Task LoadSelectedLiveTvCategoryIfNeededAsync(string category)
    {
        if (string.IsNullOrWhiteSpace(category) ||
            string.Equals(category, "All", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "Favorites", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "Indonesia", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "Internasional", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await _liveTvService.LoadCategoryChannelsAsync(category, _selectedLiveTvCountryCode);
        }
        catch { }
    }

    private void OnLiveTvCatalogUpdated()
    {
        Action sync = () =>
        {
            var existingIds = LiveTvChannels.Select(c => c.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var ch in _liveTvService.Channels)
            {
                if (!existingIds.Contains(ch.Id))
                {
                    LiveTvChannels.Add(ch);
                    existingIds.Add(ch.Id);
                }
            }

            _filteredLiveTvChannels?.Refresh();
            OnPropertyChanged(nameof(LiveTvCatalogCountText));
        };

        if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(sync);
        }
        else
        {
            sync();
        }
    }

    private void OnLiveTvStateChanged()
    {
        Action notify = () =>
        {
            OnPropertyChanged(nameof(CurrentLiveTvChannel));
            OnPropertyChanged(nameof(CurrentLiveTvChannelName));
            OnPropertyChanged(nameof(CurrentLiveTvChannelSubtitle));
            OnPropertyChanged(nameof(CurrentLiveTvFavoriteSymbol));
            OnPropertyChanged(nameof(CurrentLiveTvSourceBadge));
            OnPropertyChanged(nameof(IsLiveTvPlaying));
            OnPropertyChanged(nameof(IsLiveTvBuffering));
            OnPropertyChanged(nameof(IsLiveTvActiveOrOpen));
            OnPropertyChanged(nameof(IsAudioActive));
            OnPropertyChanged(nameof(IsNotchActive));
            OnPropertyChanged(nameof(LiveTvStateBadgeText));
            OnPropertyChanged(nameof(LiveTvStatusMessage));
            OnPropertyChanged(nameof(LiveTvNotchTooltip));
            OnPropertyChanged(nameof(LiveTvVolume));
            OnPropertyChanged(nameof(IsLiveTvMuted));
            OnPropertyChanged(nameof(LiveTvVolumePercentText));
            OnPropertyChanged(nameof(LiveTvVolumeIcon));
        };

        if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(notify);
        }
        else
        {
            notify();
        }
    }

    private bool FilterLiveTvChannel(object obj)
    {
        if (obj is not LiveTvChannel ch) return false;

        if (!string.IsNullOrWhiteSpace(_selectedLiveTvCountryCode) &&
            !string.Equals(_selectedLiveTvCountryCode, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(ch.CountryCode, _selectedLiveTvCountryCode, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(_liveTvCategoryFilter) &&
            !string.Equals(_liveTvCategoryFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(_liveTvCategoryFilter, "Favorites", StringComparison.OrdinalIgnoreCase))
            {
                if (!ch.IsFavorite) return false;
            }
            else if (string.Equals(_liveTvCategoryFilter, "Indonesia", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(ch.CountryCode, "id", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(ch.Category, "Indonesia", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            else if (string.Equals(_liveTvCategoryFilter, "Internasional", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(ch.CountryCode, "id", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            else if (!string.Equals(ch.Category, _liveTvCategoryFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(_liveTvSearchQuery))
        {
            var q = _liveTvSearchQuery.Trim();
            bool match = ch.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                      || ch.CountryName.Contains(q, StringComparison.OrdinalIgnoreCase)
                      || ch.CountryCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                      || ch.Category.Contains(q, StringComparison.OrdinalIgnoreCase);
            if (!match) return false;
        }

        return true;
    }

    private async Task ExecuteOnlineLiveTvSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(LiveTvSearchQuery)) return;

        _liveTvSearchCts?.Cancel();
        _liveTvSearchCts?.Dispose();
        _liveTvSearchCts = new CancellationTokenSource();
        var token = _liveTvSearchCts.Token;

        IsSearchingOnlineLiveTv = true;
        try
        {
            var results = await _liveTvService.SearchOnlineChannelsAsync(LiveTvSearchQuery, _selectedLiveTvCountryCode, token);
            if (!token.IsCancellationRequested && results.Count > 0)
            {
                LiveTvCategoryFilter = "All";
                OnLiveTvCatalogUpdated();
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            IsSearchingOnlineLiveTv = false;
        }
    }

    private void SaveCustomLiveTvChannel()
    {
        if (string.IsNullOrWhiteSpace(CustomLiveTvName) || string.IsNullOrWhiteSpace(CustomLiveTvUrl))
            return;

        var url = CustomLiveTvUrl.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        var ch = _liveTvService.AddCustomChannel(CustomLiveTvName, url);
        if (!LiveTvChannels.Contains(ch))
        {
            LiveTvChannels.Insert(0, ch);
        }

        CustomLiveTvName = string.Empty;
        CustomLiveTvUrl = string.Empty;
        IsAddingCustomLiveTv = false;
        SelectedLiveTvCountryCode = "ALL";
        LiveTvCategoryFilter = "All";
        _filteredLiveTvChannels?.Refresh();
        OnPropertyChanged(nameof(LiveTvCatalogCountText));
        _liveTvService.PlayChannel(ch);
    }
}
