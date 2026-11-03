using NotchMusic.Services.Bmkg;

namespace NotchMusic.ViewModels;

public partial class SettingsViewModel
{
    private readonly IBmkgService? _bmkgService;

    // Cuaca & Gempa Bumi (BMKG) Properties
    public bool EnableBmkg
    {
        get => Settings.EnableBmkg;
        set
        {
            if (Settings.EnableBmkg != value)
            {
                Settings.EnableBmkg = value;
                _settingsService.Save();
                OnPropertyChanged();
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
                OnPropertyChanged();
            }
        }
    }

    public bool BmkgNotifyNewEarthquake
    {
        get => Settings.BmkgNotifyNewEarthquake;
        set
        {
            if (Settings.BmkgNotifyNewEarthquake != value)
            {
                Settings.BmkgNotifyNewEarthquake = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool BmkgNotifyExtremeWeather
    {
        get => Settings.BmkgNotifyExtremeWeather;
        set
        {
            if (Settings.BmkgNotifyExtremeWeather != value)
            {
                Settings.BmkgNotifyExtremeWeather = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }
}
