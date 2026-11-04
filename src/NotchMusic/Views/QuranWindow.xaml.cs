using System.Windows;
using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;

namespace NotchMusic.Views;

public partial class QuranWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private bool _isForceClose;

    public QuranWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;

        Loaded += QuranWindow_Loaded;
        LocationChanged += QuranWindow_LocationChanged;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsQuranWindowCompact))
            {
                Dispatcher.Invoke(() => UpdateCompactMode(_viewModel.IsQuranWindowCompact));
            }
        };
    }

    private void QuranWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        var s = _settingsService.CurrentSettings;
        if (s.QuranWindowLeft > 0 && s.QuranWindowTop > 0)
        {
            Left = s.QuranWindowLeft;
            Top = s.QuranWindowTop;
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

        UpdateCompactMode(_viewModel.IsQuranWindowCompact);
        WindowHelper.EnsureInVisibleScreen(this);
    }

    private void UpdateCompactMode(bool isCompact)
    {
        SizeToContent = SizeToContent.Height;
        MinWidth = isCompact ? 280 : 340;
        MinHeight = 0;
        Width = isCompact ? 360 : 410;

        Dispatcher.InvokeAsync(() => WindowHelper.EnsureInVisibleScreen(this));
    }

    private void QuranWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top > 0)
        {
            _settingsService.CurrentSettings.QuranWindowLeft = Left;
            _settingsService.CurrentSettings.QuranWindowTop = Top;
            _settingsService.Save();
        }
    }

    private void ProgressBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (QuranProgressBar.ActualWidth <= 0) return;
        _viewModel.SeekQuran(e.GetPosition(QuranProgressBar).X / QuranProgressBar.ActualWidth);
        e.Handled = true;
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
        _viewModel.IsQuranWindowOpen = false;
        this.HideToNotch();
    }

    public void ForceClose()
    {
        _isForceClose = true;
        Close();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isForceClose)
        {
            e.Cancel = true;
            _viewModel.IsQuranWindowOpen = false;
            this.HideToNotch();
            return;
        }

        base.OnClosing(e);
    }
}
