using Button = System.Windows.Controls.Button;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Settings;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using ContextMenu = System.Windows.Controls.ContextMenu;
using MenuItem = System.Windows.Controls.MenuItem;

namespace NotchMusic.Views;

/// <summary>Konfigurasi satu pemutar web (nama, profil WebView2, warna, alamat awal, ukuran bawaan).</summary>
public sealed class WebPlayerOptions
{
    public required string PlayerName { get; init; }
    public required string ProfileFolderName { get; init; }
    public required string PlayingMessage { get; init; }
    public required string AccentHex { get; init; }
    public required string MenuBackgroundHex { get; init; }
    public required string MenuBorderHex { get; init; }
    public double DefaultWidth { get; init; } = 390;
    public double DefaultHeight { get; init; } = 780;
}

/// <summary>
/// Perilaku bersama jendela pemutar berbasis WebView2: profil login permanen, hook "sedang diputar" untuk saling jeda,
/// tombol navigasi, pin selalu-di-atas, ukuran (termasuk ukuran HP), zoom, mode tampilan HP, dan penyimpanan posisi.
/// Elemen visual (tombol, bingkai, WebView) didefinisikan di XAML turunan dan diberikan lewat properti abstrak.
/// </summary>
public abstract class WebPlayerWindowBase : Window
{
    private const double NarrowTitleBarWidth = 460;

    private ISettingsService? _settingsService;
    private bool _isForceClose;
    private bool _isInitialized;

    // --- disediakan turunan ---
    protected abstract WebPlayerOptions Options { get; }
    protected abstract WebView2 Web { get; }
    protected abstract TextBlock StatusLabel { get; }
    protected abstract FrameworkElement LoadingHost { get; }
    protected abstract Button BackNav { get; }
    protected abstract Button ForwardNav { get; }
    protected abstract Button PinToggle { get; }
    protected abstract Button MaximizeToggle { get; }
    protected abstract Border RootFrame { get; }
    protected abstract FrameworkElement GripChipElement { get; }
    protected abstract FrameworkElement TitleTextElement { get; }

    protected abstract Uri HomeUri { get; }
    protected abstract (double Left, double Top, double Width, double Height, bool Topmost) LoadBounds(AppSettings settings);
    protected abstract void StoreBounds(AppSettings settings, double left, double top, double width, double height);
    protected abstract void StoreTopmost(AppSettings settings, bool topmost);
    protected abstract bool MobileMode { get; set; }

    /// <summary>Dipanggil turunan setelah InitializeComponent.</summary>
    protected void InitializePlayer(ISettingsService? settingsService)
    {
        _settingsService = settingsService;

        try
        {
            Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/NotchMusic;component/Assets/app.ico"));
        }
        catch { }

        Width = Options.DefaultWidth;
        Height = Options.DefaultHeight;

        if (_settingsService != null)
        {
            var b = LoadBounds(_settingsService.CurrentSettings);
            Topmost = b.Topmost;
            if (b.Width >= MinWidth && b.Height >= MinHeight)
            {
                Width = b.Width;
                Height = b.Height;
            }
            if (b.Left > 0 && b.Top >= 0)
            {
                Left = b.Left;
                Top = b.Top;
            }
        }

        UpdatePinVisuals();
        Loaded += async (s, e) =>
        {
            if (_isInitialized) return;
            _isInitialized = true;
            await InitializeWebViewAsync();
        };
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            StatusLabel.Text = $"Menyiapkan browser {Options.PlayerName}...";
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var userDataFolder = Path.Combine(localAppData, "TAMANOTCH", Options.ProfileFolderName);
            Directory.CreateDirectory(userDataFolder);

            var envOptions = new CoreWebView2EnvironmentOptions("--autoplay-policy=no-user-gesture-required");
            var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder, options: envOptions);
            await Web.EnsureCoreWebView2Async(env);

            var core = Web.CoreWebView2;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;
            ApplyUserAgent();

            core.WebMessageReceived += (s, ev) =>
            {
                try
                {
                    if (ev.TryGetWebMessageAsString() == Options.PlayingMessage) OnPlaybackStarted();
                }
                catch { }
            };

            // 'play' tidak menggelembung, tetapi fase capture di document menangkapnya dari elemen media mana pun
            // (termasuk yang dibuat belakangan oleh pemutar web).
            await core.AddScriptToExecuteOnDocumentCreatedAsync($@"
                (function() {{
                    document.addEventListener('play', function(e) {{
                        var t = e.target;
                        if (t && (t.tagName === 'VIDEO' || t.tagName === 'AUDIO') && window.chrome && window.chrome.webview) {{
                            window.chrome.webview.postMessage('{Options.PlayingMessage}');
                        }}
                    }}, true);
                }})();
            ");

            Web.NavigationStarting += (s, ev) => Dispatcher.Invoke(() => StatusLabel.Text = $"Memuat {Options.PlayerName}...");
            Web.NavigationCompleted += (s, ev) => Dispatcher.Invoke(() =>
            {
                LoadingHost.Visibility = Visibility.Collapsed;
                Web.Visibility = Visibility.Visible;
                StatusLabel.Text = "Tersambung ke TAMANOTCH";
                BackNav.IsEnabled = Web.CanGoBack;
                ForwardNav.IsEnabled = Web.CanGoForward;
            });

            Web.Source = HomeUri;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "Gagal memuat WebView2";
            System.Windows.MessageBox.Show(
                $"Gagal menginisialisasi Microsoft Edge WebView2 untuk {Options.PlayerName}:\n{ex.Message}\n\nPastikan Microsoft Edge WebView2 Runtime terpasang di sistem Anda.",
                "TAMANOTCH - WebView2 Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>Dipanggil saat media mulai diputar di jendela ini (untuk saling jeda dengan pemutar lain).</summary>
    protected virtual void OnPlaybackStarted() { }

    // --- tampilan HP (User-Agent mobile) ---
    private void ApplyUserAgent()
    {
        var core = Web?.CoreWebView2;
        if (core == null) return;

        core.Settings.UserAgent = MobileMode
            ? MobileUserAgent.Build(core.Environment.BrowserVersionString)
            : string.Empty;   // string kosong = bawaan WebView2
    }

    protected void SetMobileMode(bool enabled)
    {
        if (MobileMode == enabled) return;

        MobileMode = enabled;
        ApplyUserAgent();
        Web?.Reload();
    }

    // --- jendela ---
    protected void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        try
        {
            if (e.ClickCount == 2) ToggleMaximize(); else DragMove();
        }
        catch { }
    }

    protected void PinButton_Click(object sender, RoutedEventArgs e)
    {
        Topmost = !Topmost;
        UpdatePinVisuals();
        if (_settingsService != null)
        {
            StoreTopmost(_settingsService.CurrentSettings, Topmost);
            _settingsService.Save();
        }
    }

    private void UpdatePinVisuals()
    {
        if (PinToggle == null) return;
        if (Topmost)
        {
            PinToggle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Options.AccentHex));
            PinToggle.Foreground = System.Windows.Media.Brushes.White;
            PinToggle.ToolTip = "Mode Mengambang AKTIF (Selalu di atas). Klik untuk lepas.";
        }
        else
        {
            PinToggle.Background = System.Windows.Media.Brushes.Transparent;
            PinToggle.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC"));
            PinToggle.ToolTip = "Mode Mengambang NONAKTIF. Klik agar selalu di atas.";
        }
    }

    protected void BackButton_Click(object sender, RoutedEventArgs e) { if (Web.CanGoBack) Web.GoBack(); }
    protected void ForwardButton_Click(object sender, RoutedEventArgs e) { if (Web.CanGoForward) Web.GoForward(); }
    protected void ReloadButton_Click(object sender, RoutedEventArgs e) => Web.Reload();
    protected void HomeButton_Click(object sender, RoutedEventArgs e) => Web.Source = HomeUri;
    protected void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    protected void MaximizeButton_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    // Menutup jendela hanya menyembunyikannya agar audio tetap berputar.
    protected void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    protected void DismissTip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.Parent is FrameworkElement parent && parent.Parent is FrameworkElement banner)
        {
            banner.Visibility = Visibility.Collapsed;
        }
    }

    private void ToggleMaximize()
        => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        bool max = WindowState == WindowState.Maximized;
        if (MaximizeToggle != null)
        {
            MaximizeToggle.Content = max ? "🗗" : "🗖";
            MaximizeToggle.ToolTip = max ? "Kembalikan Ukuran" : "Perbesar Jendela (Maximize)";
        }
        if (RootFrame != null)
        {
            RootFrame.Padding = max ? new Thickness(0) : new Thickness(5);
            RootFrame.CornerRadius = max ? new CornerRadius(0) : new CornerRadius(8);
            RootFrame.BorderThickness = max ? new Thickness(0) : new Thickness(1.5);
        }
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        SaveWindowBounds();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        ApplyResponsiveLayout();
        SaveWindowBounds();
    }

    // Di lebar sempit judul-bar meluap sehingga tombol tutup terpotong; sembunyikan elemen non-esensial.
    private void ApplyResponsiveLayout()
    {
        if (GripChipElement == null) return;

        var compact = ActualWidth < NarrowTitleBarWidth ? Visibility.Collapsed : Visibility.Visible;
        GripChipElement.Visibility = compact;
        TitleTextElement.Visibility = compact;
        MaximizeToggle.Visibility = compact;
    }

    private void SaveWindowBounds()
    {
        if (!_isInitialized || WindowState != WindowState.Normal || _settingsService == null) return;

        StoreBounds(_settingsService.CurrentSettings, Left, Top, Width, Height);
        _settingsService.Save();
    }

    protected void SetWindowPreset(double targetWidth, double targetHeight)
    {
        if (WindowState == WindowState.Maximized) WindowState = WindowState.Normal;

        Width = Math.Max(MinWidth, targetWidth);
        Height = Math.Max(MinHeight, targetHeight);

        // Pastikan posisi jendela tetap dalam batas layar aktif.
        var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)Left, (int)Top));
        var area = screen.WorkingArea;
        var source = PresentationSource.FromVisual(this);
        double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

        double areaLeft = area.Left / dpiX, areaTop = area.Top / dpiY;
        double areaRight = area.Right / dpiX, areaBottom = area.Bottom / dpiY;

        if (Left + Width > areaRight) Left = Math.Max(areaLeft, areaRight - Width - 10);
        if (Top + Height > areaBottom) Top = Math.Max(areaTop, areaBottom - Height - 10);

        SaveWindowBounds();
    }

    // --- menu ukuran ---
    protected void PresetSizeButton_Click(object sender, RoutedEventArgs e)
    {
        var white = System.Windows.Media.Brushes.White;
        var menu = new ContextMenu
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Options.MenuBackgroundHex)),
            Foreground = white,
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Options.MenuBorderHex)),
            BorderThickness = new Thickness(1)
        };

        MenuItem Item(string header, Action onClick)
        {
            var item = new MenuItem { Header = header, Foreground = white, Padding = new Thickness(10, 6, 10, 6) };
            item.Click += (s, ev) => onClick();
            return item;
        }

        menu.Items.Add(PhoneSizePresets.CreateMenu(
            white,
            SetWindowPreset,
            () => Web.ActualWidth > 0 && Web.ActualHeight > 0
                ? (Math.Max(0, ActualWidth - Web.ActualWidth), Math.Max(0, ActualHeight - Web.ActualHeight))
                : (16, 72),
            () => WindowHelper.GetWorkAreaSize(this),
            MinWidth, MinHeight,
            () =>
            {
                var area = WindowHelper.GetWorkAreaSize(this);
                SetWindowPreset(Math.Min(ActualHeight, area.Width - 20), Math.Min(ActualWidth, area.Height - 20));
            }));

        var sizes = new (string Label, double W, double H)[]
        {
            ("📻 Ringkas (480 × 640)", 480, 640),
            ("💻 Sedang (640 × 720)", 640, 720),
            ("🖥️ Standar Layar Lebar (1080 × 720)", 1080, 720),
            ("🎬 Layar Lebar HD (1280 × 800)", 1280, 800)
        };
        foreach (var s in sizes) menu.Items.Add(Item(s.Label, () => SetWindowPreset(s.W, s.H)));

        menu.Items.Add(new Separator());
        menu.Items.Add(Item("➕ Perbesar Ukuran (+15%)", () => SetWindowPreset(Width * 1.15, Height * 1.15)));
        menu.Items.Add(Item("➖ Perkecil Ukuran (-15%)", () => SetWindowPreset(Width * 0.85, Height * 0.85)));
        menu.Items.Add(Item($"🔍 Reset Ukuran Standar ({Options.DefaultWidth:0} × {Options.DefaultHeight:0})", () => SetWindowPreset(Options.DefaultWidth, Options.DefaultHeight)));

        var zoomMenu = new MenuItem { Header = "🔎 Skala Konten (Zoom)", Foreground = white, Padding = new Thickness(10, 6, 10, 6) };
        foreach (var (label, factor) in new[] { ("75%", 0.75), ("90%", 0.90), ("100% (Normal)", 1.0), ("110%", 1.10), ("125%", 1.25) })
        {
            var zi = new MenuItem { Header = label, Foreground = white, Padding = new Thickness(8, 4, 8, 4) };
            zi.Click += (s, ev) =>
            {
                if (Web?.CoreWebView2 == null) return;
                Web.ZoomFactor = factor;
            };
            zoomMenu.Items.Add(zi);
        }
        menu.Items.Add(zoomMenu);

        menu.Items.Add(new Separator());
        var mobileItem = new MenuItem
        {
            Header = "📱 Tampilan HP (mode mobile)",
            Foreground = white,
            Padding = new Thickness(10, 6, 10, 6),
            IsCheckable = true,
            IsChecked = MobileMode
        };
        mobileItem.Click += (s, ev) => SetMobileMode(mobileItem.IsChecked);
        menu.Items.Add(mobileItem);

        menu.PlacementTarget = sender as UIElement;
        menu.IsOpen = true;
    }

    // --- kontrol dari luar ---
    public void ForceClose()
    {
        _isForceClose = true;
        Close();
    }

    /// <summary>Jeda semua media yang sedang diputar di halaman.</summary>
    public async Task PausePlaybackAsync()
    {
        try
        {
            if (Web?.CoreWebView2 != null)
            {
                await Web.CoreWebView2.ExecuteScriptAsync(
                    "document.querySelectorAll('audio, video').forEach(function(m){ if (!m.paused) m.pause(); });");
            }
        }
        catch { }
    }

    public void PauseAndHide()
    {
        _ = PausePlaybackAsync();
        if (IsVisible) Hide();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isForceClose)
        {
            // Jangan matikan proses pemutar, cukup sembunyikan agar audio tetap berputar.
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }
}
