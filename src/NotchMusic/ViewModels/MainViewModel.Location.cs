using NotchMusic.Services.Location;

namespace NotchMusic.ViewModels;

public partial class MainViewModel
{
    private readonly ILocationService _locationService;

    public ILocationService LocationService => _locationService;

    private void OnLocationChanged()
    {
        OnPropertyChanged(nameof(CurrentPrayerSchedule));
        OnPropertyChanged(nameof(NextPrayerName));
        OnPropertyChanged(nameof(NextPrayerTimeFormatted));
        OnPropertyChanged(nameof(NextPrayerCountdownBadge));
        OnPropertyChanged(nameof(PrayerScheduleTooltip));
        OnPropertyChanged(nameof(SelectedBmkgCity));
        OnPropertyChanged(nameof(BmkgCustomAdm4Code));
        OnPropertyChanged(nameof(BmkgActiveAdm4Text));
        OnPropertyChanged(nameof(BmkgWeatherCityText));
        _ = _bmkgService.RefreshWeatherAsync();
    }
}
