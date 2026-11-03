using System.Windows.Input;
using System.Windows.Threading;
using NotchMusic.Models;

namespace NotchMusic.ViewModels;

public partial class MainViewModel
{
    private PanelMode _currentMode = PanelMode.Compact;

    private bool _isBezelMinimized;

    private bool _isIdleMiniMode;

    private DispatcherTimer? _idleTimer;

    private bool _isClickThrough;

    private bool _isHovering;

    private bool _isHoveringWindow;

    public event Action? RequestCollapse;

    public event Action? RequestExpand;

    public event Action? RequestOpenSettings;

    public event Action? RequestOpenMusic;

    public event Action? RequestComponentLayoutChanged;

    public event Action<string, string>? RequestShowTrayNotification;

    public PanelMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (SetProperty(ref _currentMode, value))
            {
                if (value != PanelMode.Compact && _isIdleMiniMode)
                {
                    _isIdleMiniMode = false;
                    OnPropertyChanged(nameof(IsIdleMiniMode));
                    OnPropertyChanged(nameof(ShowIdleMiniView));
                }
                if (value != PanelMode.Compact && _isBezelMinimized)
                {
                    _isBezelMinimized = false;
                    Settings.IsBezelMinimized = false;
                    _settingsService.Save();
                    OnPropertyChanged(nameof(IsBezelMinimized));
                    OnPropertyChanged(nameof(IsNotBezelMinimized));
                    OnPropertyChanged(nameof(BezelMinimizeButtonTooltip));
                    OnPropertyChanged(nameof(BezelMinimizeMenuText));
                }
                OnPropertyChanged(nameof(IsCompactMode));
                OnPropertyChanged(nameof(ShowCompactView));
                OnPropertyChanged(nameof(IsExpandedMode));
                OnPropertyChanged(nameof(IsLyricsMode));
                OnPropertyChanged(nameof(ShowIdleMiniView));
                OnPropertyChanged(nameof(ShowCompactMusicView));
                OnPropertyChanged(nameof(ShowCompactTodoOnlyView));
                OnPropertyChanged(nameof(ShowComponentIcons));
                OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
                OnPropertyChanged(nameof(ShowIdleWeatherBesideLogo));
                OnPropertyChanged(nameof(ShowHoverWeatherPill));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
            }
        }
    }

    public bool IsCompactMode => CurrentMode == PanelMode.Compact;

    public bool IsExpandedMode => CurrentMode == PanelMode.Expanded;

    public bool IsHovering
    {
        get => _isHovering;
        private set
        {
            if (SetProperty(ref _isHovering, value))
            {
                OnPropertyChanged(nameof(ShowComponentIcons));
                OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
                OnPropertyChanged(nameof(ShowIdleWeatherBesideLogo));
                OnPropertyChanged(nameof(ShowHoverWeatherPill));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                OnPropertyChanged(nameof(IsNotchActive));
            }
        }
    }

    public bool IsBezelMinimized
    {
        get => _isBezelMinimized;
        set
        {
            if (SetProperty(ref _isBezelMinimized, value))
            {
                Settings.IsBezelMinimized = value;
                _settingsService.Save();
                if (value && _currentMode != PanelMode.Compact)
                {
                    _currentMode = PanelMode.Compact;
                    OnPropertyChanged(nameof(CurrentMode));
                    OnPropertyChanged(nameof(IsCompactMode));
                    OnPropertyChanged(nameof(IsExpandedMode));
                    OnPropertyChanged(nameof(IsLyricsMode));
                }
                OnPropertyChanged(nameof(IsNotBezelMinimized));
                OnPropertyChanged(nameof(ShowCompactView));
                OnPropertyChanged(nameof(ShowComponentIcons));
                OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
                OnPropertyChanged(nameof(ShowIdleWeatherBesideLogo));
                OnPropertyChanged(nameof(ShowHoverWeatherPill));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                OnPropertyChanged(nameof(BezelMinimizeButtonTooltip));
                OnPropertyChanged(nameof(BezelMinimizeMenuText));
            }
        }
    }

    public bool IsNotBezelMinimized => !_isBezelMinimized;

    public bool ShowCompactView => IsCompactMode && !_isBezelMinimized;

    public string BezelMinimizeButtonTooltip => IsBezelMinimized
        ? "Buka kembali Notch TAMANOTCH"
        : "Minimize Notch ke Bezel Atas (Agar tidak mengganggu jendela lain)";

    public string BezelMinimizeMenuText => IsBezelMinimized
        ? "▼ Buka Kembali Notch dari Bezel"
        : "▲ Minimize Notch ke Bezel Atas";

    public bool ShowComponentIcons => IsCompactMode && !_isBezelMinimized && (_isHovering || IsPinned);

    // Slot tengah notch: saat idle berisi mata menyala; saat hover (komponen terbuka) slot ini disembunyikan.
    public bool ShowIdleEyes => !ShowComponentIcons;

    public double CenterIconSize => Math.Clamp(Settings.IdleIconSize, 24, 48);

    // Mata berbentuk lebar (40:24); kapsul idle dibuat ramping mengikuti bentuknya.
    private const double EyesAspect = 24.0 / 40.0;

    public double CenterIconWidth => CenterIconSize;

    public double CenterIconHeight => CenterIconSize * EyesAspect;

    public System.Windows.Thickness CompactViewMargin => ShowIdleEyes
        ? new System.Windows.Thickness(12, 3, 12, 5)
        : new System.Windows.Thickness(14, 4, 14, 6);

    private void RaiseCenterIconChanged()
    {
        RaiseAppearanceChanged();
        OnPropertyChanged(nameof(ShowIdleEyes));
        OnPropertyChanged(nameof(CenterIconSize));
        OnPropertyChanged(nameof(CenterIconWidth));
        OnPropertyChanged(nameof(CenterIconHeight));
        OnPropertyChanged(nameof(CompactViewMargin));
    }

    public bool ShowMusicInNotch => ShowComponentIcons;

    public bool ShowMusicPillInNotch => ShowMusicInNotch && !IsMusicWindowOpen;

    public bool ShowMusicIconInNotch => ShowMusicInNotch && IsMusicWindowOpen;

    public string MusicNotchTooltip => HasTrack
        ? $"{CurrentTrack?.Title} - {CurrentTrack?.Artist}\nKlik untuk perluas pemutar & lirik"
        : "Pemutar Musik TAMANOTCH (Klik untuk perluas pemutar & lirik)";

    public string PinMenuText => IsPinned ? "Lepas Pin Notch (Otomatis ke Logo saat Idle)" : "📌 Pin Notch (Tetap Tampilkan 5 Ikon)";

    public bool IsIdleMiniMode
    {
        get => _isIdleMiniMode && !IsPinned;
        set
        {
            var effective = value && !IsPinned;
            if (SetProperty(ref _isIdleMiniMode, effective))
            {
                OnPropertyChanged(nameof(ShowIdleMiniView));
                OnPropertyChanged(nameof(ShowCompactMusicView));
                OnPropertyChanged(nameof(ShowCompactTodoOnlyView));
                OnPropertyChanged(nameof(ShowComponentIcons));
                OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                OnPropertyChanged(nameof(ShowFullMusicInCompact));
                OnPropertyChanged(nameof(ShowFullRadioInCompact));
                OnPropertyChanged(nameof(ShowFullTodoInCompact));
                OnPropertyChanged(nameof(ShowFullBrowserInCompact));
                OnPropertyChanged(nameof(MiniModeButtonTooltip));
                OnPropertyChanged(nameof(IsExpandedMode));
            }
        }
    }

    public bool ShowIdleMiniView => IsCompactMode && IsIdleMiniMode;

    public bool ShowFullMusicInCompact => !IsIdleMiniMode || IsPlaying;

    public string MiniModeButtonTooltip => IsIdleMiniMode
        ? "Perbesar Notch (Tampilkan semua detail komponen)"
        : "Kecilkan Notch (Tampilkan yang aktif & ikon saja)";

    public string ComponentDisplayMode
    {
        get => Settings.ComponentDisplayMode ?? "Both";
        set
        {
            if (Settings.ComponentDisplayMode != value)
            {
                Settings.ComponentDisplayMode = value;
                _settingsService.Save();
                RefreshComponentLayout();
            }
        }
    }

    public bool HasClosedComponents => IsPrayerClosed || IsTodoClosed;

    public int ClosedComponentsCount => (IsPrayerClosed ? 1 : 0) + (IsTodoClosed ? 1 : 0);

    public string ComponentsButtonText => HasClosedComponents ? $"Komponen ({ClosedComponentsCount})" : "Komponen";

    public bool IsMusicWindowOpen
    {
        get => Settings.IsMusicWindowOpen;
        set
        {
            if (Settings.IsMusicWindowOpen != value)
            {
                Settings.IsMusicWindowOpen = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsMusicWindowOpen));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool IsMusicWindowCompact
    {
        get => Settings.IsMusicWindowCompact;
        set
        {
            if (Settings.IsMusicWindowCompact != value)
            {
                Settings.IsMusicWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsMusicWindowCompact));
                OnPropertyChanged(nameof(IsMusicWindowExpanded));
            }
        }
    }

    public bool IsMusicWindowExpanded => !IsMusicWindowCompact;

    public string ComponentsButtonSymbol
    {
        get
        {
            if (IsPrayerClosed && IsTodoClosed)
                return "🕌 ✓";
            if (IsPrayerClosed)
                return "🕌";
            if (IsTodoClosed)
                return "✓";
            return "🍱";
        }
    }

    public string ComponentsButtonTooltip
    {
        get
        {
            if (IsPrayerClosed && IsTodoClosed)
                return "Komponen ditutup: Waktu Sholat & To-Do List (Klik untuk kelola/buka)";
            if (IsPrayerClosed)
                return "Komponen ditutup: Waktu Sholat (Klik untuk kelola/buka)";
            if (IsTodoClosed)
                return "Komponen ditutup: To-Do List (Klik untuk kelola/buka)";
            return "Kelola komponen notch";
        }
    }

    private bool _isComponentsMenuOpen;

    public bool IsComponentsMenuOpen
    {
        get => _isComponentsMenuOpen;
        set => SetProperty(ref _isComponentsMenuOpen, value);
    }

    public bool IsComponentBoth => ComponentDisplayMode == "Both";

    public bool IsComponentMusicOnly => ComponentDisplayMode == "MusicOnly";

    public bool ShowMusicSection => ComponentDisplayMode != "TodoOnly";

    public bool ShowMusicMinimizeButton => !ShowTodoSectionInMainWindow;

    public bool ShowSplitter => ShowExpandedPrayerDivider2;

    public bool ShowCompactMusicView => IsCompactMode && ShowMusicSection;

    public System.Windows.GridLength MusicColumnWidth => ShowMusicSection ? new System.Windows.GridLength(410) : new System.Windows.GridLength(0);

    public bool ShowNormalExpandedContent => !IsPrayerPanelOpen;

    public double ExpandedPanelWidth
    {
        get
        {
            if (IsPrayerPanelOpen)
                return 460;

            if (ShowMusicSection && ShowPrayerSectionInMainWindow && ShowTodoSectionInMainWindow)
                return 1150;
            if (ShowMusicSection && ShowPrayerSectionInMainWindow)
                return 716;
            if (ShowPrayerSectionInMainWindow && ShowTodoSectionInMainWindow)
                return 720;
            if (ShowMusicSection && ShowTodoSectionInMainWindow)
                return 845;

            if (ShowMusicSection)
                return 410;
            if (ShowTodoSectionInMainWindow)
                return 410;
            if (ShowPrayerSectionInMainWindow)
                return 285;

            return 410;
        }
    }

    public bool IsNotchActive => IsAudioActive || IsRadioBuffering || IsRadioWindowOpen || IsLiveTvBuffering || IsLiveTvWindowOpen || IsBrowserWindowOpen || CurrentMode != PanelMode.Compact || IsComponentsMenuOpen || HasActivePrayerNotification || _isHovering;

    public bool IsPinned
    {
        get => _isPinned;
        set
        {
            if (SetProperty(ref _isPinned, value))
            {
                Settings.IsPinned = value;
                _settingsService.Save();
                if (value)
                {
                    _idleTimer?.Stop();
                    if (_isIdleMiniMode)
                    {
                        _isIdleMiniMode = false;
                    }
                }
                else if (!_isHovering)
                {
                    ResetIdleTimer();
                }
                OnPropertyChanged(nameof(IsIdleMiniMode));
                OnPropertyChanged(nameof(ShowIdleMiniView));
                OnPropertyChanged(nameof(ShowCompactMusicView));
                OnPropertyChanged(nameof(ShowCompactTodoOnlyView));
                OnPropertyChanged(nameof(ShowComponentIcons));
                OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                OnPropertyChanged(nameof(ShowFullMusicInCompact));
                OnPropertyChanged(nameof(ShowFullRadioInCompact));
                OnPropertyChanged(nameof(ShowFullTodoInCompact));
                OnPropertyChanged(nameof(ShowFullBrowserInCompact));
                OnPropertyChanged(nameof(MiniModeButtonTooltip));
                OnPropertyChanged(nameof(IsExpandedMode));
                OnPropertyChanged(nameof(PinButtonTooltip));
                OnPropertyChanged(nameof(PinMenuText));
            }
        }
    }

    public string PinButtonTooltip => IsPinned
        ? "Notch sedang di-Pin (Tidak akan mengecil saat idle) — Klik untuk lepas Pin"
        : "Pin Notch agar tidak berubah menjadi kecil saat idle";

    public bool IsClickThrough
    {
        get => _isClickThrough;
        set => SetProperty(ref _isClickThrough, value);
    }

    public ICommand SetModeCommand { get; }

    public ICommand TogglePinCommand { get; }

    public ICommand ToggleBezelMinimizeCommand { get; }

    public ICommand OpenSettingsCommand { get; }

    public ICommand ToggleComponentsMenuCommand { get; }

    public ICommand RestoreAllComponentsCommand { get; }

    public ICommand OpenMusicWindowCommand { get; }

    public ICommand CloseMusicWindowCommand { get; }

    public ICommand MinimizeMusicWindowCommand { get; }

    public ICommand ExpandMusicWindowCommand { get; }

    public ICommand WakeFromIdleCommand { get; }

    public ICommand ToggleMiniModeCommand { get; }

    public ICommand ExitApplicationCommand { get; }

    public event Action? RequestExit;

    public void RefreshComponentLayout()
    {
        RaiseCenterIconChanged();
        OnPropertyChanged(nameof(ComponentDisplayMode));
        OnPropertyChanged(nameof(IsTodoDetached));
        OnPropertyChanged(nameof(IsPrayerDetached));
        OnPropertyChanged(nameof(IsTodoWindowOpen));
        OnPropertyChanged(nameof(IsPrayerWindowOpen));
        OnPropertyChanged(nameof(IsPrayerClosed));
        OnPropertyChanged(nameof(IsTodoClosed));
        OnPropertyChanged(nameof(IsTodoDetachedWindowVisible));
        OnPropertyChanged(nameof(HasClosedComponents));
        OnPropertyChanged(nameof(ClosedComponentsCount));
        OnPropertyChanged(nameof(ComponentsButtonText));
        OnPropertyChanged(nameof(ComponentsButtonSymbol));
        OnPropertyChanged(nameof(ComponentsButtonTooltip));
        OnPropertyChanged(nameof(IsComponentBoth));
        OnPropertyChanged(nameof(IsComponentMusicOnly));
        OnPropertyChanged(nameof(IsComponentTodoOnly));
        OnPropertyChanged(nameof(ShowMusicSection));
        OnPropertyChanged(nameof(ShowTodoSection));
        OnPropertyChanged(nameof(ShowTodoTicker));
        OnPropertyChanged(nameof(ShowTodoSectionInMainWindow));
        OnPropertyChanged(nameof(ShowMusicMinimizeButton));
        OnPropertyChanged(nameof(ShowSplitter));
        OnPropertyChanged(nameof(ShowIdleMiniView));
        OnPropertyChanged(nameof(ShowCompactMusicView));
        OnPropertyChanged(nameof(ShowCompactTodoOnlyView));
        OnPropertyChanged(nameof(ShowPrayerDivider1));
        OnPropertyChanged(nameof(ShowPrayerDivider2));
        OnPropertyChanged(nameof(ShowPrayerSectionInMainWindow));
        OnPropertyChanged(nameof(ShowExpandedPrayerDivider1));
        OnPropertyChanged(nameof(ShowExpandedPrayerDivider2));
        OnPropertyChanged(nameof(MusicColumnWidth));
        OnPropertyChanged(nameof(PrayerColumnWidth));
        OnPropertyChanged(nameof(TodoColumnWidth));
        OnPropertyChanged(nameof(ExpandedPanelWidth));
        OnPropertyChanged(nameof(EnablePrayerTimes));
        if (!EnablePrayerTimes) ActivePrayerNotification = null;
        OnPropertyChanged(nameof(HasActivePrayerNotification));
        OnPropertyChanged(nameof(ShowCompactPrayerPill));
        OnPropertyChanged(nameof(ShowIdlePrayerBesideLogo));
        OnPropertyChanged(nameof(CurrentPrayerSchedule));
        OnPropertyChanged(nameof(NextPrayerName));
        OnPropertyChanged(nameof(NextPrayerTimeFormatted));
        OnPropertyChanged(nameof(NextPrayerCountdownBadge));
        OnPropertyChanged(nameof(PrayerScheduleTooltip));
        OnPropertyChanged(nameof(IsRadioWindowOpen));
        OnPropertyChanged(nameof(IsRadioActiveOrOpen));
        OnPropertyChanged(nameof(RadioStatusSummary));
        OnPropertyChanged(nameof(RadioButtonActionText));
        OnPropertyChanged(nameof(EnableBmkg));
        OnPropertyChanged(nameof(ShowWeatherInCompactNotch));
        OnPropertyChanged(nameof(ShowIdleWeatherBesideLogo));
        OnPropertyChanged(nameof(ShowHoverWeatherPill));
        OnPropertyChanged(nameof(IsBmkgWindowOpen));
        OnPropertyChanged(nameof(SelectedBmkgCity));
        OnPropertyChanged(nameof(BmkgWeatherCityText));
        OnPropertyChanged(nameof(BmkgActiveAdm4Text));
        RefreshAdhanSettings();
        _prayerReminderService.RefreshSchedule();
        if (EnableBmkg)
        {
            _ = _bmkgService.RefreshAllAsync(forceWeather: false);
        }
        RequestComponentLayoutChanged?.Invoke();
    }

    private DispatcherTimer? _expandHoverTimer;

    public void ToggleMiniMode()
    {
        _idleTimer?.Stop();
        if (!IsIdleMiniMode)
        {
            if (IsPinned)
            {
                IsPinned = false;
            }
            IsIdleMiniMode = true;
        }
        else
        {
            IsIdleMiniMode = false;
            if (!IsPinned && !_isHovering)
            {
                ResetIdleTimer();
            }
        }
    }

    public void EnterIdleMiniMode()
    {
        if (IsPinned || IsLyricsMode || IsComponentsMenuOpen || HasActivePrayerNotification || HasActiveBmkgNotification)
        {
            return;
        }

        IsIdleMiniMode = true;
    }

    public void WakeFromIdle()
    {
        _idleTimer?.Stop();
        if (_isIdleMiniMode)
        {
            IsIdleMiniMode = false;
        }

        if (!IsPinned && !_isHovering)
        {
            ResetIdleTimer();
        }
    }

    public void ResetIdleTimer()
    {
        _idleTimer?.Stop();
        if (IsPinned || IsLyricsMode)
        {
            return;
        }

        if (System.Windows.Application.Current?.Dispatcher == null)
        {
            return;
        }

        _idleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
        _idleTimer.Tick += (s, e) =>
        {
            _idleTimer?.Stop();
            if (!_isHoveringWindow && !IsPinned && !IsLyricsMode && !IsComponentsMenuOpen && !HasActivePrayerNotification && !HasActiveBmkgNotification)
            {
                IsHovering = false;
                EnterIdleMiniMode();
            }
        };
        _idleTimer.Start();
    }

    public void OnMouseEnter()
    {
        _isHoveringWindow = true;
        IsHovering = true;
        _expandHoverTimer?.Stop();
        _idleTimer?.Stop();
        OnPropertyChanged(nameof(IsNotchActive));

        // Check for any cloud changes immediately when approaching the notch
        if (_googleDriveSyncService.IsConnected && Settings.GoogleDriveAutoSync && !_googleDriveSyncService.IsSyncing)
        {
            _ = _googleDriveSyncService.CheckForRemoteChangesAsync();
        }

        // Fitur On Hover dinonaktifkan: panel tidak lagi terbuka otomatis saat kursor diarahkan
        if (false && Settings.ExpandOnHover && CurrentMode == PanelMode.Compact && !IsPinned)
        {
            _expandHoverTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(650) };
            _expandHoverTimer.Tick += (s, e) =>
            {
                _expandHoverTimer?.Stop();
                if (_isHovering && CurrentMode == PanelMode.Compact && !IsPinned)
                {
                    RequestExpand?.Invoke();
                }
            };
            _expandHoverTimer.Start();
        }
    }

    public void OnMouseLeave()
    {
        _isHoveringWindow = false;
        _expandHoverTimer?.Stop();
        OnPropertyChanged(nameof(IsNotchActive));

        if (!IsPinned)
        {
            ResetIdleTimer();
        }

        // Fitur On Hover dinonaktifkan: panel tidak lagi tertutup otomatis saat kursor keluar
        if (false && Settings.ExpandOnHover && CurrentMode == PanelMode.Expanded && !IsPinned)
        {
            var delayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            delayTimer.Tick += (s, e) =>
            {
                delayTimer.Stop();
                if (!_isHovering && !IsPinned && CurrentMode == PanelMode.Expanded)
                {
                    RequestCollapse?.Invoke();
                }
            };
            delayTimer.Start();
        }
    }
}
