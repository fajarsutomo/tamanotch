using System.Windows.Input;

namespace TamaNotch.ViewModels;

public partial class MainViewModel
{
    public event Action? RequestOpenFloatingBrowser;

    public bool IsBrowserWindowOpen
    {
        get => Settings.IsBrowserWindowOpen;
        set
        {
            if (Settings.IsBrowserWindowOpen != value)
            {
                Settings.IsBrowserWindowOpen = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsBrowserWindowOpen));
                OnPropertyChanged(nameof(IsBrowserClosed));
                OnPropertyChanged(nameof(BrowserStatusSummary));
                OnPropertyChanged(nameof(BrowserButtonActionText));
                OnPropertyChanged(nameof(IsNotchActive));
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool IsBrowserClosed => !IsBrowserWindowOpen;

    public string BrowserStatusSummary => IsBrowserWindowOpen ? "Jendela Melayang Aktif" : "Jendela Tertutup";

    public string BrowserButtonActionText => IsBrowserWindowOpen ? "Tutup" : "Buka";

    public ICommand OpenFloatingBrowserCommand { get; }

    public ICommand ToggleBrowserCommand { get; }

    public ICommand OpenBrowserCommand { get; }

    public ICommand CloseBrowserCommand { get; }
}
