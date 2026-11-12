using Button = System.Windows.Controls.Button;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;
using TamaNotch.Helpers;
using TamaNotch.Models;
using TamaNotch.Services.Settings;
using TamaNotch.ViewModels;

namespace TamaNotch.Views;

/// <summary>Pemutar Jellyfin (server media pribadi) di jendela WebView2 dengan login permanen.</summary>
public partial class JellyfinWindow : WebPlayerWindowBase
{
    private static readonly WebPlayerOptions PlayerOptions = new()
    {
        PlayerName = "Jellyfin",
        ProfileFolderName = "JellyfinWebView2Profile",
        PlayingMessage = "jellyfin_playing",
        AccentHex = "#AA5CC3",
        MenuBackgroundHex = "#13151F",
        MenuBorderHex = "#2A2D45",
        DefaultWidth = 390,
        DefaultHeight = 780
    };

    private readonly MainViewModel? _viewModel;
    private readonly ISettingsService? _settingsService;

    public JellyfinWindow(MainViewModel? viewModel = null, ISettingsService? settingsService = null)
    {
        _viewModel = viewModel;
        _settingsService = settingsService;
        InitializeComponent();
        InitializePlayer(settingsService);
    }

    protected override WebPlayerOptions Options => PlayerOptions;
    protected override WebView2 Web => webView;
    protected override TextBlock StatusLabel => StatusText;
    protected override FrameworkElement LoadingHost => LoadingPanel;
    protected override Button BackNav => BackButton;
    protected override Button ForwardNav => ForwardButton;
    protected override Button PinToggle => PinButton;
    protected override Button MaximizeToggle => MaximizeButton;
    protected override Border RootFrame => RootBorder;
    protected override FrameworkElement GripChipElement => GripChip;
    protected override FrameworkElement TitleTextElement => TitleText;

    protected override Uri HomeUri => JellyfinAddress.Normalize(_settingsService?.CurrentSettings.JellyfinUrl);

    protected override (double Left, double Top, double Width, double Height, bool Topmost) LoadBounds(AppSettings s)
        => (s.JellyfinLeft, s.JellyfinTop, s.JellyfinWidth, s.JellyfinHeight, s.JellyfinAlwaysOnTop);

    protected override void StoreBounds(AppSettings s, double left, double top, double width, double height)
    {
        s.JellyfinLeft = left;
        s.JellyfinTop = top;
        s.JellyfinWidth = width;
        s.JellyfinHeight = height;
    }

    protected override void StoreTopmost(AppSettings s, bool topmost) => s.JellyfinAlwaysOnTop = topmost;

    protected override bool MobileMode
    {
        get => _settingsService?.CurrentSettings.JellyfinMobileMode ?? true;
        set
        {
            if (_settingsService == null) return;
            _settingsService.CurrentSettings.JellyfinMobileMode = value;
            _settingsService.Save();
        }
    }

    protected override void OnPlaybackStarted() => _viewModel?.NotifyJellyfinPlaying();

    /// <summary>Arahkan ke alamat server yang tersimpan di pengaturan (dipakai setelah alamat diubah).</summary>
    public void NavigateHome()
    {
        if (webView.CoreWebView2 != null) webView.Source = HomeUri;
    }
}
