using NotchMusic.Models;
using NotchMusic.Services.Prayer;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Location;

/// <summary>Sisi UI yang memulai perubahan lokasi; menentukan apa yang ikut berubah bila sinkronisasi dimatikan.</summary>
public enum LocationOrigin
{
    Prayer,
    Bmkg
}

/// <summary>
/// Satu pintu perubahan lokasi. Menjaga lokasi waktu sholat, kota BMKG, dan kode ADM4 tetap konsisten
/// sehingga data cuaca lama tidak lagi menimpa kota yang baru dipilih.
/// </summary>
public interface ILocationService
{
    /// <summary>Dipicu setelah lokasi tersimpan; pemanggil menyegarkan tampilan dan cuaca.</summary>
    event Action? LocationChanged;

    /// <summary>Pilih lokasi tingkat kota (kode ADM4 kota diambil dari <see cref="CityLocation.BmkgAdm4Code"/>).</summary>
    void ApplyCity(CityLocation city, LocationOrigin origin);

    /// <summary>Pilih lokasi presisi (kelurahan/desa) dengan kode ADM4 sendiri.</summary>
    void ApplyPrecise(string name, string adm4Code, double latitude, double longitude, double timezone, LocationOrigin origin = LocationOrigin.Bmkg);
}

public class LocationService : ILocationService
{
    private readonly ISettingsService _settingsService;
    private readonly IPrayerService _prayerService;
    private readonly IPrayerReminderService? _reminderService;

    public event Action? LocationChanged;

    public LocationService(
        ISettingsService settingsService,
        IPrayerService prayerService,
        IPrayerReminderService? reminderService = null)
    {
        _settingsService = settingsService;
        _prayerService = prayerService;
        _reminderService = reminderService;
    }

    public void ApplyCity(CityLocation city, LocationOrigin origin)
    {
        var settings = _settingsService.CurrentSettings;
        bool follow = settings.BmkgFollowPrayerCity;
        bool updatePrayer = origin == LocationOrigin.Prayer || follow;
        bool updateBmkg = origin == LocationOrigin.Bmkg || follow;

        if (updateBmkg)
        {
            settings.BmkgSelectedCity = city.Name;
            settings.BmkgCustomAdm4Code = city.BmkgAdm4Code;
        }

        if (updatePrayer)
        {
            _prayerService.UpdateLocation(city.Name, city.Latitude, city.Longitude, city.Timezone);
            _reminderService?.RefreshSchedule();
        }

        _settingsService.Save();
        LocationChanged?.Invoke();
    }

    public void ApplyPrecise(string name, string adm4Code, double latitude, double longitude, double timezone, LocationOrigin origin = LocationOrigin.Bmkg)
    {
        var settings = _settingsService.CurrentSettings;
        bool follow = settings.BmkgFollowPrayerCity;

        if (origin == LocationOrigin.Bmkg || follow)
        {
            settings.BmkgCustomAdm4Code = adm4Code;
            settings.BmkgSelectedCity = name;
        }

        bool hasCoordinates = Math.Abs(latitude) > 0.001 && Math.Abs(longitude) > 0.001;
        if ((origin == LocationOrigin.Prayer || follow) && hasCoordinates)
        {
            _prayerService.UpdateLocation(name, latitude, longitude, timezone);
            _reminderService?.RefreshSchedule();
        }

        _settingsService.Save();
        LocationChanged?.Invoke();
    }
}
