using System.Windows.Input;
using System.Windows.Threading;
using NotchMusic.Models;

namespace NotchMusic.ViewModels;

public partial class MainViewModel
{
    private PanelMode _currentMode = PanelMode.Compact;

    private bool _isClickThrough;

    private bool _isHovering;
    private bool _isHoveringWindow;
    private DispatcherTimer? _hoverCollapseTimer;




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
                OnPropertyChanged(nameof(IsCompactMode));
                OnPropertyChanged(nameof(ShowCompactView));
                OnPropertyChanged(nameof(IsExpandedMode));
                OnPropertyChanged(nameof(IsLyricsMode));
                OnPropertyChanged(nameof(ShowCompactMusicView));
                OnPropertyChanged(nameof(ShowCompactTodoOnlyView));
                OnPropertyChanged(nameof(ShowComponentIcons));
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
                OnPropertyChanged(nameof(ShowHoverWeatherPill));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                OnPropertyChanged(nameof(IsNotchActive));
            }
        }
    }

    public bool ShowCompactView => IsCompactMode;

    public bool ShowComponentIcons => IsCompactMode && (_isHovering || IsPinned);

    // Slot tengah notch: saat idle berisi mata menyala; saat hover (komponen terbuka) slot ini disembunyikan.
    // Hanya mode compact: di mode lain island tidak boleh dipaksa ke ukuran kapsul idle (isinya terpotong habis).
    public bool ShowIdleEyes => IsCompactMode && !ShowComponentIcons;

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

    public bool IsNotchActive => IsAudioActive || IsQuranLoading || IsQuranWindowOpen || IsRadioBuffering || IsRadioWindowOpen || IsLiveTvBuffering || IsLiveTvWindowOpen || IsBrowserWindowOpen || CurrentMode != PanelMode.Compact || IsComponentsMenuOpen || HasActivePrayerNotification || _isHovering;

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
                    _hoverCollapseTimer?.Stop();
                }
                else if (!_isHoveringWindow)
                {
                    ScheduleHoverCollapse();
                }
                OnPropertyChanged(nameof(ShowCompactMusicView));
                OnPropertyChanged(nameof(ShowCompactTodoOnlyView));
                OnPropertyChanged(nameof(ShowComponentIcons));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
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

    public ICommand OpenSettingsCommand { get; }

    public ICommand ToggleComponentsMenuCommand { get; }

    public ICommand RestoreAllComponentsCommand { get; }

    public ICommand OpenMusicWindowCommand { get; }

    public ICommand CloseMusicWindowCommand { get; }

    public ICommand MinimizeMusicWindowCommand { get; }

    public ICommand ExpandMusicWindowCommand { get; }

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
        OnPropertyChanged(nameof(CurrentPrayerSchedule));
        OnPropertyChanged(nameof(NextPrayerName));
        OnPropertyChanged(nameof(NextPrayerTimeFormatted));
        OnPropertyChanged(nameof(NextPrayerCountdownBadge));
        OnPropertyChanged(nameof(PrayerScheduleTooltip));
        OnPropertyChanged(nameof(IsQuranWindowOpen));
        OnPropertyChanged(nameof(IsRadioWindowOpen));
        OnPropertyChanged(nameof(IsRadioActiveOrOpen));
        OnPropertyChanged(nameof(RadioStatusSummary));
        OnPropertyChanged(nameof(RadioButtonActionText));
        OnPropertyChanged(nameof(EnableBmkg));
        OnPropertyChanged(nameof(ShowWeatherInCompactNotch));
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
        OnPropertyChanged(nameof(ShowAirQualityInIsland));
        RefreshAirQuality(fetch: true);
        RequestComponentLayoutChanged?.Invoke();
    }

    /// <summary>
    /// Tampilan dapat menganimasikan island menciut ke kapsul idle: ia menerima aksi yang menyelesaikan penciutan
    /// (mengosongkan <see cref="IsHovering"/>) dan memanggilnya setelah animasi. Tanpa pelanggan, island kembali seketika.
    /// </summary>
    public event Action<Action>? RequestHoverCollapseAnimation;

    /// <summary>Kursor kembali ke notch saat animasi menciut berjalan: animasi harus dibatalkan.</summary>
    public event Action? HoverCollapseAnimationCancelled;

    public void OnMouseEnter()
    {
        _isHoveringWindow = true;
        HoverCollapseAnimationCancelled?.Invoke();
        IsHovering = true;
        _hoverCollapseTimer?.Stop();
        OnPropertyChanged(nameof(IsNotchActive));

        // Check for any cloud changes immediately when approaching the notch
        if (_googleDriveSyncService.IsConnected && Settings.GoogleDriveAutoSync && !_googleDriveSyncService.IsSyncing)
        {
            _ = _googleDriveSyncService.CheckForRemoteChangesAsync();
        }
    }

    public void OnMouseLeave()
    {
        _isHoveringWindow = false;
        OnPropertyChanged(nameof(IsNotchActive));

        if (!IsPinned)
        {
            ScheduleHoverCollapse();
        }
    }

    /// <summary>Jeda setelah kursor meninggalkan notch sebelum island kembali ke tampilan idle.</summary>
    public TimeSpan HoverCollapseDelay { get; set; } = TimeSpan.FromMilliseconds(1200);

    private void ScheduleHoverCollapse()
    {
        _hoverCollapseTimer?.Stop();
        if (IsPinned || IsLyricsMode)
        {
            return;
        }

        _hoverCollapseTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher.CurrentDispatcher) { Interval = HoverCollapseDelay };
        _hoverCollapseTimer.Tick += (s, e) =>
        {
            _hoverCollapseTimer?.Stop();
            CollapseHoverIfIdle();
        };
        _hoverCollapseTimer.Start();
    }

    /// <summary>Kembali ke idle bila kursor tidak di atas notch dan tidak ada yang menahan island tetap terbuka.</summary>
    public void CollapseHoverIfIdle()
    {
        if (!CanCollapseHover())
        {
            return;
        }

        if (IsHovering && RequestHoverCollapseAnimation is { } animate)
        {
            // Kondisi diperiksa lagi saat animasi selesai: kursor atau notifikasi bisa saja berubah selama bergerak.
            animate(() =>
            {
                if (CanCollapseHover()) IsHovering = false;
            });
            return;
        }

        IsHovering = false;
    }

    private bool CanCollapseHover()
        => !(_isHoveringWindow || IsPinned || IsLyricsMode || IsComponentsMenuOpen || HasVisibleIslandBanner);
}
