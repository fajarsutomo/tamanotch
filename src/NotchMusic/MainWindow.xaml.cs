using System.Windows;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.Media;
using NotchMusic.Services.Settings;
using NotchMusic.Services.Tray;
using NotchMusic.ViewModels;
using NotchMusic.Views;

namespace NotchMusic;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly ITrayIconService _trayService;
    private readonly IMediaSessionService _mediaService;
    private YouTubeMusicWindow? _ytMusicWindow;
    private SpotifyWindow? _spotifyWindow;
    private JellyfinWindow? _jellyfinWindow;
    private FloatingBrowserWindow? _floatingBrowserWindow;
    private RadioWindow? _radioWindow;
    private QuranWindow? _quranWindow;
    private LiveTvWindow? _liveTvWindow;
    private MusicWindow? _musicWindow;
    private TodoWindow? _todoWindow;
    private PrayerWindow? _prayerWindow;
    private BmkgWindow? _bmkgWindow;
    private HotkeyManager? _hotkeyManager;
    private bool _isRealExit;

    public MainWindow()
    {
        InitializeComponent();

        _settingsService = new SettingsService();
        Services.Dns.WebViewDns.Settings = _settingsService;
        var lyricsService = new LyricsService(_settingsService);
        var mediaService = new WindowsMediaSessionService();
        _trayService = new TrayIconService();

        _viewModel = new MainViewModel(mediaService, lyricsService, _settingsService);
        DataContext = _viewModel;
        WindowHelper.HorizontalOffsetPercent = _settingsService.CurrentSettings.NotchHorizontalOffsetPercent;

        _viewModel.RequestHoverCollapseAnimation += AnimateHoverCollapse;
        _viewModel.HoverCollapseAnimationCancelled += CancelHoverCollapseAnimation;
        _viewModel.PropertyChanged += (s, ev) =>
        {
            if (ev.PropertyName == nameof(MainViewModel.ShowComponentIcons) && _viewModel.ShowComponentIcons && _viewModel.IsCompactMode)
            {
                BeginHoverExpandAnimation();
            }
        };
        _viewModel.RequestOpenSettings += OpenSettings;
        _viewModel.RequestOpenYouTubeMusic += OpenYouTubeMusic;
        _mediaService = mediaService;
        _viewModel.RequestOpenSpotify += OpenSpotify;
        _mediaService.PlaybackStartedOnSpotify += (s, e) => Dispatcher.Invoke(PauseYouTubeMusicPlayback);
        _mediaService.PlaybackStartedOnYouTubeMusic += (s, e) => Dispatcher.Invoke(PauseSpotifyPlayback);
        _viewModel.PlaybackStartedOnSpotify += () => Dispatcher.Invoke(PauseYouTubeMusicPlayback);
        _viewModel.PlaybackStartedOnYouTubeMusic += () => Dispatcher.Invoke(PauseSpotifyPlayback);
        _viewModel.PlaybackStartedOnSpotify += () => Dispatcher.Invoke(PauseJellyfinPlayback);
        _viewModel.PlaybackStartedOnYouTubeMusic += () => Dispatcher.Invoke(PauseJellyfinPlayback);
        _viewModel.RequestOpenJellyfin += OpenJellyfin;
        _viewModel.PlaybackStartedOnJellyfin += () => Dispatcher.Invoke(() =>
        {
            PauseYouTubeMusicPlayback();
            PauseSpotifyPlayback();
        });
        _viewModel.RequestVocalRemoverChanged += OnVocalRemoverChanged;
        _viewModel.RequestOpenFloatingBrowser += OpenFloatingBrowser;
        _viewModel.RequestOpenRadio += OpenRadio;
        _viewModel.RequestOpenPrayerSchedule += OpenPrayerSchedule;
        _viewModel.RequestOpenQuran += OpenQuran;
        _viewModel.PlaybackStartedOnQuran += () => Dispatcher.Invoke(() =>
        {
            PauseYouTubeMusicPlayback();
            PauseSpotifyPlayback();
            PauseJellyfinPlayback();
        });
        _viewModel.RequestOpenLiveTv += OpenLiveTv;
        _viewModel.RequestOpenMusic += OpenMusic;
        _viewModel.RequestOpenBmkg += OpenBmkg;
        _viewModel.RequestExit += () => Dispatcher.Invoke(ExitApplication);
        _viewModel.RequestComponentLayoutChanged += () => Dispatcher.Invoke(() =>
        {
            UpdateTodoWindowState();
            UpdatePrayerWindowState();
            UpdateBmkgWindowState();
            UpdateMusicWindowState();
            UpdateBrowserWindowState();
            UpdateRadioWindowState();
            UpdateQuranWindowState();
            UpdateLiveTvWindowState();
        });
        _viewModel.RequestShowTrayNotification += (title, msg) => Dispatcher.Invoke(() => _trayService.ShowNotification(title, msg));

        _viewModel.PropertyChanged += (s, ev) =>
        {
            if (ev.PropertyName == nameof(MainViewModel.CurrentMode) ||
                ev.PropertyName == nameof(MainViewModel.IsCompactMode) ||
                ev.PropertyName == nameof(MainViewModel.ShowCompactView) ||
                ev.PropertyName == nameof(MainViewModel.IsMusicWindowOpen) ||
                ev.PropertyName == nameof(MainViewModel.ShowMusicInNotch) ||
                ev.PropertyName == nameof(MainViewModel.IsTodoDetachedWindowVisible))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    PositionCompactWindow();
                    _settingsService.CurrentSettings.PanelLeft = Left;
                    _settingsService.CurrentSettings.PanelTop = Top;
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        };

        SizeChanged += (s, ev) =>
        {
            if (ev.NewSize.Width > 0)
            {
                if (_viewModel.IsCompactMode || _viewModel.IsLyricsMode)
                {
                    PositionCompactWindow();
                }
                else if (ev.PreviousSize.Width > 0 && Math.Abs(ev.PreviousSize.Width - ev.NewSize.Width) > 1)
                {
                    double center = Left + ev.PreviousSize.Width / 2.0;
                    Left = center - ev.NewSize.Width / 2.0;
                    WindowHelper.DockToTopBezel(this);
                }
            }
        };

        SetupTray();
    }

    /// <summary>Durasi island menciut ke kapsul idle; cukup panjang agar gerakannya terbaca.</summary>
    private static readonly TimeSpan HoverCollapseDuration = TimeSpan.FromMilliseconds(400);

    /// <summary>Durasi island membuka dari kapsul idle; sama dengan menciut agar terasa setara.</summary>
    private static readonly TimeSpan HoverExpandDuration = TimeSpan.FromMilliseconds(400);

    private int _collapseAnimationVersion;
    private bool _collapseAnimating;

    /// <summary>
    /// Island hover menciut ke kapsul idle: isinya memudar dan bingkainya mengecil ke arah kapsul (dari tengah atas),
    /// baru setelah itu keadaan idle diterapkan sehingga pergantiannya mulus. Window tetap sebesar island hover selama animasi.
    /// </summary>
    private void AnimateHoverCollapse(Action commit)
    {
        if (_collapseAnimating) return;
        if (_expandAnimating)
        {
            _expandAnimationVersion++;
            ResetIslandAnimation();
        }

        var border = MainIslandBorder;
        if (!WindowAnimator.Enabled || !IsVisible || border.Child is not UIElement content
            || border.ActualWidth <= 0 || border.ActualHeight <= 0)
        {
            commit();
            return;
        }

        var (idleWidth, idleHeight) = _viewModel.EstimateIdleCapsuleSize();
        double scaleX = Math.Min(1.0, idleWidth / border.ActualWidth);
        double scaleY = Math.Min(1.0, idleHeight / border.ActualHeight);
        if (scaleX >= 0.98 && scaleY >= 0.98)
        {
            commit();
            return;
        }

        _collapseAnimating = true;
        int version = ++_collapseAnimationVersion;

        // Island menciut ke kapsul idle yang sebenarnya (bisa di kanan/kiri tengah), bukan ke tengah island.
        double originX = ComputeCapsulePivotX(border, idleWidth, scaleX);
        border.RenderTransformOrigin = new System.Windows.Point(originX, 0);
        var scale = new System.Windows.Media.ScaleTransform(1, 1);
        border.RenderTransform = scale;

        // Bayangan (glow) dirender lewat software dan dihitung ulang tiap frame, itu yang membuat animasi patah-patah.
        // Selama animasi, glow dilepas dan bingkai disimpan sebagai tekstur GPU (BitmapCache) agar skala mulus.
        border.Effect = null;
        border.CacheMode = new System.Windows.Media.BitmapCache(1.0);

        var duration = HoverCollapseDuration;
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut };
        var shrinkX = new System.Windows.Media.Animation.DoubleAnimation(1, scaleX, duration) { EasingFunction = ease };
        var shrinkY = new System.Windows.Media.Animation.DoubleAnimation(1, scaleY, duration) { EasingFunction = ease };
        // Isi memudar lebih cepat agar tidak terlihat gepeng saat bingkai mengecil.
        var fade = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(HoverCollapseDuration.TotalMilliseconds * 0.45));

        shrinkX.Completed += (s, e) =>
        {
            if (version != _collapseAnimationVersion) return;   // dibatalkan
            commit();
            ResetIslandAnimation();
        };

        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, shrinkX);
        scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, shrinkY);
        content.BeginAnimation(UIElement.OpacityProperty, fade);
    }

    /// <summary>
    /// Asal skala agar tepi kiri dan kanan island bertemu tepat dengan tepi kapsul idle: p = (kiriKapsul - s * kiriIsland) / (1 - s).
    /// </summary>
    private double ComputeCapsulePivotX(System.Windows.Controls.Border border, double idleWidth, double scaleX)
    {
        if (IdleNotchCenterX is not double idleCenter || scaleX >= 0.98) return 0.5;

        double islandLeft = Left + border.TranslatePoint(new System.Windows.Point(0, 0), this).X;
        double capsuleLeft = idleCenter - idleWidth / 2.0;
        double pivot = (capsuleLeft - scaleX * islandLeft) / (1.0 - scaleX);
        return Math.Clamp((pivot - islandLeft) / border.ActualWidth, -2.0, 3.0);
    }

    private int _expandAnimationVersion;
    private bool _expandAnimating;

    /// <summary>
    /// Island hover membuka dari kapsul idle (kebalikan menciut): bingkai membesar dari ukuran kapsul dan isinya muncul belakangan.
    /// Dipanggil saat keadaan hover mulai; bingkai disembunyikan sesaat agar tidak berkedip utuh sebelum animasi berjalan.
    /// </summary>
    private void BeginHoverExpandAnimation()
    {
        if (!WindowAnimator.Enabled || !IsVisible || _collapseAnimating) return;

        var border = MainIslandBorder;
        int version = ++_expandAnimationVersion;
        _expandAnimating = true;
        border.Opacity = 0;

        // Prioritas Render (bukan Background) agar jeda tanpa gambar sependek mungkin; tata letak dipaksa selesai dulu
        // supaya ukuran dan posisi jendela hover sudah final saat asal skala dihitung.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render, new Action(() =>
        {
            if (version != _expandAnimationVersion) return;   // dibatalkan, bingkai sudah dipulihkan
            UpdateLayout();

            if (!_viewModel.ShowComponentIcons || border.Child is not UIElement content
                || border.ActualWidth <= 0 || border.ActualHeight <= 0)
            {
                ResetIslandAnimation();
                return;
            }

            var (idleWidth, idleHeight) = _viewModel.EstimateIdleCapsuleSize();
            double scaleX = Math.Min(1.0, idleWidth / border.ActualWidth);
            double scaleY = Math.Min(1.0, idleHeight / border.ActualHeight);
            if (scaleX >= 0.98 && scaleY >= 0.98)
            {
                ResetIslandAnimation();
                return;
            }

            border.RenderTransformOrigin = new System.Windows.Point(ComputeCapsulePivotX(border, idleWidth, scaleX), 0);
            var scale = new System.Windows.Media.ScaleTransform(scaleX, scaleY);
            border.RenderTransform = scale;
            border.Effect = null;
            border.CacheMode = new System.Windows.Media.BitmapCache(1.0);
            content.Opacity = 0;
            border.Opacity = 1;

            var duration = HoverExpandDuration;
            var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
            var growX = new System.Windows.Media.Animation.DoubleAnimation(scaleX, 1, duration) { EasingFunction = ease };
            var growY = new System.Windows.Media.Animation.DoubleAnimation(scaleY, 1, duration) { EasingFunction = ease };
            // Isi baru muncul setelah bingkai cukup besar, supaya teks tidak terlihat gepeng.
            var fade = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.5))
            {
                BeginTime = TimeSpan.FromMilliseconds(duration.TotalMilliseconds * 0.4)
            };

            growX.Completed += (s, e) =>
            {
                if (version != _expandAnimationVersion) return;
                ResetIslandAnimation();
            };

            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, growX);
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, growY);
            content.BeginAnimation(UIElement.OpacityProperty, fade);
        }));
    }

    private void CancelHoverCollapseAnimation()
    {
        if (!_collapseAnimating) return;
        _collapseAnimationVersion++;
        ResetIslandAnimation();
    }

    private void ResetIslandAnimation()
    {
        var border = MainIslandBorder;
        if (border.Child is UIElement content)
        {
            content.BeginAnimation(UIElement.OpacityProperty, null);
            content.Opacity = 1;
        }
        if (border.RenderTransform is System.Windows.Media.ScaleTransform scale)
        {
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, null);
        }
        border.RenderTransform = System.Windows.Media.Transform.Identity;
        border.RenderTransformOrigin = new System.Windows.Point(0, 0);
        border.ClearValue(UIElement.EffectProperty);       // kembali ke glow dari style
        border.ClearValue(UIElement.CacheModeProperty);
        border.Opacity = 1;
        _collapseAnimating = false;
        _expandAnimating = false;
    }

    // Tepi kanan notch saat idle; notch notifikasi (merah/hijau) melebar ke kiri dari tepi ini.
    private double _idleRightEdge = double.NaN;
    private double _idleLeftEdge = double.NaN;
    private double _idleCenterX = double.NaN;

    /// <summary>Titik tengah horizontal kapsul idle (koordinat layar, DIP); null sebelum notch pernah tampil idle.</summary>
    public double? IdleNotchCenterX => double.IsNaN(_idleCenterX) ? null : _idleCenterX;

    /// <summary>
    /// Posisi jendela di mode compact. Notch idle dipusatkan (dengan geseran horizontal pengguna). Notch notifikasi dan island
    /// hover melebar dari kapsul idle: ke kiri dari tepi kanannya bila notch berada di kanan layar, ke kanan dari tepi kirinya
    /// bila di kiri, dan simetris bila di tengah.
    /// </summary>
    private void PositionCompactWindow()
    {
        if (ActualWidth > 0 && !double.IsNaN(_idleRightEdge))
        {
            bool alertNotch = _viewModel.ShowAlertNotch || _viewModel.ShowPrayerNotch || _viewModel.ShowWellnessNotch;
            bool hoverIsland = _viewModel.IsCompactMode && _viewModel.ShowComponentIcons;
            double offset = _settingsService.CurrentSettings.NotchHorizontalOffsetPercent;

            double? left = null;
            if (alertNotch)
            {
                left = _idleRightEdge - ActualWidth;
            }
            else if (hoverIsland && offset > SideAnchorThresholdPercent)
            {
                left = _idleRightEdge - ActualWidth;
            }
            else if (hoverIsland && offset < -SideAnchorThresholdPercent)
            {
                left = _idleLeftEdge;
            }

            if (left.HasValue)
            {
                var area = WindowHelper.GetWorkAreaSize(this);
                Left = Math.Clamp(left.Value, 0, Math.Max(0, area.Width - ActualWidth));
                Top = 0;
                return;
            }
        }

        WindowHelper.CenterAtTopBezel(this);
        if (_viewModel.ShowEyesInIdle && ActualWidth > 0)
        {
            _idleLeftEdge = Left;
            _idleRightEdge = Left + ActualWidth;
            _idleCenterX = Left + ActualWidth / 2.0;
        }
    }

    /// <summary>Geseran notch (persen lebar layar) di bawah ini dianggap "di tengah": island hover tetap simetris.</summary>
    private const double SideAnchorThresholdPercent = 5;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        // Position window top-center flush with the top bezel of Windows
        var settings = _settingsService.CurrentSettings;
        var primary = System.Windows.Forms.Screen.PrimaryScreen;
        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        if (!_viewModel.IsCompactMode && settings.PanelLeft > 0)
        {
            Left = settings.PanelLeft;
            Top = 0;
            WindowHelper.DockToTopBezel(this);
        }
        else
        {
            if (primary != null)
            {
                var bounds = primary.Bounds;
                double screenLeft = bounds.Left / dpiX;
                double screenTop = bounds.Top / dpiY;
                double screenWidth = bounds.Width / dpiX;
                Left = screenLeft + (screenWidth - ActualWidth) / 2.0;
                Top = screenTop;
            }
            WindowHelper.CenterAtTopBezel(this);
        }

        _settingsService.CurrentSettings.PanelLeft = Left;
        _settingsService.CurrentSettings.PanelTop = Top;

        // Setup Hotkeys
        SetupHotkeys();

        // Listen for screen resolution / monitor change
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += (s, ev) =>
        {
            Dispatcher.Invoke(() =>
            {
                if (_viewModel.IsCompactMode)
                    WindowHelper.CenterAtTopBezel(this);
                else
                    WindowHelper.DockToTopBezel(this);
            });
        };

        UpdateTodoWindowState();
        UpdatePrayerWindowState();
        UpdateBmkgWindowState();
        UpdateMusicWindowState();

        await _viewModel.InitializeAsync();
    }

    private void SetupTray()
    {
        _trayService.Initialize();
        _trayService.ToggleVisibilityRequested += () =>
        {
            Dispatcher.Invoke(() =>
            {
                if (Visibility == Visibility.Visible)
                {
                    Hide();
                    _todoWindow?.Hide();
                    _prayerWindow?.Hide();
                    _bmkgWindow?.Hide();
                    _musicWindow?.Hide();
                }
                else
                {
                    Show();
                    Activate();
                    if (_viewModel.IsCompactMode)
                        WindowHelper.CenterAtTopBezel(this);
                    else
                        WindowHelper.DockToTopBezel(this);
                    if (_viewModel.IsTodoDetached && _viewModel.ShowTodoSection)
                    {
                        _todoWindow?.Show();
                    }
                    if (_viewModel.IsPrayerDetached && _viewModel.EnablePrayerTimes)
                    {
                        _prayerWindow?.Show();
                    }
                    if (_viewModel.EnableBmkg && _viewModel.IsBmkgWindowOpen)
                    {
                        _bmkgWindow?.Show();
                    }
                }
            });
        };

        _trayService.ModeChangeRequested += (mode) =>
        {
            Dispatcher.Invoke(() =>
            {
                Show();
                _viewModel.CurrentMode = mode;
                if (_viewModel.IsCompactMode)
                    WindowHelper.CenterAtTopBezel(this);
                else
                    WindowHelper.DockToTopBezel(this);
            });
        };

        _trayService.ToggleClickThroughRequested += () =>
        {
            Dispatcher.Invoke(() =>
            {
                _viewModel.IsClickThrough = !_viewModel.IsClickThrough;
                WindowHelper.SetClickThrough(this, _viewModel.IsClickThrough);
            });
        };

        _trayService.OpenYouTubeMusicRequested += () => Dispatcher.Invoke(OpenYouTubeMusic);
        _trayService.OpenSpotifyRequested += () => Dispatcher.Invoke(OpenSpotify);
        _trayService.OpenJellyfinRequested += () => Dispatcher.Invoke(OpenJellyfin);
        _trayService.OpenRadioRequested += () => Dispatcher.Invoke(OpenRadio);
        _trayService.OpenQuranRequested += () => Dispatcher.Invoke(OpenQuran);
        _trayService.OpenLiveTvRequested += () => Dispatcher.Invoke(OpenLiveTv);
        _trayService.OpenBmkgRequested += () => Dispatcher.Invoke(OpenBmkg);
        _trayService.OpenFloatingBrowserRequested += () => Dispatcher.Invoke(OpenFloatingBrowser);
        _trayService.OpenSettingsRequested += () => Dispatcher.Invoke(OpenSettings);

        _trayService.ExitRequested += () =>
        {
            Dispatcher.Invoke(() =>
            {
                _isRealExit = true;
                Close();
            });
        };
    }

    private void SetupHotkeys()
    {
        _hotkeyManager?.Dispose();
        _hotkeyManager = new HotkeyManager(this);

        var settings = _settingsService.CurrentSettings;
        if (!string.IsNullOrEmpty(settings.ToggleVisibilityHotkey))
        {
            _hotkeyManager.Register(settings.ToggleVisibilityHotkey, () =>
            {
                if (Visibility == Visibility.Visible)
                {
                    Hide();
                    _todoWindow?.Hide();
                    _prayerWindow?.Hide();
                    _musicWindow?.Hide();
                }
                else
                {
                    Show();
                    Activate();
                    if (_viewModel.IsTodoDetached && _viewModel.ShowTodoSection)
                    {
                        _todoWindow?.Show();
                    }
                    if (_viewModel.IsPrayerDetached && _viewModel.EnablePrayerTimes)
                    {
                        _prayerWindow?.Show();
                    }
                }
            }, out _);
        }

        if (!string.IsNullOrEmpty(settings.ToggleLyricsHotkey))
        {
            _hotkeyManager.Register(settings.ToggleLyricsHotkey, () =>
            {
                Show();
                _viewModel.CurrentMode = _viewModel.CurrentMode == PanelMode.Lyrics ? PanelMode.Compact : PanelMode.Lyrics;
            }, out _);
        }
    }

    private void UpdateTodoWindowState()
    {
        if (_viewModel.IsTodoDetached && _viewModel.IsTodoWindowOpen)
        {
            if (_todoWindow == null)
            {
                _todoWindow = new TodoWindow(_viewModel, _settingsService);
                _todoWindow.Closed += (s, e) => _todoWindow = null;
            }
            WindowAnimator.Cancel(_todoWindow);
            if (!_todoWindow.IsVisible)
            {
                if (_todoWindow.WindowState == WindowState.Minimized)
                {
                    _todoWindow.WindowState = WindowState.Normal;
                }
                _todoWindow.ShowFromNotch();
                _todoWindow.Activate();
            }
        }
        else
        {
            _todoWindow?.HideToNotch();
        }
    }

    private void UpdatePrayerWindowState()
    {
        if (_viewModel.IsPrayerDetached && _viewModel.EnablePrayerTimes && _viewModel.IsPrayerWindowOpen)
        {
            if (_prayerWindow == null)
            {
                _prayerWindow = new PrayerWindow(_viewModel, _settingsService);
                _prayerWindow.Closed += (s, e) => _prayerWindow = null;
            }
            WindowAnimator.Cancel(_prayerWindow);
            if (!_prayerWindow.IsVisible)
            {
                _prayerWindow.ShowFromNotch();
            }
        }
        else if (_viewModel.IsPrayerDetached || !_viewModel.EnablePrayerTimes)
        {
            // Terpasang di notch: jendela yang dibuka dari pill dibiarkan sampai ditutup sendiri.
            _prayerWindow?.HideToNotch();
        }
    }

    private void OpenPrayerSchedule()
    {
        if (!_viewModel.EnablePrayerTimes) return;

        if (_prayerWindow == null)
        {
            _prayerWindow = new PrayerWindow(_viewModel, _settingsService);
            _prayerWindow.Closed += (s, e) => _prayerWindow = null;
        }
        if (_prayerWindow.WindowState == WindowState.Minimized)
        {
            _prayerWindow.WindowState = WindowState.Normal;
        }
        WindowAnimator.Cancel(_prayerWindow);
        if (!_prayerWindow.IsVisible)
        {
            _prayerWindow.ShowFromNotch();
        }
        _prayerWindow.Activate();
    }

    private void UpdateBrowserWindowState()
    {
        if (_viewModel.IsBrowserWindowOpen)
        {
            OpenFloatingBrowser();
        }
        else
        {
            _floatingBrowserWindow?.Hide();
        }
    }

    private void UpdateMusicWindowState()
    {
        if (_viewModel.IsMusicWindowOpen)
        {
            OpenMusic();
        }
        else
        {
            _musicWindow?.HideToNotch();
        }
    }

    public void OpenMusic()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsMusicWindowOpen = true;

            if (_musicWindow == null)
            {
                _musicWindow = new MusicWindow(_viewModel, _settingsService);
                _musicWindow.Closed += (s, e) => _musicWindow = null;
            }

            if (_musicWindow.WindowState == WindowState.Minimized)
            {
                _musicWindow.WindowState = WindowState.Normal;
            }

            WindowAnimator.Cancel(_musicWindow);
            _musicWindow.ShowFromNotch();
            _musicWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka Pemutar Musik:\n{ex.Message}",
                "TAMANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateRadioWindowState()
    {
        if (_viewModel.IsRadioWindowOpen)
        {
            OpenRadio();
        }
        else
        {
            _radioWindow?.HideToNotch();
        }
    }

    private void UpdateQuranWindowState()
    {
        if (_viewModel.IsQuranWindowOpen)
        {
            OpenQuran();
        }
        else
        {
            _quranWindow?.HideToNotch();
        }
    }

    private void UpdateLiveTvWindowState()
    {
        if (_viewModel.IsLiveTvWindowOpen)
        {
            OpenLiveTv();
        }
        else
        {
            _liveTvWindow?.Hide();
        }
    }

    private void UpdateBmkgWindowState()
    {
        if (_viewModel.EnableBmkg && _viewModel.IsBmkgWindowOpen)
        {
            OpenBmkg();
        }
        else
        {
            _bmkgWindow?.HideToNotch();
        }
    }

    public void OpenBmkg()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsBmkgWindowOpen = true;

            if (_bmkgWindow == null)
            {
                _bmkgWindow = new BmkgWindow(_viewModel, _settingsService);
                _bmkgWindow.Closed += (s, e) => _bmkgWindow = null;
            }

            if (_bmkgWindow.WindowState == WindowState.Minimized)
            {
                _bmkgWindow.WindowState = WindowState.Normal;
            }

            WindowAnimator.Cancel(_bmkgWindow);
            _bmkgWindow.ShowFromNotch();
            _bmkgWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka Cuaca & Gempa Bumi BMKG:\n{ex.Message}",
                "TAMANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ApplyNotchHorizontalOffset()
    {
        WindowHelper.HorizontalOffsetPercent = _settingsService.CurrentSettings.NotchHorizontalOffsetPercent;
        PositionCompactWindow();
        _settingsService.CurrentSettings.PanelLeft = Left;
        _settingsService.CurrentSettings.PanelTop = Top;
    }

    private void OpenSettings()
    {
        var settingsVm = new SettingsViewModel(_settingsService, _ => SetupHotkeys(), _viewModel.GoogleDriveSyncService, _viewModel.UpdateService, _viewModel.PrayerService, _viewModel.LocationService, _viewModel.BmkgService, _viewModel.WellnessService);
        settingsVm.PropertyChanged += (s, ev) =>
        {
            if (ev.PropertyName == nameof(SettingsViewModel.NotchHorizontalOffsetPercent))
            {
                ApplyNotchHorizontalOffset();
                return;
            }

            _viewModel.RefreshComponentLayout();
            UpdatePrayerWindowState();
            UpdateBmkgWindowState();
            UpdateTodoWindowState();
            _ = _viewModel.BmkgService.RefreshAllAsync();
        };
        if (_viewModel.HasAvailableUpdate && _viewModel.AvailableUpdateInfo != null)
        {
            settingsVm.AvailableUpdate = _viewModel.AvailableUpdateInfo;
            settingsVm.HasAvailableUpdate = true;
            settingsVm.UpdateStatus = $"Tersedia versi baru: v{_viewModel.AvailableUpdateInfo.Version} ({_viewModel.AvailableUpdateInfo.UpdateTypeLabel})";
        }
        var jellyfinUrlBefore = _settingsService.CurrentSettings.JellyfinUrl;
        var win = new SettingsWindow(settingsVm) { Owner = this };
        win.ShowDialog();
        if (_settingsService.CurrentSettings.JellyfinUrl != jellyfinUrlBefore)
        {
            _jellyfinWindow?.NavigateHome();
        }
        _viewModel.RefreshComponentLayout();
        UpdateTodoWindowState();
        UpdatePrayerWindowState();
        UpdateBmkgWindowState();
        UpdateMusicWindowState();
        UpdateBrowserWindowState();
        UpdateRadioWindowState();
    }

    private async void OnVocalRemoverChanged(bool enabled)
    {
        try
        {
            if (_ytMusicWindow != null)
            {
                await _ytMusicWindow.SetVocalRemoverAsync(enabled);
            }

            if (enabled)
            {
                _viewModel.SessionStatus = (_ytMusicWindow != null)
                    ? "🎤 Mode Karaoke (Penghilang Vokal) Aktif"
                    : "🎤 Karaoke Aktif (Putar lagu di ▶ YT Music untuk hapus vokal)";
            }
            else
            {
                _viewModel.SessionStatus = "🎤 Mode Karaoke Dinonaktifkan";
            }
        }
        catch { }
    }

    public void OpenYouTubeMusic()
    {
        try
        {
            // Minimize Tamanotch ke mode compact saat YT Music dibuka
            _viewModel.CurrentMode = PanelMode.Compact;

            if (_ytMusicWindow == null)
            {
                _ytMusicWindow = new YouTubeMusicWindow(_viewModel, _settingsService);
                _ytMusicWindow.Closed += (s, e) => _ytMusicWindow = null;

                var saved = _settingsService.CurrentSettings;
                if (saved.YouTubeMusicLeft > 0 && saved.YouTubeMusicTop >= 0 &&
                    saved.YouTubeMusicWidth >= _ytMusicWindow.MinWidth &&
                    saved.YouTubeMusicHeight >= _ytMusicWindow.MinHeight)
                {
                    _ytMusicWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _ytMusicWindow.Left = saved.YouTubeMusicLeft;
                    _ytMusicWindow.Top = saved.YouTubeMusicTop;
                    _ytMusicWindow.Width = saved.YouTubeMusicWidth;
                    _ytMusicWindow.Height = saved.YouTubeMusicHeight;
                }
                else
                {
                    // Dapatkan area monitor tempat Tamanotch berada
                    var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
                    var workingArea = screen.WorkingArea;

                    var source = PresentationSource.FromVisual(this);
                    double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                    double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                    double areaLeft = workingArea.Left / dpiX;
                    double areaTop = workingArea.Top / dpiY;
                    double areaWidth = workingArea.Width / dpiX;
                    double areaHeight = workingArea.Height / dpiY;

                    // Ukuran ideal tidak melebihi batas layar Windows
                    double horizontalMargin = 20;
                    double verticalMargin = 16;
                    double targetWidth = Math.Min(1080, areaWidth - (horizontalMargin * 2));
                    if (targetWidth < _ytMusicWindow.MinWidth)
                    {
                        targetWidth = Math.Min(_ytMusicWindow.MinWidth, areaWidth - 10);
                    }

                    // Terbuka di bawah tamanotch dan tengah horizontalnya
                    double notchHeight = 64;
                    double notchBottom = Top + notchHeight;
                    if (notchBottom < areaTop) notchBottom = areaTop;

                    double targetTop = notchBottom + 8;
                    double availableHeight = (areaTop + areaHeight) - targetTop - verticalMargin;
                    double targetHeight = Math.Min(720, availableHeight);

                    if (targetHeight < _ytMusicWindow.MinHeight)
                    {
                        targetHeight = Math.Min(_ytMusicWindow.MinHeight, areaHeight - 30);
                        targetTop = Math.Max(areaTop + 4, (areaTop + areaHeight) - targetHeight - verticalMargin);
                    }

                    double targetLeft = areaLeft + (areaWidth - targetWidth) / 2.0;

                    _ytMusicWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _ytMusicWindow.Left = targetLeft;
                    _ytMusicWindow.Top = targetTop;
                    _ytMusicWindow.Width = targetWidth;
                    _ytMusicWindow.Height = targetHeight;
                }
            }

            if (_ytMusicWindow.WindowState == WindowState.Minimized)
            {
                _ytMusicWindow.WindowState = WindowState.Normal;
            }

            _ytMusicWindow.Show();
            _ytMusicWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka pemutar YouTube Music:\n{ex.Message}",
                "TAMANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void OpenSpotify()
    {
        try
        {



            _viewModel.CurrentMode = PanelMode.Compact;

            if (_spotifyWindow == null)
            {
                _spotifyWindow = new SpotifyWindow(_viewModel, _settingsService);
                _spotifyWindow.Closed += (s, e) => _spotifyWindow = null;

                var saved = _settingsService.CurrentSettings;
                if (saved.SpotifyLeft > 0 && saved.SpotifyTop >= 0 &&
                    saved.SpotifyWidth >= _spotifyWindow.MinWidth &&
                    saved.SpotifyHeight >= _spotifyWindow.MinHeight)
                {
                    _spotifyWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _spotifyWindow.Left = saved.SpotifyLeft;
                    _spotifyWindow.Top = saved.SpotifyTop;
                    _spotifyWindow.Width = saved.SpotifyWidth;
                    _spotifyWindow.Height = saved.SpotifyHeight;
                }
                else
                {
                    var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
                    var workingArea = screen.WorkingArea;

                    var source = PresentationSource.FromVisual(this);
                    double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                    double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                    double areaLeft = workingArea.Left / dpiX;
                    double areaTop = workingArea.Top / dpiY;
                    double areaWidth = workingArea.Width / dpiX;
                    double areaHeight = workingArea.Height / dpiY;

                    double horizontalMargin = 20;
                    double verticalMargin = 16;
                    double targetWidth = Math.Min(380, areaWidth - (horizontalMargin * 2));
                    if (targetWidth < _spotifyWindow.MinWidth)
                    {
                        targetWidth = Math.Min(_spotifyWindow.MinWidth, areaWidth - 10);
                    }

                    double notchHeight = 64;
                    double notchBottom = Top + notchHeight;
                    if (notchBottom < areaTop) notchBottom = areaTop;

                    double targetTop = notchBottom + 8;
                    double availableHeight = (areaTop + areaHeight) - targetTop - verticalMargin;
                    double targetHeight = Math.Min(740, availableHeight);

                    if (targetHeight < _spotifyWindow.MinHeight)
                    {
                        targetHeight = Math.Min(_spotifyWindow.MinHeight, areaHeight - 30);
                        targetTop = Math.Max(areaTop + 4, (areaTop + areaHeight) - targetHeight - verticalMargin);
                    }

                    double targetLeft = areaLeft + (areaWidth - targetWidth) / 2.0;

                    _spotifyWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _spotifyWindow.Left = targetLeft;
                    _spotifyWindow.Top = targetTop;
                    _spotifyWindow.Width = targetWidth;
                    _spotifyWindow.Height = targetHeight;
                }
            }

            if (_spotifyWindow.WindowState == WindowState.Minimized)
            {
                _spotifyWindow.WindowState = WindowState.Normal;
            }

            _spotifyWindow.Show();
            _spotifyWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka pemutar Spotify:\n{ex.Message}",
                "TAMANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void OpenJellyfin()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;

            if (_jellyfinWindow == null)
            {
                _jellyfinWindow = new JellyfinWindow(_viewModel, _settingsService);
                _jellyfinWindow.Closed += (s, e) => _jellyfinWindow = null;

                var saved = _settingsService.CurrentSettings;
                PositionPlayerWindow(_jellyfinWindow, saved.JellyfinLeft, saved.JellyfinTop, saved.JellyfinWidth, saved.JellyfinHeight, 390, 780);
            }

            if (_jellyfinWindow.WindowState == WindowState.Minimized)
            {
                _jellyfinWindow.WindowState = WindowState.Normal;
            }

            _jellyfinWindow.Show();
            _jellyfinWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka pemutar Jellyfin:\n{ex.Message}",
                "TAMANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    // Memakai posisi/ukuran tersimpan bila masih valid; kalau tidak, taruh di bawah notch, di tengah layar aktif.
    private void PositionPlayerWindow(Window window, double savedLeft, double savedTop, double savedWidth, double savedHeight, double defaultWidth, double defaultHeight)
    {
        window.WindowStartupLocation = WindowStartupLocation.Manual;

        if (savedLeft > 0 && savedTop >= 0 && savedWidth >= window.MinWidth && savedHeight >= window.MinHeight)
        {
            window.Left = savedLeft;
            window.Top = savedTop;
            window.Width = savedWidth;
            window.Height = savedHeight;
            return;
        }

        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
        var workingArea = screen.WorkingArea;
        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        double areaLeft = workingArea.Left / dpiX;
        double areaTop = workingArea.Top / dpiY;
        double areaWidth = workingArea.Width / dpiX;
        double areaHeight = workingArea.Height / dpiY;

        double width = Math.Min(defaultWidth, areaWidth - 40);
        if (width < window.MinWidth) width = Math.Min(window.MinWidth, areaWidth - 10);

        double top = Math.Max(areaTop, Top + 64) + 8;
        double height = Math.Min(defaultHeight, areaTop + areaHeight - top - 16);
        if (height < window.MinHeight)
        {
            height = Math.Min(window.MinHeight, areaHeight - 30);
            top = Math.Max(areaTop + 4, areaTop + areaHeight - height - 16);
        }

        window.Left = areaLeft + (areaWidth - width) / 2.0;
        window.Top = top;
        window.Width = width;
        window.Height = height;
    }

    public void PauseJellyfinPlayback()
    {
        _ = _jellyfinWindow?.PausePlaybackAsync();
    }

    public void PauseYouTubeMusicPlayback()
    {
        _ytMusicWindow?.PausePlaybackAsync();
    }

    public void PauseSpotifyPlayback()
    {
        _spotifyWindow?.PausePlaybackAsync();
        _ = _mediaService.PauseSessionByKeywordAsync("spotify");
    }

    public void PauseAndHideYouTubeMusic()
    {
        PauseYouTubeMusicPlayback();
        if (_ytMusicWindow != null)
        {
            _ytMusicWindow.Hide();
        }
    }

    public void PauseAndHideSpotify()
    {
        PauseSpotifyPlayback();
        if (_spotifyWindow != null)
        {
            _spotifyWindow.Hide();
        }
    }

    public void PauseSpotify()
    {
        PauseSpotifyPlayback();
    }

    public void OpenFloatingBrowser()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsBrowserWindowOpen = true;

            if (_floatingBrowserWindow == null)
            {
                _floatingBrowserWindow = new FloatingBrowserWindow(_viewModel, _settingsService);
                _floatingBrowserWindow.Closed += (s, e) => _floatingBrowserWindow = null;

                var saved = _settingsService.CurrentSettings;
                if (saved.BrowserLeft > 0 && saved.BrowserTop >= 0 && saved.BrowserWidth >= _floatingBrowserWindow.MinWidth && saved.BrowserHeight >= _floatingBrowserWindow.MinHeight)
                {
                    _floatingBrowserWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _floatingBrowserWindow.Left = saved.BrowserLeft;
                    _floatingBrowserWindow.Top = saved.BrowserTop;
                    _floatingBrowserWindow.Width = saved.BrowserWidth;
                    _floatingBrowserWindow.Height = saved.BrowserHeight;
                }
                else
                {
                    var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
                    var workingArea = screen.WorkingArea;

                    var source = PresentationSource.FromVisual(this);
                    double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                    double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                    double areaLeft = workingArea.Left / dpiX;
                    double areaTop = workingArea.Top / dpiY;
                    double areaWidth = workingArea.Width / dpiX;
                    double areaHeight = workingArea.Height / dpiY;

                    double horizontalMargin = 20;
                    double verticalMargin = 16;
                    double targetWidth = Math.Min(960, areaWidth - (horizontalMargin * 2));
                    if (targetWidth < _floatingBrowserWindow.MinWidth)
                    {
                        targetWidth = Math.Min(_floatingBrowserWindow.MinWidth, areaWidth - 10);
                    }

                    double notchHeight = 90;
                    double notchBottom = Top + notchHeight;
                    if (notchBottom < areaTop) notchBottom = areaTop;

                    double targetTop = notchBottom + 8;
                    double availableHeight = (areaTop + areaHeight) - targetTop - verticalMargin;
                    double targetHeight = Math.Min(540, availableHeight);

                    if (targetHeight < _floatingBrowserWindow.MinHeight)
                    {
                        targetHeight = Math.Min(_floatingBrowserWindow.MinHeight, areaHeight - 30);
                        targetTop = Math.Max(areaTop + 4, (areaTop + areaHeight) - targetHeight - verticalMargin);
                    }

                    double targetLeft = areaLeft + (areaWidth - targetWidth) / 2.0;

                    _floatingBrowserWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                    _floatingBrowserWindow.Left = targetLeft;
                    _floatingBrowserWindow.Top = targetTop;
                    _floatingBrowserWindow.Width = targetWidth;
                    _floatingBrowserWindow.Height = targetHeight;
                }
            }

            if (_floatingBrowserWindow.WindowState == WindowState.Minimized)
            {
                _floatingBrowserWindow.WindowState = WindowState.Normal;
            }

            _floatingBrowserWindow.Show();
            _floatingBrowserWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka browser mengambang:\n{ex.Message}",
                "TAMANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void OpenRadio()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsRadioWindowOpen = true;

            if (_radioWindow == null)
            {
                _radioWindow = new RadioWindow(_viewModel, _settingsService);
                _radioWindow.Closed += (s, e) => _radioWindow = null;
            }

            if (_radioWindow.WindowState == WindowState.Minimized)
            {
                _radioWindow.WindowState = WindowState.Normal;
            }

            WindowAnimator.Cancel(_radioWindow);
            _radioWindow.ShowFromNotch();
            _radioWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka Radio Online:\n{ex.Message}",
                "TAMANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void OpenQuran()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsQuranWindowOpen = true;

            if (_quranWindow == null)
            {
                _quranWindow = new QuranWindow(_viewModel, _settingsService);
                _quranWindow.Closed += (s, e) => _quranWindow = null;
            }

            if (_quranWindow.WindowState == WindowState.Minimized)
            {
                _quranWindow.WindowState = WindowState.Normal;
            }

            WindowAnimator.Cancel(_quranWindow);
            _quranWindow.ShowFromNotch();
            _quranWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka pemutar Al-Qur'an:\n{ex.Message}",
                "TAMANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    public void OpenLiveTv()
    {
        try
        {
            _viewModel.CurrentMode = PanelMode.Compact;
            _viewModel.IsLiveTvWindowOpen = true;

            if (_liveTvWindow == null)
            {
                _liveTvWindow = new LiveTvWindow(_viewModel, _settingsService);
                _liveTvWindow.Closed += (s, e) => _liveTvWindow = null;
            }

            if (_liveTvWindow.WindowState == WindowState.Minimized)
            {
                _liveTvWindow.WindowState = WindowState.Normal;
            }

            _liveTvWindow.Show();
            _liveTvWindow.Activate();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Tidak dapat membuka Live TV:\n{ex.Message}",
                "TAMANOTCH - Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Island_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
            if (_viewModel.IsCompactMode)
            {
                WindowHelper.CenterAtTopBezel(this);
            }
            else
            {
                WindowHelper.DockToTopBezel(this);
            }
            _settingsService.CurrentSettings.PanelLeft = Left;
            _settingsService.CurrentSettings.PanelTop = Top;
            _settingsService.Save();
        }
    }

    private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _viewModel.OnMouseEnter();
    }

    private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _viewModel.OnMouseLeave();
    }

    private void ProgressBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
    }

    private void ProgressBar_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider slider)
        {
            _viewModel.SeekCommand.Execute(slider.Value);
        }
    }

    private void ToggleClickThrough_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsClickThrough = !_viewModel.IsClickThrough;
        WindowHelper.SetClickThrough(this, _viewModel.IsClickThrough);
    }

    public void ExitApplication()
    {
        _isRealExit = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isRealExit)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _ytMusicWindow?.ForceClose();
        _spotifyWindow?.ForceClose();
        _jellyfinWindow?.ForceClose();
        _floatingBrowserWindow?.ForceClose();
        _radioWindow?.ForceClose();
        _quranWindow?.ForceClose();
        _liveTvWindow?.ForceClose();
        _musicWindow?.ForceClose();
        _viewModel.RadioService.Dispose();
        _viewModel.QuranService.Dispose();
        _viewModel.LiveTvService.Dispose();
        _viewModel.BmkgService.Dispose();
        _bmkgWindow?.ForceClose();
        _todoWindow?.ForceClose();
        _prayerWindow?.Close();
        _hotkeyManager?.Dispose();
        _trayService.Dispose();
        base.OnClosing(e);
        System.Windows.Application.Current.Shutdown();
    }
}
