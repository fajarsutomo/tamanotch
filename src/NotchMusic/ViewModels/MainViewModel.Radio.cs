using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using NotchMusic.Models;
using NotchMusic.Services.Radio;

namespace NotchMusic.ViewModels;

public partial class MainViewModel
{
    private readonly IRadioService _radioService;

    // Radio Online Fields (Komponen ke-5)
    private readonly ICollectionView _filteredRadioStations;

    private string _radioCategoryFilter = "All";

    private string _radioSearchQuery = string.Empty;

    private bool _isSearchingOnlineRadio;

    private bool _isAddingCustomRadio;

    private string _customRadioName = string.Empty;

    private string _customRadioUrl = string.Empty;

    private CancellationTokenSource? _radioSearchCts;

    public event Action? RequestOpenRadio;

    // Radio Online Properties (Di sebelah kanan Music Player)
    public IRadioService RadioService => _radioService;

    public ObservableCollection<RadioStation> RadioStations { get; } = new();

    public ICollectionView FilteredRadioStations => _filteredRadioStations;

    public bool IsRadioWindowOpen
    {
        get => Settings.IsRadioWindowOpen;
        set
        {
            if (Settings.IsRadioWindowOpen != value)
            {
                Settings.IsRadioWindowOpen = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsRadioWindowOpen));
                OnPropertyChanged(nameof(IsRadioActiveOrOpen));
                OnPropertyChanged(nameof(RadioStatusSummary));
                OnPropertyChanged(nameof(RadioButtonActionText));
                OnPropertyChanged(nameof(IsNotchActive));
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool IsRadioWindowCompact
    {
        get => Settings.IsRadioWindowCompact;
        set
        {
            if (Settings.IsRadioWindowCompact != value)
            {
                Settings.IsRadioWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsRadioWindowCompact));
                OnPropertyChanged(nameof(IsRadioWindowExpanded));
            }
        }
    }

    public bool IsRadioWindowExpanded => !IsRadioWindowCompact;

    public RadioStation? CurrentRadioStation => _radioService.CurrentStation;

    public string CurrentRadioStationName => _radioService.CurrentStation?.Name ?? "Pilih Stasiun Radio";

    public string CurrentRadioStationSubtitle => _radioService.CurrentStation?.Subtitle ?? "Radio Online Indonesia & Dunia";

    public string CurrentRadioFavoriteSymbol => _radioService.CurrentStation?.FavoriteSymbol ?? "☆";

    public bool IsRadioPlaying => _radioService.PlaybackState == RadioPlaybackState.Playing;

    public bool IsRadioBuffering => _radioService.PlaybackState == RadioPlaybackState.Buffering;

    public bool IsRadioActiveOrOpen => IsRadioPlaying || IsRadioBuffering || IsRadioWindowOpen;

    public string RadioStateBadgeText => _radioService.PlaybackState switch
    {
        RadioPlaybackState.Playing => "LIVE",
        RadioPlaybackState.Buffering => "BUFFERING",
        RadioPlaybackState.Error => "OFFLINE",
        _ => "SIAP"
    };

    public string RadioStatusMessage => _radioService.StatusMessage;

    public string RadioCompactLabel => (IsRadioPlaying || IsRadioBuffering) && _radioService.CurrentStation != null
        ? _radioService.CurrentStation.Name
        : "Radio";

    public string RadioNotchTooltip => _radioService.CurrentStation != null
        ? $"Radio Online: {_radioService.CurrentStation.Name} ({RadioStateBadgeText})\nKlik untuk membuka daftar stasiun & pemutar Radio Online"
        : "Buka Radio Online (Streaming Radio Indonesia & Dunia)";

    public string RadioStatusSummary => IsRadioPlaying && _radioService.CurrentStation != null
        ? $"LIVE: {_radioService.CurrentStation.Name}"
        : (IsRadioWindowOpen ? "Jendela Radio Aktif" : "Siap Memutar");

    public string RadioButtonActionText => IsRadioWindowOpen ? "Tutup" : "Buka";

    public double RadioVolume
    {
        get => _radioService.Volume;
        set
        {
            _radioService.Volume = value;
            if (value > 0 && _radioService.IsMuted)
            {
                _radioService.IsMuted = false;
            }
            OnPropertyChanged(nameof(RadioVolume));
            OnPropertyChanged(nameof(RadioVolumePercentText));
            OnPropertyChanged(nameof(RadioVolumeIcon));
        }
    }

    public bool IsRadioMuted
    {
        get => _radioService.IsMuted;
        set
        {
            _radioService.IsMuted = value;
            OnPropertyChanged(nameof(IsRadioMuted));
            OnPropertyChanged(nameof(RadioVolumePercentText));
            OnPropertyChanged(nameof(RadioVolumeIcon));
        }
    }

    public string RadioVolumePercentText => IsRadioMuted ? "Mute" : $"{Math.Round(RadioVolume * 100)}%";

    public string RadioVolumeIcon => IsRadioMuted || RadioVolume <= 0.01 ? "🔇" : (RadioVolume < 0.5 ? "🔉" : "🔊");

    public string RadioCategoryFilter
    {
        get => _radioCategoryFilter;
        set
        {
            if (SetProperty(ref _radioCategoryFilter, value))
            {
                _filteredRadioStations?.Refresh();
            }
        }
    }

    public string RadioSearchQuery
    {
        get => _radioSearchQuery;
        set
        {
            if (SetProperty(ref _radioSearchQuery, value))
            {
                _filteredRadioStations?.Refresh();
            }
        }
    }

    public bool IsSearchingOnlineRadio
    {
        get => _isSearchingOnlineRadio;
        set
        {
            if (SetProperty(ref _isSearchingOnlineRadio, value))
            {
                OnPropertyChanged(nameof(SearchOnlineRadioButtonText));
            }
        }
    }

    public string SearchOnlineRadioButtonText => IsSearchingOnlineRadio ? "Mencari..." : "🌐 Cari Online";

    public bool IsAddingCustomRadio
    {
        get => _isAddingCustomRadio;
        set => SetProperty(ref _isAddingCustomRadio, value);
    }

    public string CustomRadioName
    {
        get => _customRadioName;
        set => SetProperty(ref _customRadioName, value);
    }

    public string CustomRadioUrl
    {
        get => _customRadioUrl;
        set => SetProperty(ref _customRadioUrl, value);
    }

    public ICommand OpenRadioCommand { get; }

    public ICommand ToggleRadioWindowCommand { get; }

    public ICommand CloseRadioWindowCommand { get; }

    public ICommand MinimizeRadioWindowCommand { get; }

    public ICommand ExpandRadioWindowCommand { get; }

    public ICommand ToggleRadioPlayPauseCommand { get; }

    public ICommand StopRadioCommand { get; }

    public ICommand NextRadioStationCommand { get; }

    public ICommand PreviousRadioStationCommand { get; }

    public ICommand PlayRadioStationCommand { get; }

    public ICommand SetRadioCategoryCommand { get; }

    public ICommand ToggleCurrentRadioFavoriteCommand { get; }

    public ICommand ToggleStationFavoriteCommand { get; }

    public ICommand ToggleRadioMuteCommand { get; }

    public ICommand SearchOnlineRadioCommand { get; }

    public ICommand ToggleAddCustomRadioCommand { get; }

    public ICommand SaveCustomRadioCommand { get; }

    public ICommand DeleteCustomRadioStationCommand { get; }

    private void OnRadioStateChanged()
    {
        Action notify = () =>
        {
            if (IsRadioPlaying || IsRadioBuffering) StopQuranForOtherPlayback();
            OnPropertyChanged(nameof(CurrentRadioStation));
            OnPropertyChanged(nameof(CurrentRadioStationName));
            OnPropertyChanged(nameof(CurrentRadioStationSubtitle));
            OnPropertyChanged(nameof(CurrentRadioFavoriteSymbol));
            OnPropertyChanged(nameof(IsRadioPlaying));
            OnPropertyChanged(nameof(IsAudioActive));
            OnPropertyChanged(nameof(IsRadioBuffering));
            OnPropertyChanged(nameof(IsRadioActiveOrOpen));
            OnPropertyChanged(nameof(IsNotchActive));
            OnPropertyChanged(nameof(RadioStateBadgeText));
            OnPropertyChanged(nameof(RadioStatusMessage));
            OnPropertyChanged(nameof(RadioCompactLabel));
            OnPropertyChanged(nameof(RadioNotchTooltip));
            OnPropertyChanged(nameof(RadioStatusSummary));
            OnPropertyChanged(nameof(RadioVolume));
            OnPropertyChanged(nameof(IsRadioMuted));
            OnPropertyChanged(nameof(RadioVolumePercentText));
            OnPropertyChanged(nameof(RadioVolumeIcon));
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

    private bool FilterRadioStation(object obj)
    {
        if (obj is not RadioStation st) return false;

        if (!string.IsNullOrWhiteSpace(_radioCategoryFilter) && !string.Equals(_radioCategoryFilter, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(_radioCategoryFilter, "Favorites", StringComparison.OrdinalIgnoreCase))
            {
                if (!st.IsFavorite) return false;
            }
            else if (!string.Equals(st.Category, _radioCategoryFilter, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(_radioSearchQuery))
        {
            var q = _radioSearchQuery.Trim();
            bool match = st.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                      || st.CityOrCountry.Contains(q, StringComparison.OrdinalIgnoreCase)
                      || st.Genre.Contains(q, StringComparison.OrdinalIgnoreCase)
                      || st.Category.Contains(q, StringComparison.OrdinalIgnoreCase);
            if (!match) return false;
        }

        return true;
    }

    private async Task ExecuteOnlineRadioSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(RadioSearchQuery)) return;

        _radioSearchCts?.Cancel();
        _radioSearchCts?.Dispose();
        _radioSearchCts = new CancellationTokenSource();
        var token = _radioSearchCts.Token;

        IsSearchingOnlineRadio = true;
        try
        {
            var results = await _radioService.SearchOnlineStationsAsync(RadioSearchQuery, token);
            if (!token.IsCancellationRequested && results.Count > 0)
            {
                RadioCategoryFilter = "All";
                foreach (var r in results)
                {
                    if (!RadioStations.Any(existing => string.Equals(existing.Id, r.Id, StringComparison.OrdinalIgnoreCase) ||
                                                       string.Equals(existing.StreamUrl, r.StreamUrl, StringComparison.OrdinalIgnoreCase)))
                    {
                        RadioStations.Add(r);
                    }
                }
                _filteredRadioStations?.Refresh();
            }
        }
        catch (OperationCanceledException) { }
        catch { }
        finally
        {
            IsSearchingOnlineRadio = false;
        }
    }

    private void SaveCustomRadioStation()
    {
        if (string.IsNullOrWhiteSpace(CustomRadioName) || string.IsNullOrWhiteSpace(CustomRadioUrl))
            return;

        var url = CustomRadioUrl.Trim();
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            url = "https://" + url;
        }

        var st = _radioService.AddCustomStation(CustomRadioName, url);
        if (!RadioStations.Contains(st))
        {
            RadioStations.Add(st);
        }

        CustomRadioName = string.Empty;
        CustomRadioUrl = string.Empty;
        IsAddingCustomRadio = false;
        RadioCategoryFilter = "All";
        _filteredRadioStations?.Refresh();
        _radioService.PlayStation(st);
    }
}
