using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NotchMusic.Helpers;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class ClaudeUsageWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(30) };
    private bool _isForceClose;

    public ClaudeUsageWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;

        Loaded += ClaudeUsageWindow_Loaded;
        LocationChanged += ClaudeUsageWindow_LocationChanged;

        // Hitung mundur reset dan "diperbarui X menit lalu" ikut berjalan selama jendela terlihat.
        _clock.Tick += (s, e) => _viewModel.RaiseClaudeUsageChanged();
        IsVisibleChanged += (s, e) =>
        {
            if (IsVisible)
            {
                _viewModel.RaiseClaudeUsageChanged();
                _clock.Start();
            }
            else
            {
                _clock.Stop();
            }
        };
    }

    private void ClaudeUsageWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        var s = _settingsService.CurrentSettings;
        if (s.ClaudeUsageWindowLeft > 0 && s.ClaudeUsageWindowTop > 0)
        {
            Left = s.ClaudeUsageWindowLeft;
            Top = s.ClaudeUsageWindowTop;
        }
        else
        {
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            if (primary != null)
            {
                var workArea = primary.WorkingArea;
                var source = PresentationSource.FromVisual(this);
                double dpiX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
                double dpiY = source?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;

                Left = workArea.Left / dpiX + (workArea.Width / dpiX - Width) / 2.0;
                Top = workArea.Top / dpiY + 68;
            }
        }

        WindowHelper.EnsureInVisibleScreen(this);
    }

    private void ClaudeUsageWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top > 0)
        {
            _settingsService.CurrentSettings.ClaudeUsageWindowLeft = Left;
            _settingsService.CurrentSettings.ClaudeUsageWindowTop = Top;
            _settingsService.Save();
        }
    }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try
            {
                DragMove();
            }
            catch { }
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsClaudeUsageWindowOpen = false;
        this.HideToNotch();
    }

    public void ForceClose()
    {
        _isForceClose = true;
        _clock.Stop();
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isForceClose)
        {
            e.Cancel = true;
            _viewModel.IsClaudeUsageWindowOpen = false;
            this.HideToNotch();
            return;
        }

        base.OnClosing(e);
    }
}
