using System;
using System.Windows;
using System.Windows.Input;
using TamaNotch.Helpers;
using TamaNotch.Services.Settings;
using TamaNotch.ViewModels;

namespace TamaNotch.Views;

public partial class PrayerWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ISettingsService _settingsService;

    public PrayerWindow(MainViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = _viewModel;

        Loaded += PrayerWindow_Loaded;
        LocationChanged += PrayerWindow_LocationChanged;
        SizeChanged += PrayerWindow_SizeChanged;

        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsPrayerWindowCompact))
            {
                Dispatcher.Invoke(() => UpdateCompactMode(_viewModel.IsPrayerWindowCompact));
            }
        };
    }

    private void PrayerWindow_Loaded(object sender, RoutedEventArgs e)
    {
        WindowHelper.SetToolWindow(this);

        var s = _settingsService.CurrentSettings;
        if (s.PrayerWindowLeft > 0 && s.PrayerWindowTop > 0)
        {
            Left = s.PrayerWindowLeft;
            Top = s.PrayerWindowTop;
        }
        else
        {
            var primary = System.Windows.Forms.Screen.PrimaryScreen;
            if (primary != null)
            {
                var workArea = primary.WorkingArea;
                Left = Math.Max(workArea.Left + 20, workArea.Left + 60);
                Top = workArea.Top + 60;
            }
        }

        UpdateCompactMode(_viewModel.IsPrayerWindowCompact);
        WindowHelper.EnsureInVisibleScreen(this);
    }

    private void UpdateCompactMode(bool isCompact)
    {
        SizeToContent = SizeToContent.Height;
        if (isCompact)
        {
            MinWidth = 240;
            MinHeight = 0;
            Width = 320;
        }
        else
        {
            MinWidth = 300;
            MinHeight = 0;
            Width = 360;
        }

        Dispatcher.InvokeAsync(() => WindowHelper.EnsureInVisibleScreen(this));
    }

    private void PrayerWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Normal && Left > 0 && Top > 0)
        {
            _settingsService.CurrentSettings.PrayerWindowLeft = Left;
            _settingsService.CurrentSettings.PrayerWindowTop = Top;
            _settingsService.Save();
        }
    }

    private void PrayerWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_viewModel.IsPrayerWindowCompact && WindowState == WindowState.Normal && ActualWidth >= MinWidth)
        {
            _settingsService.CurrentSettings.PrayerWindowWidth = ActualWidth;
            _settingsService.CurrentSettings.PrayerWindowHeight = ActualHeight;
            _settingsService.Save();
        }
    }

    private void Border_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void Panel_CloseRequested(object? sender, EventArgs e)
    {
        // Terpasang di notch: IsPrayerWindowOpen juga mengatur pill dan panel sholat di notch utama, jadi hanya jendela yang disembunyikan.
        if (_viewModel.IsPrayerDetached) _viewModel.IsPrayerWindowOpen = false;
        this.HideToNotch();
    }
}
