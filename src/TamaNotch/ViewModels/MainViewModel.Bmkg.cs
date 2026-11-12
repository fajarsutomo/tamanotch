using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using TamaNotch.Models;
using TamaNotch.Services.Bmkg;
using TamaNotch.Services.Location;

namespace TamaNotch.ViewModels;

public partial class MainViewModel
{
    private readonly IBmkgService _bmkgService;

    private DispatcherTimer? _notificationDismissTimer;

    // BMKG Cuaca & Gempa Bumi Fields (Komponen ke-7)
    private BmkgNotificationEvent? _activeBmkgNotification;

    private DispatcherTimer? _bmkgNotificationDismissTimer;

    private string _bmkgActiveTab = "Weather"; // "Weather", "Earthquake"

    private string _bmkgEarthquakeSubTab = "AutoGempa"; // "AutoGempa", "Terkini", "Dirasakan"

    private bool _isShowingShakemap = true;

    private bool _isEditingCustomAdm4;

    private string _bmkgLocationStatus = string.Empty;

    private bool _isSelectingPreciseRegion;

    private bool _isSyncingRegionSelectors;

    private BmkgRegionItem? _selectedBmkgProvince;

    private BmkgRegionItem? _selectedBmkgRegency;

    private BmkgRegionItem? _selectedBmkgDistrict;

    private BmkgRegionItem? _selectedBmkgVillage;

    private string _bmkgLocationSearchQuery = string.Empty;

    private bool _isSearchingBmkgLocation;

    private CancellationTokenSource? _bmkgLocationSearchCts;

    public event Action? RequestOpenBmkg;

    public bool ShowHoverWeatherPill => IsCompactMode && ShowComponentIcons && Settings.EnableBmkg;

    // ================= BMKG CUACA & GEMPA BUMI PROPERTIES (KOMPONEN KE-7) =================
    public IBmkgService BmkgService => _bmkgService;

    public List<CityLocation> BmkgCities { get; } = CityLocation.GetIndonesianCities();

    public bool EnableBmkg
    {
        get => Settings.EnableBmkg;
        set
        {
            if (Settings.EnableBmkg != value)
            {
                Settings.EnableBmkg = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(EnableBmkg));
                OnPropertyChanged(nameof(ShowHoverWeatherPill));
                OnPropertyChanged(nameof(HasActiveBmkgNotification));
                if (value)
                {
                    _bmkgService.Start();
                    if (Settings.ShowAirQualityInIsland) _airQualityService.Start();
                }
                else
                {
                    _bmkgService.Stop();
                    _airQualityService.Stop();
                    ActiveBmkgNotification = null;
                }
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool ShowWeatherInCompactNotch
    {
        get => Settings.ShowWeatherInCompactNotch;
        set
        {
            if (Settings.ShowWeatherInCompactNotch != value)
            {
                Settings.ShowWeatherInCompactNotch = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(ShowWeatherInCompactNotch));
            }
        }
    }

    public bool IsBmkgWindowOpen
    {
        get => Settings.IsBmkgWindowOpen;
        set
        {
            if (Settings.IsBmkgWindowOpen != value)
            {
                Settings.IsBmkgWindowOpen = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsBmkgWindowOpen));
                OnPropertyChanged(nameof(IsNotchActive));
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool IsBmkgWindowCompact
    {
        get => Settings.IsBmkgWindowCompact;
        set
        {
            if (Settings.IsBmkgWindowCompact != value)
            {
                Settings.IsBmkgWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsBmkgWindowCompact));
                OnPropertyChanged(nameof(IsBmkgWindowExpanded));
            }
        }
    }

    public bool IsBmkgWindowExpanded => !IsBmkgWindowCompact;

    public string BmkgActiveTab
    {
        get => _bmkgActiveTab;
        set
        {
            if (SetProperty(ref _bmkgActiveTab, value ?? "Weather"))
            {
                OnPropertyChanged(nameof(IsBmkgWeatherTab));
                OnPropertyChanged(nameof(IsBmkgEarthquakeTab));
            }
        }
    }

    public bool IsBmkgWeatherTab => string.Equals(_bmkgActiveTab, "Weather", StringComparison.OrdinalIgnoreCase);

    public bool IsBmkgEarthquakeTab => string.Equals(_bmkgActiveTab, "Earthquake", StringComparison.OrdinalIgnoreCase);

    public string BmkgEarthquakeSubTab
    {
        get => _bmkgEarthquakeSubTab;
        set
        {
            if (SetProperty(ref _bmkgEarthquakeSubTab, value ?? "AutoGempa"))
            {
                OnPropertyChanged(nameof(IsBmkgEqAutoTab));
                OnPropertyChanged(nameof(IsBmkgEqTerkiniTab));
                OnPropertyChanged(nameof(IsBmkgEqDirasakanTab));
            }
        }
    }

    public bool IsBmkgEqAutoTab => string.Equals(_bmkgEarthquakeSubTab, "AutoGempa", StringComparison.OrdinalIgnoreCase);

    public bool IsBmkgEqTerkiniTab => string.Equals(_bmkgEarthquakeSubTab, "Terkini", StringComparison.OrdinalIgnoreCase);

    public bool IsBmkgEqDirasakanTab => string.Equals(_bmkgEarthquakeSubTab, "Dirasakan", StringComparison.OrdinalIgnoreCase);

    public bool IsShowingShakemap
    {
        get => _isShowingShakemap;
        set => SetProperty(ref _isShowingShakemap, value);
    }

    public bool IsEditingCustomAdm4
    {
        get => _isEditingCustomAdm4;
        set => SetProperty(ref _isEditingCustomAdm4, value);
    }

    public bool IsSelectingPreciseRegion
    {
        get => _isSelectingPreciseRegion;
        set
        {
            if (SetProperty(ref _isSelectingPreciseRegion, value) && value)
            {
                _ = SyncRegionSelectorsFromCurrentAdm4Async();
            }
        }
    }

    public IReadOnlyList<BmkgRegionItem> BmkgProvinces => _bmkgService.GetProvinces();

    public ObservableCollection<BmkgRegionItem> BmkgRegencies { get; } = new();

    public ObservableCollection<BmkgRegionItem> BmkgDistricts { get; } = new();

    public ObservableCollection<BmkgRegionItem> BmkgVillages { get; } = new();

    public ObservableCollection<BmkgLocationSearchResult> BmkgLocationSearchResults { get; } = new();

    public bool HasBmkgLocationSearchResults => BmkgLocationSearchResults.Count > 0;

    public string BmkgLocationSearchQuery
    {
        get => _bmkgLocationSearchQuery;
        set => SetProperty(ref _bmkgLocationSearchQuery, value ?? string.Empty);
    }

    public bool IsSearchingBmkgLocation
    {
        get => _isSearchingBmkgLocation;
        set
        {
            if (SetProperty(ref _isSearchingBmkgLocation, value))
            {
                OnPropertyChanged(nameof(BmkgLocationSearchButtonText));
            }
        }
    }

    public string BmkgLocationSearchButtonText => IsSearchingBmkgLocation ? "Mencari..." : "Cari";

    public BmkgRegionItem? SelectedBmkgProvince
    {
        get => _selectedBmkgProvince;
        set
        {
            if (SetProperty(ref _selectedBmkgProvince, value) && !_isSyncingRegionSelectors && value != null)
            {
                _ = OnSelectedProvinceChangedAsync(value.Code);
            }
        }
    }

    public BmkgRegionItem? SelectedBmkgRegency
    {
        get => _selectedBmkgRegency;
        set
        {
            if (SetProperty(ref _selectedBmkgRegency, value) && !_isSyncingRegionSelectors && value != null)
            {
                _ = OnSelectedRegencyChangedAsync(value.Code);
            }
        }
    }

    public BmkgRegionItem? SelectedBmkgDistrict
    {
        get => _selectedBmkgDistrict;
        set
        {
            if (SetProperty(ref _selectedBmkgDistrict, value) && !_isSyncingRegionSelectors && value != null)
            {
                _ = OnSelectedDistrictChangedAsync(value.Code);
            }
        }
    }

    public BmkgRegionItem? SelectedBmkgVillage
    {
        get => _selectedBmkgVillage;
        set
        {
            if (SetProperty(ref _selectedBmkgVillage, value) && !_isSyncingRegionSelectors && value != null)
            {
                ApplyPreciseVillageSelection(value);
            }
        }
    }

    public string BmkgLocationStatus
    {
        get => _bmkgLocationStatus;
        set => SetProperty(ref _bmkgLocationStatus, value);
    }

    public bool BmkgFollowPrayerCity
    {
        get => Settings.BmkgFollowPrayerCity;
        set
        {
            if (Settings.BmkgFollowPrayerCity != value)
            {
                Settings.BmkgFollowPrayerCity = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(BmkgFollowPrayerCity));
                OnPropertyChanged(nameof(SelectedBmkgCity));
                OnPropertyChanged(nameof(BmkgWeatherCityText));
                _ = _bmkgService.RefreshWeatherAsync();
            }
        }
    }

    public CityLocation? SelectedBmkgCity
    {
        get
        {
            string target = Settings.BmkgFollowPrayerCity ? Settings.PrayerCity : Settings.BmkgSelectedCity;
            return BmkgCities.FirstOrDefault(c => c.Name.Equals(target, StringComparison.OrdinalIgnoreCase))
                   ?? CityLocation.FindBestMatchOrNearest(target, Settings.PrayerLatitude, Settings.PrayerLongitude);
        }
        set
        {
            if (value != null)
            {
                _locationService.ApplyCity(value, LocationOrigin.Bmkg);
                BmkgLocationStatus = $"✓ Kota dipilih: {value.DisplayName} (Anda dapat memilih Kecamatan/Kelurahan lebih spesifik di bawah)";
                _ = SyncRegionSelectorsFromCurrentAdm4Async();
            }
        }
    }

    public string BmkgCustomAdm4Code
    {
        get => Settings.BmkgCustomAdm4Code;
        set
        {
            if (Settings.BmkgCustomAdm4Code != value)
            {
                Settings.BmkgCustomAdm4Code = value?.Trim() ?? string.Empty;
                _settingsService.Save();
                OnPropertyChanged(nameof(BmkgCustomAdm4Code));
                OnPropertyChanged(nameof(BmkgActiveAdm4Text));
            }
        }
    }

    public string BmkgActiveAdm4Text => $"Kode ADM4: {_bmkgService.GetActiveAdm4Code()}";

    public BmkgWeatherData? CurrentBmkgWeather => _bmkgService.CurrentWeatherData;

    public BmkgWeatherForecastItem? CurrentWeatherSlot => _bmkgService.CurrentWeatherData?.CurrentWeather;

    public List<BmkgWeatherDayGroup> BmkgWeatherDayGroups => _bmkgService.CurrentWeatherData?.DayGroups ?? new();

    public string BmkgWeatherEmoji => CurrentWeatherSlot?.WeatherEmoji ?? "🌤";

    public string BmkgWeatherIconGeometry => CurrentWeatherSlot?.WeatherIconGeometry ?? BmkgWeatherForecastItem.PartlyCloudyGeometry;

    public string BmkgWeatherIconColor => CurrentWeatherSlot?.WeatherIconColor ?? "#FFD43B";

    public string BmkgWeatherTempText => CurrentWeatherSlot != null ? $"{CurrentWeatherSlot.TemperatureC}°C" : "--°C";

    public string BmkgWeatherDescText => CurrentWeatherSlot?.WeatherDesc ?? "Memuat cuaca...";

    public string BmkgWeatherCityText => _bmkgService.CurrentWeatherData?.CompactCityLabel ?? _bmkgService.GetActiveCityName();

    public string BmkgWeatherFullCityText => _bmkgService.CurrentWeatherData?.ShortCityLabel ?? _bmkgService.GetActiveCityName();

    public string BmkgWeatherLocationSubtitle => _bmkgService.CurrentWeatherData?.FullLocationSubtitle ?? "Indonesia • BMKG";

    public string BmkgWeatherRangeText => _bmkgService.CurrentWeatherData?.TodayTempRangeFormatted ?? "-";

    public string BmkgWeatherHumidityText => CurrentWeatherSlot?.HumidityFormatted ?? "--%";

    public string BmkgWeatherWindText => CurrentWeatherSlot != null ? $"{CurrentWeatherSlot.WindSpeedFormatted} ({CurrentWeatherSlot.WindDirectionLabel})" : "-";

    public string BmkgWeatherCloudText => CurrentWeatherSlot?.CloudCoverFormatted ?? "--%";

    public string BmkgWeatherRainText => CurrentWeatherSlot?.PrecipitationFormatted ?? "0 mm";

    public BmkgEarthquakeItem? LatestEarthquake => _bmkgService.LatestEarthquake;

    public IReadOnlyList<BmkgEarthquakeItem> RecentEarthquakes => _bmkgService.RecentEarthquakes;

    public IReadOnlyList<BmkgEarthquakeItem> FeltEarthquakes => _bmkgService.FeltEarthquakes;

    public bool HasLatestEarthquake => _bmkgService.LatestEarthquake != null;

    /// <summary>Lencana gempa terbaru di island hanya tampil sampai batas ini; setelahnya hanya ada di jendela Cuaca &amp; Gempa.</summary>
    public static readonly TimeSpan IslandEarthquakeMaxAge = TimeSpan.FromHours(3);

    private DispatcherTimer? _islandEarthquakeExpiryTimer;

    public bool ShowIslandEarthquakeBadge => IsRecentEarthquake(LatestEarthquake, DateTimeOffset.UtcNow);

    public static bool IsRecentEarthquake(BmkgEarthquakeItem? eq, DateTimeOffset utcNow)
        => eq != null && (!eq.EventDateTime.HasValue || utcNow - eq.EventDateTime.Value.ToUniversalTime() <= IslandEarthquakeMaxAge);

    // Lencana perlu hilang tepat saat melewati batas walau data BMKG belum disegarkan.
    private void ScheduleIslandEarthquakeExpiry()
    {
        _islandEarthquakeExpiryTimer?.Stop();
        _islandEarthquakeExpiryTimer = null;

        var time = LatestEarthquake?.EventDateTime;
        if (!time.HasValue) return;

        var remaining = time.Value.ToUniversalTime() + IslandEarthquakeMaxAge - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return;

        // DispatcherTimer.Interval dibatasi ~24 hari; batas 3 jam jauh di bawahnya.
        _islandEarthquakeExpiryTimer = new DispatcherTimer { Interval = remaining + TimeSpan.FromSeconds(1) };
        _islandEarthquakeExpiryTimer.Tick += (s, e) =>
        {
            _islandEarthquakeExpiryTimer?.Stop();
            OnPropertyChanged(nameof(ShowIslandEarthquakeBadge));
        };
        _islandEarthquakeExpiryTimer.Start();
    }

    public string LatestEarthquakeBadge => LatestEarthquake != null ? $"M {LatestEarthquake.Magnitude}" : "Gempa";

    public string LatestEarthquakeCompactText => LatestEarthquake != null
        ? $"M {LatestEarthquake.Magnitude} • {LatestEarthquake.Wilayah}"
        : "Belum ada data gempa terkini";

    public string BmkgStatusMessage => _bmkgService.StatusMessage;

    public bool IsBmkgLoading => _bmkgService.IsLoadingWeather || _bmkgService.IsLoadingEarthquake;

    public string BmkgNotchTooltip
    {
        get
        {
            var weatherLine = CurrentWeatherSlot != null
                ? $"🌤️ Cuaca BMKG ({BmkgWeatherCityText}): {BmkgWeatherTempText} • {BmkgWeatherDescText} (Kelembapan {BmkgWeatherHumidityText})"
                : $"🌤️ Cuaca BMKG ({BmkgWeatherCityText}): Memuat...";

            var eqLine = LatestEarthquake != null
                ? $"🌋 Gempa Terbaru: M {LatestEarthquake.Magnitude} • {LatestEarthquake.Wilayah} ({LatestEarthquake.Jam}, Kedalaman {LatestEarthquake.Kedalaman})"
                : "🌋 Gempa Bumi BMKG: Siap memantau...";

            var aqLine = ShowAirQualityBadge ? $"{AirQualityTooltip.Split('\n')[0]}\n" : string.Empty;
            return $"{weatherLine}\n{aqLine}{eqLine}\nKlik untuk membuka jendela Cuaca & Gempa Bumi BMKG";
        }
    }

    public bool HasActiveBmkgNotification => EnableBmkg && _activeBmkgNotification != null;

    // --- Notch notifikasi: peringatan berat (gempa M >= 5 / tsunami, cuaca ekstrem) menggantikan mata idle ---
    private static readonly System.Windows.Media.Color AlertRed = System.Windows.Media.Color.FromRgb(0xFF, 0x3B, 0x4E);
    private static readonly System.Windows.Media.Color AlertOrange = System.Windows.Media.Color.FromRgb(0xFF, 0x9F, 0x0A);
    private static readonly System.Windows.Media.Color AlertYellow = System.Windows.Media.Color.FromRgb(0xFF, 0xD4, 0x3B);

    private static System.Windows.Media.Color AlertColorFor(BmkgAlertLevel level) => level switch
    {
        BmkgAlertLevel.Orange => AlertOrange,
        BmkgAlertLevel.Yellow => AlertYellow,
        _ => AlertRed
    };

    private BmkgAlertLevel ActiveAlertLevel => _activeBmkgNotification?.Level ?? BmkgAlertLevel.Red;

    /// <summary>Peringatan aktif yang cukup berat untuk tampil sebagai notch notifikasi (bukan banner).</summary>
    public bool IsAlertNotchNotification => HasActiveBmkgNotification;

    /// <summary>Notch idle berubah jadi satu baris pesan berframe merah; saat hover atau mode lain banner penuh dipakai.</summary>
    public bool ShowAlertNotch => IsAlertNotchNotification && IsCompactMode && ShowIdleEyes;

    // Notifikasi yang sudah tampil sebagai notch tidak diulang di island (banner). Yang belum sempat tampil di notch
    // (mis. tiba saat island sedang terbuka karena hover) tetap memakai banner.
    private BmkgNotificationEvent? _bmkgShownInNotch;
    private PrayerNotificationEvent? _prayerShownInNotch;
    private WellnessNotificationEvent? _wellnessShownInNotch;

    private bool BmkgShownInNotch => _bmkgShownInNotch != null && ReferenceEquals(_bmkgShownInNotch, _activeBmkgNotification);

    private bool PrayerShownInNotch => _prayerShownInNotch != null && ReferenceEquals(_prayerShownInNotch, _activePrayerNotification);

    private bool WellnessShownInNotch => _wellnessShownInNotch != null && ReferenceEquals(_wellnessShownInNotch, _activeWellnessNotification);

    private void TrackNotchDisplay()
    {
        if (ShowAlertNotch) _bmkgShownInNotch = _activeBmkgNotification;
        if (ShowPrayerNotch)
        {
            // Mulai hitung tampil 20 detik tepat saat notifikasi sholat pertama kali muncul di notch.
            if (!PrayerShownInNotch) StartPrayerDismissTimer(PrayerShownDuration);
            _prayerShownInNotch = _activePrayerNotification;
        }
        if (ShowWellnessNotch) _wellnessShownInNotch = _activeWellnessNotification;
    }

    public bool ShowBmkgBanner => HasActiveBmkgNotification && !ShowAlertNotch && !BmkgShownInNotch;

    /// <summary>Ada banner notifikasi yang sedang tampil di island; island tidak boleh menciut selama itu.</summary>
    public bool HasVisibleIslandBanner => ShowBmkgBanner || ShowWellnessBanner;

    public bool ShowEyesInIdle => ShowIdleEyes && !ShowAlertNotch && !ShowPrayerNotch && !ShowWellnessNotch;

    public string AlertNotchText
    {
        get
        {
            var n = _activeBmkgNotification;
            if (n == null) return string.Empty;
            return n.IsEarthquake ? $"⚠ {n.MagnitudeBadge} • {n.Message}" : $"{n.Title} • {n.Message}";
        }
    }

    // NaN = lebar mengikuti panjang pesan (tinggi tetap mengikuti frame idle).
    public double AlertNotchWidth => double.NaN;

    public double AlertNotchMaxTextWidth => Math.Max(240, System.Windows.SystemParameters.PrimaryScreenWidth * 0.6);

    private static readonly Dictionary<BmkgAlertLevel, System.Windows.Media.Brush> AlertBrushes = new()
    {
        [BmkgAlertLevel.Red] = TamaNotch.Helpers.NeonPalette.Freeze(new System.Windows.Media.SolidColorBrush(AlertRed)),
        [BmkgAlertLevel.Orange] = TamaNotch.Helpers.NeonPalette.Freeze(new System.Windows.Media.SolidColorBrush(AlertOrange)),
        [BmkgAlertLevel.Yellow] = TamaNotch.Helpers.NeonPalette.Freeze(new System.Windows.Media.SolidColorBrush(AlertYellow))
    };

    private static readonly Dictionary<BmkgAlertLevel, System.Windows.Media.Effects.Effect> AlertGlows = new()
    {
        [BmkgAlertLevel.Red] = CreateAlertGlow(AlertRed),
        [BmkgAlertLevel.Orange] = CreateAlertGlow(AlertOrange),
        [BmkgAlertLevel.Yellow] = CreateAlertGlow(AlertYellow)
    };

    /// <summary>Bingkai notch notifikasi BMKG: kuning (gempa M &lt; 3), oranye (M 3 sampai &lt; 5), merah (M &gt;= 5, tsunami, cuaca ekstrem).</summary>
    public System.Windows.Media.Brush AlertFrameBrush => AlertBrushes[ActiveAlertLevel];

    public System.Windows.Media.Effects.Effect AlertGlowEffect => AlertGlows[ActiveAlertLevel];

    private static System.Windows.Media.Effects.Effect CreateAlertGlow(System.Windows.Media.Color color)
    {
        var effect = new System.Windows.Media.Effects.DropShadowEffect { Color = color, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.9 };
        effect.Freeze();
        return effect;
    }

    protected override void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(ShowComponentIcons))
        {
            if (!ShowComponentIcons && _embeddedPanel != null) EmbeddedPanel = null;
            base.OnPropertyChanged(nameof(ShowEmbeddedPanel));
            base.OnPropertyChanged(nameof(ShowNotesBadge));
            if (_systemMonitor is not null)
            {
                base.OnPropertyChanged(nameof(ShowMonitorBadge));
                base.OnPropertyChanged(nameof(ShowUptimePill));
                UpdateMonitorRunning();
                UpdateUptimeTimer();
            }
        }
        if (propertyName is nameof(ShowComponentIcons) or nameof(ShowIdleEyes) or nameof(ShowCompactView)
            or nameof(HasActiveBmkgNotification) or nameof(HasActivePrayerNotification)
            or nameof(HasActiveWellnessNotification))
        {
            TrackNotchDisplay();
            base.OnPropertyChanged(nameof(ShowWellnessNotch));
            base.OnPropertyChanged(nameof(ShowWellnessBanner));
            base.OnPropertyChanged(nameof(WellnessNotchText));
            base.OnPropertyChanged(nameof(HasVisibleIslandBanner));
            base.OnPropertyChanged(nameof(IsAlertNotchNotification));
            base.OnPropertyChanged(nameof(ShowAlertNotch));
            base.OnPropertyChanged(nameof(ShowBmkgBanner));
            base.OnPropertyChanged(nameof(ShowEyesInIdle));
            base.OnPropertyChanged(nameof(ShowDropShape));
            base.OnPropertyChanged(nameof(DropFrameBrush));
            base.OnPropertyChanged(nameof(DropGlowEffect));
            base.OnPropertyChanged(nameof(AlertNotchText));
            base.OnPropertyChanged(nameof(AlertFrameBrush));
            base.OnPropertyChanged(nameof(AlertGlowEffect));
            base.OnPropertyChanged(nameof(ShowPrayerNotch));
            base.OnPropertyChanged(nameof(PrayerNotchText));
        }
    }

    public BmkgNotificationEvent? ActiveBmkgNotification
    {
        get => _activeBmkgNotification;
        private set
        {
            if (SetProperty(ref _activeBmkgNotification, value))
            {
                OnPropertyChanged(nameof(HasActiveBmkgNotification));
                // Notifikasi menahan island terbuka saat disorot; setelah hilang, kembali ke idle bila kursor sudah pergi.
                if (value == null) ScheduleHoverCollapse();
            }
        }
    }

    // BMKG Cuaca & Gempa Bumi Commands
    public ICommand OpenBmkgCommand { get; }

    public ICommand OpenBmkgWeatherTabCommand { get; }

    public ICommand OpenBmkgEarthquakeTabCommand { get; }

    public ICommand ToggleBmkgWindowCommand { get; }

    public ICommand CloseBmkgWindowCommand { get; }

    public ICommand MinimizeBmkgWindowCommand { get; }

    public ICommand ExpandBmkgWindowCommand { get; }

    public ICommand SetBmkgTabCommand { get; }

    public ICommand SetBmkgEarthquakeSubTabCommand { get; }

    public ICommand RefreshBmkgCommand { get; }

    public ICommand DetectBmkgLocationCommand { get; }

    public ICommand ToggleShakemapCommand { get; }

    public ICommand OpenEarthquakeMapCommand { get; }

    public ICommand OpenShakemapBrowserCommand { get; }

    public ICommand OpenBmkgWebsiteCommand { get; }

    public ICommand ToggleCustomAdm4EditCommand { get; }

    public ICommand ApplyCustomAdm4Command { get; }

    public ICommand TogglePreciseRegionSelectorCommand { get; }

    public ICommand SearchBmkgLocationCommand { get; }

    public ICommand SelectBmkgSearchResultCommand { get; }

    public ICommand ClearBmkgLocationSearchCommand { get; }

    public ICommand TestBmkgAlertCommand { get; }

    public ICommand DismissBmkgNotificationCommand { get; }

    public ICommand OpenBmkgNotificationDetailCommand { get; }

    // Koordinat cuaca hanya dipakai menyelaraskan sholat bila datanya untuk lokasi aktif saat ini dan
    // lokasi aktif bukan pilihan tingkat kota (yang koordinatnya sudah tepat dari pemilih kota).
    private bool ShouldAdoptWeatherCoordinates(BmkgWeatherData w)
    {
        if (!Settings.BmkgFollowPrayerCity) return false;
        if (Math.Abs(w.Latitude) <= 0.001 || Math.Abs(w.Longitude) <= 0.001) return false;
        if (!string.Equals(w.Adm4Code, _bmkgService.GetActiveAdm4Code(), StringComparison.Ordinal)) return false;
        return !CityLocation.GetIndonesianCities().Any(c => c.BmkgAdm4Code == w.Adm4Code);
    }

    private void OnBmkgDataUpdated()
    {
        void Notify()
        {
            var w = _bmkgService.CurrentWeatherData;
            if (w != null && ShouldAdoptWeatherCoordinates(w))
            {
                bool coordChanged = Math.Abs(Settings.PrayerLatitude - w.Latitude) > 0.002 ||
                                    Math.Abs(Settings.PrayerLongitude - w.Longitude) > 0.002;
                if (coordChanged)
                {
                    _prayerService.UpdateLocation(w.ShortCityLabel, w.Latitude, w.Longitude, w.TimezoneOffsetHours);
                    _prayerReminderService.RefreshSchedule();
                    OnPropertyChanged(nameof(CurrentPrayerSchedule));
                    OnPropertyChanged(nameof(NextPrayerName));
                    OnPropertyChanged(nameof(NextPrayerTimeFormatted));
                    OnPropertyChanged(nameof(NextPrayerCountdownBadge));
                    OnPropertyChanged(nameof(PrayerScheduleTooltip));
                }
            }

            OnPropertyChanged(nameof(CurrentBmkgWeather));
            OnPropertyChanged(nameof(CurrentWeatherSlot));
            OnPropertyChanged(nameof(BmkgWeatherDayGroups));
            OnPropertyChanged(nameof(BmkgWeatherEmoji));
            OnPropertyChanged(nameof(BmkgWeatherIconGeometry));
            OnPropertyChanged(nameof(BmkgWeatherIconColor));
            OnPropertyChanged(nameof(BmkgWeatherTempText));
            OnPropertyChanged(nameof(BmkgWeatherDescText));
            OnPropertyChanged(nameof(BmkgWeatherCityText));
            OnPropertyChanged(nameof(BmkgWeatherFullCityText));
            OnPropertyChanged(nameof(BmkgWeatherLocationSubtitle));
            OnPropertyChanged(nameof(BmkgWeatherRangeText));
            OnPropertyChanged(nameof(BmkgWeatherHumidityText));
            OnPropertyChanged(nameof(BmkgWeatherWindText));
            OnPropertyChanged(nameof(BmkgWeatherCloudText));
            OnPropertyChanged(nameof(BmkgWeatherRainText));
            OnPropertyChanged(nameof(LatestEarthquake));
            OnPropertyChanged(nameof(RecentEarthquakes));
            OnPropertyChanged(nameof(FeltEarthquakes));
            OnPropertyChanged(nameof(HasLatestEarthquake));
            OnPropertyChanged(nameof(ShowIslandEarthquakeBadge));
            ScheduleIslandEarthquakeExpiry();
            OnPropertyChanged(nameof(LatestEarthquakeBadge));
            OnPropertyChanged(nameof(LatestEarthquakeCompactText));
            OnPropertyChanged(nameof(BmkgStatusMessage));
            OnPropertyChanged(nameof(IsBmkgLoading));
            OnPropertyChanged(nameof(BmkgNotchTooltip));
            OnPropertyChanged(nameof(BmkgActiveAdm4Text));
            OnPropertyChanged(nameof(ShowHoverWeatherPill));
        }

        if (System.Windows.Application.Current?.Dispatcher != null &&
            !System.Windows.Application.Current.Dispatcher.CheckAccess())
        {
            System.Windows.Application.Current.Dispatcher.BeginInvoke(Notify);
        }
        else
        {
            Notify();
        }
    }

    private void OnBmkgNotificationTriggered(BmkgNotificationEvent evt)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() => ShowBmkgNotification(evt));
    }

    /// <summary>Tampilkan peringatan BMKG di island/notch (tanpa notifikasi tray Windows) lalu tutup otomatis.</summary>
    public void ShowBmkgNotification(BmkgNotificationEvent evt)
    {
        ActiveBmkgNotification = evt;

        _bmkgNotificationDismissTimer?.Stop();
        _bmkgNotificationDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(22) };
        _bmkgNotificationDismissTimer.Tick += (s, e) =>
        {
            _bmkgNotificationDismissTimer?.Stop();
            ActiveBmkgNotification = null;
        };
        _bmkgNotificationDismissTimer.Start();
    }

    private async Task DetectBmkgLocationAsync()
    {
        BmkgLocationStatus = "Mendeteksi lokasi presisi...";
        try
        {
            var loc = await _prayerService.DetectLocationAsync();
            if (loc != null)
            {
                var precise = await _bmkgService.ResolveCoordinatesToPreciseAdm4Async(loc.Latitude, loc.Longitude, loc.Name);
                if (precise != null && !string.IsNullOrWhiteSpace(precise.Adm4Code))
                {
                    await ApplyBmkgSearchResultAsync(precise);
                    BmkgLocationStatus = $"✓ Terdeteksi presisi: {precise.FullLabel} ({precise.Adm4Code})";
                    return;
                }

                var nearest = CityLocation.FindBestMatchOrNearest(loc.Name, loc.Latitude, loc.Longitude);
                _locationService.ApplyCity(new CityLocation
                {
                    Name = nearest.Name,
                    Province = nearest.Province,
                    Latitude = loc.Latitude,
                    Longitude = loc.Longitude,
                    Timezone = nearest.Timezone,
                    BmkgAdm4Code = nearest.BmkgAdm4Code
                }, LocationOrigin.Bmkg);

                BmkgLocationStatus = $"✓ Lokasi: {nearest.DisplayName}";
                _ = SyncRegionSelectorsFromCurrentAdm4Async();
            }
            else
            {
                BmkgLocationStatus = "Gagal mendeteksi lokasi otomatis. Gunakan pencarian atau pilih wilayah manual.";
            }
        }
        catch
        {
            BmkgLocationStatus = "Gagal mendeteksi lokasi otomatis.";
        }
    }

    private async Task ExecuteBmkgPreciseLocationSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(BmkgLocationSearchQuery))
        {
            BmkgLocationStatus = "Ketik nama Kelurahan, Kecamatan, atau Kota terlebih dahulu.";
            return;
        }

        _bmkgLocationSearchCts?.Cancel();
        _bmkgLocationSearchCts?.Dispose();
        _bmkgLocationSearchCts = new CancellationTokenSource();
        var token = _bmkgLocationSearchCts.Token;

        IsSearchingBmkgLocation = true;
        BmkgLocationStatus = $"Mencari wilayah '{BmkgLocationSearchQuery.Trim()}'...";

        try
        {
            var results = await _bmkgService.SearchPreciseLocationsAsync(BmkgLocationSearchQuery, token);
            if (token.IsCancellationRequested) return;

            BmkgLocationSearchResults.Clear();
            foreach (var r in results)
            {
                BmkgLocationSearchResults.Add(r);
            }
            OnPropertyChanged(nameof(HasBmkgLocationSearchResults));

            if (results.Count == 1)
            {
                await ApplyBmkgSearchResultAsync(results[0]);
            }
            else if (results.Count > 1)
            {
                BmkgLocationStatus = $"Ditemukan {results.Count} wilayah presisi — klik salah satu di bawah untuk menerapkan:";
            }
            else
            {
                BmkgLocationStatus = "Wilayah tidak ditemukan. Gunakan tombol 'Pilih Wilayah' (Provinsi ▸ Kota ▸ Kecamatan ▸ Kelurahan).";
            }
        }
        catch (Exception ex)
        {
            BmkgLocationStatus = $"Pencarian gagal: {ex.Message}";
        }
        finally
        {
            IsSearchingBmkgLocation = false;
        }
    }

    private Task ApplyBmkgSearchResultAsync(BmkgLocationSearchResult res)
    {
        var name = !string.IsNullOrWhiteSpace(res.VillageName)
            ? $"{res.VillageName}, {res.RegencyName}"
            : res.RegencyName;
        _locationService.ApplyPrecise(name, res.Adm4Code, res.Latitude, res.Longitude, res.Timezone);

        BmkgLocationSearchResults.Clear();
        OnPropertyChanged(nameof(HasBmkgLocationSearchResults));
        OnPropertyChanged(nameof(BmkgWeatherFullCityText));

        BmkgLocationStatus = $"✓ Lokasi presisi: {res.FullLabel} ({res.Adm4Code})";
        _ = SyncRegionSelectorsFromCurrentAdm4Async();
        return Task.CompletedTask;
    }

    public async Task SyncRegionSelectorsFromCurrentAdm4Async()
    {
        if (_isSyncingRegionSelectors) return;
        _isSyncingRegionSelectors = true;

        try
        {
            string adm4 = _bmkgService.GetActiveAdm4Code();
            var parts = adm4.Split('.');
            string provCode = parts.Length >= 1 ? parts[0] : "31";
            string regCode = parts.Length >= 2 ? $"{parts[0]}.{parts[1]}" : "31.71";
            string distCode = parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : "31.71.01";

            var prov = BmkgProvinces.FirstOrDefault(p => p.Code == provCode) ?? BmkgProvinces.FirstOrDefault();
            _selectedBmkgProvince = prov;
            OnPropertyChanged(nameof(SelectedBmkgProvince));

            if (prov != null)
            {
                var regencies = await _bmkgService.GetRegenciesAsync(prov.Code);
                BmkgRegencies.Clear();
                foreach (var r in regencies) BmkgRegencies.Add(r);

                var reg = BmkgRegencies.FirstOrDefault(r => r.Code == regCode) ?? BmkgRegencies.FirstOrDefault();
                _selectedBmkgRegency = reg;
                OnPropertyChanged(nameof(SelectedBmkgRegency));

                if (reg != null)
                {
                    var districts = await _bmkgService.GetDistrictsAsync(reg.Code);
                    BmkgDistricts.Clear();
                    foreach (var d in districts) BmkgDistricts.Add(d);

                    var dist = BmkgDistricts.FirstOrDefault(d => d.Code == distCode) ?? BmkgDistricts.FirstOrDefault();
                    _selectedBmkgDistrict = dist;
                    OnPropertyChanged(nameof(SelectedBmkgDistrict));

                    if (dist != null)
                    {
                        var villages = await _bmkgService.GetVillagesAsync(dist.Code);
                        BmkgVillages.Clear();
                        foreach (var v in villages) BmkgVillages.Add(v);

                        var vil = BmkgVillages.FirstOrDefault(v => v.Code == adm4) ?? BmkgVillages.FirstOrDefault();
                        _selectedBmkgVillage = vil;
                        OnPropertyChanged(nameof(SelectedBmkgVillage));
                    }
                }
            }
        }
        catch { }
        finally
        {
            _isSyncingRegionSelectors = false;
        }
    }

    private async Task OnSelectedProvinceChangedAsync(string provCode)
    {
        if (_isSyncingRegionSelectors) return;
        _isSyncingRegionSelectors = true;
        try
        {
            var regencies = await _bmkgService.GetRegenciesAsync(provCode);
            BmkgRegencies.Clear();
            foreach (var r in regencies) BmkgRegencies.Add(r);

            var firstReg = BmkgRegencies.FirstOrDefault();
            _selectedBmkgRegency = firstReg;
            OnPropertyChanged(nameof(SelectedBmkgRegency));

            if (firstReg != null)
            {
                var districts = await _bmkgService.GetDistrictsAsync(firstReg.Code);
                BmkgDistricts.Clear();
                foreach (var d in districts) BmkgDistricts.Add(d);

                var firstDist = BmkgDistricts.FirstOrDefault();
                _selectedBmkgDistrict = firstDist;
                OnPropertyChanged(nameof(SelectedBmkgDistrict));

                if (firstDist != null)
                {
                    var villages = await _bmkgService.GetVillagesAsync(firstDist.Code);
                    BmkgVillages.Clear();
                    foreach (var v in villages) BmkgVillages.Add(v);

                    var firstVil = BmkgVillages.FirstOrDefault();
                    _selectedBmkgVillage = firstVil;
                    OnPropertyChanged(nameof(SelectedBmkgVillage));

                    if (firstVil != null)
                    {
                        ApplyPreciseVillageSelection(firstVil);
                    }
                }
            }
        }
        finally
        {
            _isSyncingRegionSelectors = false;
        }
    }

    private async Task OnSelectedRegencyChangedAsync(string regCode)
    {
        if (_isSyncingRegionSelectors) return;
        _isSyncingRegionSelectors = true;
        try
        {
            var districts = await _bmkgService.GetDistrictsAsync(regCode);
            BmkgDistricts.Clear();
            foreach (var d in districts) BmkgDistricts.Add(d);

            var firstDist = BmkgDistricts.FirstOrDefault();
            _selectedBmkgDistrict = firstDist;
            OnPropertyChanged(nameof(SelectedBmkgDistrict));

            if (firstDist != null)
            {
                var villages = await _bmkgService.GetVillagesAsync(firstDist.Code);
                BmkgVillages.Clear();
                foreach (var v in villages) BmkgVillages.Add(v);

                var firstVil = BmkgVillages.FirstOrDefault();
                _selectedBmkgVillage = firstVil;
                OnPropertyChanged(nameof(SelectedBmkgVillage));

                if (firstVil != null)
                {
                    ApplyPreciseVillageSelection(firstVil);
                }
            }
        }
        finally
        {
            _isSyncingRegionSelectors = false;
        }
    }

    private async Task OnSelectedDistrictChangedAsync(string distCode)
    {
        if (_isSyncingRegionSelectors) return;
        _isSyncingRegionSelectors = true;
        try
        {
            var villages = await _bmkgService.GetVillagesAsync(distCode);
            BmkgVillages.Clear();
            foreach (var v in villages) BmkgVillages.Add(v);

            var firstVil = BmkgVillages.FirstOrDefault();
            _selectedBmkgVillage = firstVil;
            OnPropertyChanged(nameof(SelectedBmkgVillage));

            if (firstVil != null)
            {
                ApplyPreciseVillageSelection(firstVil);
            }
        }
        finally
        {
            _isSyncingRegionSelectors = false;
        }
    }

    private void ApplyPreciseVillageSelection(BmkgRegionItem village)
    {
        Settings.BmkgCustomAdm4Code = village.Code;
        string regName = _selectedBmkgRegency?.Name ?? "";
        string distName = _selectedBmkgDistrict?.Name ?? "";
        Settings.BmkgSelectedCity = !string.IsNullOrWhiteSpace(regName) ? $"{village.Name}, {regName}" : village.Name;
        _settingsService.Save();

        OnPropertyChanged(nameof(BmkgCustomAdm4Code));
        OnPropertyChanged(nameof(BmkgActiveAdm4Text));
        OnPropertyChanged(nameof(BmkgWeatherCityText));
        OnPropertyChanged(nameof(BmkgWeatherFullCityText));

        BmkgLocationStatus = $"✓ Lokasi presisi: Kel./Desa {village.Name}, Kec. {distName}, {regName} ({village.Code})";
        _ = _bmkgService.RefreshWeatherAsync();
    }
}
