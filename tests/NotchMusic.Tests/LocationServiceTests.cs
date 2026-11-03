using System;
using System.Linq;
using NotchMusic.Models;
using NotchMusic.Services.Location;
using NotchMusic.Services.Prayer;
using NotchMusic.Services.Settings;
using NotchMusic.ViewModels;
using Xunit;

namespace NotchMusic.Tests;

public class LocationServiceTests
{
    private class FakeSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; } = new();
        public int SaveCount { get; private set; }
        public void Save() => SaveCount++;
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => System.IO.Path.GetTempPath();
        public string GetLyricsCachePath() => System.IO.Path.GetTempPath();
        public void ClearLyricsCache() { }
    }

    private class FakeReminderService : IPrayerReminderService
    {
        public int RefreshCount { get; private set; }
        public event Action<PrayerNotificationEvent>? ReminderTriggered { add { } remove { } }
        public event Action<PrayerSchedule>? ScheduleUpdated { add { } remove { } }
        public PrayerSchedule CurrentSchedule => throw new NotSupportedException();
        public void Start() { }
        public void Stop() { }
        public void RefreshSchedule() => RefreshCount++;
        public void Dispose() { }
    }

    private static (LocationService Service, FakeSettingsService Settings, FakeReminderService Reminder) Create(bool follow)
    {
        var settings = new FakeSettingsService();
        settings.CurrentSettings.BmkgFollowPrayerCity = follow;
        var reminder = new FakeReminderService();
        var service = new LocationService(settings, new PrayerService(settings), reminder);
        return (service, settings, reminder);
    }

    private static CityLocation Bandung() =>
        CityLocation.GetIndonesianCities().First(c => c.Name == "Bandung");

    [Fact]
    public void ApplyCity_FromPrayer_WithFollow_UpdatesPrayerAndBmkgTogether()
    {
        var (service, settings, reminder) = Create(follow: true);
        var city = Bandung();
        int raised = 0;
        service.LocationChanged += () => raised++;

        service.ApplyCity(city, LocationOrigin.Prayer);

        var s = settings.CurrentSettings;
        Assert.Equal("Bandung", s.PrayerCity);
        Assert.Equal(city.Latitude, s.PrayerLatitude);
        Assert.Equal(city.Longitude, s.PrayerLongitude);
        Assert.Equal("Bandung", s.BmkgSelectedCity);
        Assert.Equal(city.BmkgAdm4Code, s.BmkgCustomAdm4Code);
        Assert.Equal(1, reminder.RefreshCount);
        Assert.Equal(1, raised);
    }

    [Fact]
    public void ApplyCity_FromPrayer_WithoutFollow_LeavesBmkgUntouched()
    {
        var (service, settings, _) = Create(follow: false);
        settings.CurrentSettings.BmkgCustomAdm4Code = "99.99.99.9999";
        settings.CurrentSettings.BmkgSelectedCity = "Medan";

        service.ApplyCity(Bandung(), LocationOrigin.Prayer);

        Assert.Equal("Bandung", settings.CurrentSettings.PrayerCity);
        Assert.Equal("99.99.99.9999", settings.CurrentSettings.BmkgCustomAdm4Code);
        Assert.Equal("Medan", settings.CurrentSettings.BmkgSelectedCity);
    }

    [Fact]
    public void ApplyCity_FromBmkg_WithoutFollow_LeavesPrayerUntouched()
    {
        var (service, settings, reminder) = Create(follow: false);
        var before = settings.CurrentSettings.PrayerCity;

        service.ApplyCity(Bandung(), LocationOrigin.Bmkg);

        Assert.Equal(before, settings.CurrentSettings.PrayerCity);
        Assert.Equal(Bandung().BmkgAdm4Code, settings.CurrentSettings.BmkgCustomAdm4Code);
        Assert.Equal(0, reminder.RefreshCount);
    }

    [Fact]
    public void ApplyPrecise_WithFollow_SetsAdm4AndPrayerCoordinates()
    {
        var (service, settings, reminder) = Create(follow: true);

        service.ApplyPrecise("Dago, Kota Bandung", "32.73.07.1002", -6.88, 107.61, 7.0);

        var s = settings.CurrentSettings;
        Assert.Equal("32.73.07.1002", s.BmkgCustomAdm4Code);
        Assert.Equal("Dago, Kota Bandung", s.BmkgSelectedCity);
        Assert.Equal("Dago, Kota Bandung", s.PrayerCity);
        Assert.Equal(-6.88, s.PrayerLatitude);
        Assert.Equal(107.61, s.PrayerLongitude);
        Assert.Equal(1, reminder.RefreshCount);
    }

    [Fact]
    public void ApplyPrecise_WithoutCoordinates_DoesNotTouchPrayer()
    {
        var (service, settings, reminder) = Create(follow: true);
        var before = settings.CurrentSettings.PrayerCity;

        service.ApplyPrecise("Dago", "32.73.07.1002", 0, 0, 7.0);

        Assert.Equal(before, settings.CurrentSettings.PrayerCity);
        Assert.Equal("32.73.07.1002", settings.CurrentSettings.BmkgCustomAdm4Code);
        Assert.Equal(0, reminder.RefreshCount);
    }

    [Fact]
    public void SettingsViewModel_SelectingCity_AlsoMovesWeatherLocation()
    {
        // Regresi: sebelumnya kode ADM4 lama tetap dipakai sehingga cuaca tidak pindah.
        var settings = new FakeSettingsService();
        settings.CurrentSettings.BmkgFollowPrayerCity = true;
        settings.CurrentSettings.BmkgCustomAdm4Code = "31.71.01.1001";
        var vm = new SettingsViewModel(settings, _ => { });
        var bandung = vm.IndonesianCities.First(c => c.Name == "Bandung");

        vm.SelectedCity = bandung;

        Assert.Equal("Bandung", settings.CurrentSettings.PrayerCity);
        Assert.Equal(bandung.BmkgAdm4Code, settings.CurrentSettings.BmkgCustomAdm4Code);
        Assert.Equal("Bandung", vm.SelectedCity?.Name);
    }
}
