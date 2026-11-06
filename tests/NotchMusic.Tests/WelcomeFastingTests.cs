using NotchMusic.Models;
using NotchMusic.Services.Prayer;
using NotchMusic.Services.Settings;
using NotchMusic.Services.Wellness;
using NotchMusic.ViewModels;
using Xunit;

#pragma warning disable CS0067

namespace NotchMusic.Tests;

public class WelcomeFastingTests
{
    private sealed class FakeSettings : ISettingsService
    {
        public AppSettings CurrentSettings { get; } = new();
        public void Save() { }
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => Path.GetTempPath();
        public string GetLyricsCachePath() => Path.GetTempPath();
        public void ClearLyricsCache() { }
    }

    private sealed class FixedPrayerService : IPrayerService
    {
        private readonly PrayerSchedule _schedule;
        public FixedPrayerService(PrayerSchedule schedule) => _schedule = schedule;
        public PrayerSchedule GetTodaySchedule() => _schedule;
        public PrayerSchedule GetScheduleForDate(DateTime date) => _schedule;
        public Task<CityLocation?> DetectLocationAsync(CancellationToken cancellationToken = default) => Task.FromResult<CityLocation?>(null);
        public void UpdateLocation(string cityName, double latitude, double longitude, double timezone) { }
    }

    private static PrayerTimeItem Item(string name, int h, int m, bool mandatory = true) => new()
    {
        Name = name,
        Time = new TimeSpan(h, m, 0),
        FormattedTime = $"{h:00}:{m:00}",
        IsMandatoryPrayer = mandatory
    };

    private static PrayerSchedule Schedule() => new()
    {
        Items = new List<PrayerTimeItem>
        {
            Item("Imsak", 4, 30, mandatory: false),
            Item("Subuh", 4, 40),
            Item("Dzuhur", 11, 58),
            Item("Ashar", 15, 10),
            Item("Maghrib", 17, 52),
            Item("Isya", 19, 2)
        }
    };

    // 3 Nov 2026 = Selasa; 2 Nov = Senin; 5 Nov = Kamis.
    private static readonly DateTime Tuesday = new(2026, 11, 3);
    private static readonly DateTime Monday = new(2026, 11, 2);
    private static readonly DateTime Thursday = new(2026, 11, 5);

    private sealed class ReminderHarness
    {
        public FakeSettings Settings { get; } = new();
        public DateTime Now { get; set; }
        public List<PrayerNotificationEvent> Events { get; } = new();
        public PrayerReminderService Service { get; }

        public ReminderHarness(DateTime day)
        {
            Now = day;
            Settings.CurrentSettings.EnablePrayerTimes = true;
            Settings.CurrentSettings.PrayerSoundAlert = false;
            Service = new PrayerReminderService(new FixedPrayerService(Schedule()), Settings, () => Now);
            Service.ReminderTriggered += Events.Add;
        }

        public void At(DateTime day, int h, int m, int s = 0)
        {
            Now = day.Date.AddHours(h).AddMinutes(m).AddSeconds(s);
            Service.Tick();
        }
    }

    // --- Pengingat sebelum / saat / sesudah adzan ---

    [Fact]
    public void PrayerReminders_FireBeforeAtAndAfter_EachOnce()
    {
        var h = new ReminderHarness(Tuesday);

        h.At(Tuesday, 11, 47);   // belum 10 menit sebelum (11:48)
        Assert.Empty(h.Events);

        h.At(Tuesday, 11, 48);
        h.At(Tuesday, 11, 48, 30); // tidak berulang
        h.At(Tuesday, 11, 58);
        h.At(Tuesday, 12, 13);

        Assert.Equal(new[] { PrayerReminderType.Before, PrayerReminderType.AtPrayerTime, PrayerReminderType.After },
            h.Events.Select(e => e.Type));
        Assert.Contains("10 Menit Menuju Waktu Dzuhur", h.Events[0].Title);
        Assert.Contains("berjamaah", h.Events[0].Message);
        Assert.Contains("Telah Tiba", h.Events[1].Title);
        Assert.True(h.Events[1].AutoPauseRequested);
        Assert.Contains("15 menit", h.Events[2].Message);
    }

    [Fact]
    public void PrayerReminder_MissedByMoreThanTwoMinutes_DoesNotFire()
    {
        var h = new ReminderHarness(Tuesday);

        h.At(Tuesday, 11, 51); // 3 menit setelah titik "sebelum" (11:48) - jendela 2 menit sudah lewat

        Assert.Empty(h.Events);
    }

    // --- Puasa ---

    [Fact]
    public void FastingDay_RespectsMondayThursdayOption()
    {
        var s = new AppSettings { EnableFasting = true };
        Assert.True(FastingHelper.IsFastingDay(s, Tuesday));

        s.FastingMondayThursdayOnly = true;
        Assert.False(FastingHelper.IsFastingDay(s, Tuesday));
        Assert.True(FastingHelper.IsFastingDay(s, Monday));
        Assert.True(FastingHelper.IsFastingDay(s, Thursday));

        s.EnableFasting = false;
        Assert.False(FastingHelper.IsFastingDay(s, Monday));
    }

    [Fact]
    public void FastingNow_IsBetweenImsakAndMaghrib()
    {
        var s = new AppSettings { EnableFasting = true };
        var schedule = Schedule();

        Assert.False(FastingHelper.IsFastingNow(s, schedule, Tuesday.AddHours(4).AddMinutes(29)));
        Assert.True(FastingHelper.IsFastingNow(s, schedule, Tuesday.AddHours(4).AddMinutes(30)));
        Assert.True(FastingHelper.IsFastingNow(s, schedule, Tuesday.AddHours(12)));
        Assert.False(FastingHelper.IsFastingNow(s, schedule, Tuesday.AddHours(17).AddMinutes(52)));
    }

    [Fact]
    public void Sahur_FiresBeforeImsak_OnlyOnFastingDays()
    {
        var h = new ReminderHarness(Tuesday);
        h.Settings.CurrentSettings.EnableFasting = true;
        h.Settings.CurrentSettings.FastingSahurMinutes = 30;

        h.At(Tuesday, 3, 59);
        Assert.DoesNotContain(h.Events, e => e.Type == PrayerReminderType.Sahur);

        h.At(Tuesday, 4, 0);
        h.At(Tuesday, 4, 0, 40);

        var sahur = Assert.Single(h.Events, e => e.Type == PrayerReminderType.Sahur);
        Assert.Contains("Sahur", sahur.Title);
        Assert.Contains("04:30", sahur.Message);

        var off = new ReminderHarness(Tuesday);
        off.At(Tuesday, 4, 0);
        Assert.DoesNotContain(off.Events, e => e.Type == PrayerReminderType.Sahur);
    }

    [Fact]
    public void Maghrib_OnFastingDay_AnnouncesBreakingTheFast()
    {
        var h = new ReminderHarness(Tuesday);
        h.Settings.CurrentSettings.EnableFasting = true;

        h.At(Tuesday, 17, 52);

        var evt = Assert.Single(h.Events, e => e.Type == PrayerReminderType.AtPrayerTime);
        Assert.Contains("Berbuka", evt.Title);
    }

    [Fact]
    public void Maghrib_WithoutFasting_KeepsNormalAdhanTitle()
    {
        var h = new ReminderHarness(Tuesday);

        h.At(Tuesday, 17, 52);

        var evt = Assert.Single(h.Events, e => e.Type == PrayerReminderType.AtPrayerTime);
        Assert.Contains("Waktu Sholat Maghrib", evt.Title);
    }

    [Fact]
    public void DrinkReminder_IsSilentWhileFasting_AndResumesAfterwards()
    {
        var settings = new FakeSettings();
        settings.CurrentSettings.DrinkReminderEnabled = true;
        settings.CurrentSettings.DrinkReminderMinutes = 30;
        settings.CurrentSettings.ReminderWorkStart = "00:00";
        settings.CurrentSettings.ReminderWorkEnd = "23:59";
        var now = Tuesday.AddHours(10);
        bool fasting = true;
        var service = new WellnessReminderService(settings, () => now, () => TimeSpan.Zero, () => fasting);
        var events = new List<WellnessNotificationEvent>();
        service.ReminderTriggered += events.Add;

        for (int i = 0; i < 6; i++) { service.Tick(); now = now.AddMinutes(30); }
        Assert.Empty(events);

        fasting = false;
        service.Tick();                 // jadwalkan ulang
        now = now.AddMinutes(31);
        service.Tick();

        Assert.Single(events);
    }

    // --- Selamat datang kembali ---

    private sealed class WelcomeHarness
    {
        public FakeSettings Settings { get; } = new();
        public TimeSpan Idle { get; set; }
        public DateTime Now { get; set; } = new DateTime(2026, 11, 3, 9, 0, 0);
        public List<TimeSpan> Returns { get; } = new();
        public WelcomeBackService Service { get; }

        public WelcomeHarness()
        {
            Settings.CurrentSettings.WelcomeBackIdleMinutes = 60;
            Service = new WelcomeBackService(Settings, () => Idle, () => Now);
            Service.Returned += Returns.Add;
        }

        public void Tick(TimeSpan idle) { Idle = idle; Service.Tick(); }

        public void Advance(TimeSpan by, TimeSpan idle) { Now += by; Tick(idle); }
    }

    [Fact]
    public void Welcome_FiresOnceWhenInputReturnsAfterLongIdle()
    {
        var h = new WelcomeHarness();

        h.Tick(TimeSpan.FromMinutes(10));
        h.Tick(TimeSpan.FromMinutes(61));
        h.Tick(TimeSpan.FromMinutes(95));
        Assert.Empty(h.Returns);

        h.Tick(TimeSpan.FromSeconds(1));   // gerak mouse / ketik lagi
        h.Tick(TimeSpan.FromSeconds(4));

        Assert.Equal(TimeSpan.FromMinutes(95), Assert.Single(h.Returns));
    }

    [Fact]
    public void Welcome_ShortIdle_DoesNotFire()
    {
        var h = new WelcomeHarness();

        h.Tick(TimeSpan.FromMinutes(30));
        h.Tick(TimeSpan.FromSeconds(2));

        Assert.Empty(h.Returns);
    }

    [Fact]
    public void Welcome_Disabled_DoesNotFire()
    {
        var h = new WelcomeHarness();
        h.Settings.CurrentSettings.EnableWelcomeBack = false;

        h.Tick(TimeSpan.FromMinutes(120));
        h.Tick(TimeSpan.FromSeconds(1));

        Assert.Empty(h.Returns);
    }

    [Fact]
    public void Welcome_ThresholdFollowsSetting()
    {
        var h = new WelcomeHarness();
        h.Settings.CurrentSettings.WelcomeBackIdleMinutes = 15;

        h.Tick(TimeSpan.FromMinutes(20));
        h.Tick(TimeSpan.FromSeconds(1));

        Assert.Single(h.Returns);
    }

    [Fact]
    public void Welcome_Locked_StaysSilentUntilUnlock_EvenIfLockScreenInputResetsIdle()
    {
        var h = new WelcomeHarness();
        h.Service.OnLocked();

        h.Advance(TimeSpan.FromMinutes(90), TimeSpan.FromMinutes(90));
        h.Advance(TimeSpan.FromSeconds(3), TimeSpan.Zero);   // mengetik password: idle reset
        h.Advance(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(2));
        Assert.Empty(h.Returns);

        h.Now += TimeSpan.FromSeconds(3);
        h.Service.OnUnlocked();

        Assert.Equal(TimeSpan.FromMinutes(90) + TimeSpan.FromSeconds(9), Assert.Single(h.Returns));
    }

    [Fact]
    public void Welcome_ShortLock_DoesNotGreet()
    {
        var h = new WelcomeHarness();
        h.Service.OnLocked();
        h.Now += TimeSpan.FromMinutes(10);

        h.Service.OnUnlocked();

        Assert.Empty(h.Returns);
    }

    [Fact]
    public void Welcome_SleepWithoutLock_GreetsOnFirstInputAfterWakeSettles()
    {
        var h = new WelcomeHarness();
        h.Service.OnSuspended();
        h.Now += TimeSpan.FromHours(2);   // timer berhenti selama sleep: tak ada Tick
        h.Service.OnResumed();

        h.Advance(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));   // belum lewat jeda bangun
        Assert.Empty(h.Returns);

        h.Advance(TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));
        Assert.Equal(TimeSpan.FromHours(2) + TimeSpan.FromSeconds(7), Assert.Single(h.Returns));

        h.Advance(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));   // tidak berulang
        Assert.Single(h.Returns);
    }

    [Fact]
    public void Welcome_SleepThenLockScreen_WaitsForUnlock()
    {
        var h = new WelcomeHarness();
        h.Service.OnSuspended();
        h.Now += TimeSpan.FromHours(3);
        h.Service.OnResumed();
        h.Service.OnLocked();   // layar kunci muncul saat bangun

        h.Advance(TimeSpan.FromSeconds(10), TimeSpan.Zero);
        h.Advance(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        Assert.Empty(h.Returns);

        h.Now += TimeSpan.FromSeconds(5);
        h.Service.OnUnlocked();

        Assert.Equal(TimeSpan.FromHours(3) + TimeSpan.FromSeconds(25), Assert.Single(h.Returns));
    }

    [Fact]
    public void Welcome_SleepWithUnattendedWake_WaitsForRealInput()
    {
        var h = new WelcomeHarness();
        h.Service.OnSuspended();
        h.Now += TimeSpan.FromHours(2);
        h.Service.OnResumed();

        h.Advance(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5));   // bangun otomatis, belum ada orang
        Assert.Empty(h.Returns);

        h.Advance(TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));    // pengguna datang
        Assert.Single(h.Returns);
    }

    [Theory]
    [InlineData(8, "Selamat pagi")]
    [InlineData(12, "Selamat siang")]
    [InlineData(16, "Selamat sore")]
    [InlineData(21, "Selamat malam")]
    public void BuildWelcome_GreetsByTimeOfDay(int hour, string salam)
    {
        var evt = MainViewModel.BuildWelcome("Fajar", TimeSpan.FromMinutes(90), Tuesday.AddHours(hour));

        Assert.Equal(WellnessReminderKind.Welcome, evt.Kind);
        Assert.Equal("Selamat datang kembali, Fajar", evt.Title);
        Assert.StartsWith(salam, evt.Message);
        Assert.Contains("1j 30m", evt.Message);
    }

    [Fact]
    public void BuildWelcome_WithoutName_OmitsIt()
    {
        var evt = MainViewModel.BuildWelcome("  ", TimeSpan.FromHours(2), Tuesday.AddHours(9));

        Assert.Equal("Selamat datang kembali", evt.Title);
    }

    [Fact]
    public void WelcomeBack_ShowsAsVioletNotchOnly_AndClickDismisses()
    {
        var stub = new ViewModelTestFactory.StubWelcomeBack();
        var settings = new ViewModelTestFactory.MemorySettings();
        settings.CurrentSettings.UserDisplayName = "Fajar";
        var vm = ViewModelTestFactory.CreateMain(settings, welcomeBack: stub);

        stub.Raise(TimeSpan.FromHours(2));

        Assert.True(vm.ShowWellnessNotch);
        Assert.False(vm.ShowWellnessBanner);
        Assert.Equal("#B388FF", vm.WellnessAccentColor);
        Assert.StartsWith("Selamat datang kembali, Fajar", vm.WellnessNotchText);
        Assert.DoesNotContain("Klik:", vm.WellnessNotchText);

        vm.WellnessPrimaryActionCommand.Execute(null);

        Assert.False(vm.HasActiveWellnessNotification);
    }

    // --- Pengingat sholat hanya di notch ---

    private static PrayerNotificationEvent Reminder(PrayerReminderType type) => new()
    {
        PrayerName = "Dzuhur",
        PrayerTimeFormatted = "11:58",
        Type = type,
        Title = "t",
        Message = "m"
    };

    [Theory]
    [InlineData(PrayerReminderType.Before)]
    [InlineData(PrayerReminderType.AtPrayerTime)]
    [InlineData(PrayerReminderType.After)]
    [InlineData(PrayerReminderType.Sahur)]
    public void AllPrayerReminders_ShowInTheNotch(PrayerReminderType type)
    {
        var vm = ViewModelTestFactory.CreateMain();

        vm.ShowPrayerNotification(Reminder(type));

        Assert.True(vm.ShowPrayerNotch);
        Assert.False(vm.HasVisibleIslandBanner);
    }

    [Fact]
    public void PrayerReminder_WhileIslandIsOpen_StaysPendingInsteadOfBecomingABanner()
    {
        var vm = ViewModelTestFactory.CreateMain();
        vm.OnMouseEnter();

        vm.ShowPrayerNotification(Reminder(PrayerReminderType.Before));

        Assert.False(vm.ShowPrayerNotch);
        Assert.False(vm.HasVisibleIslandBanner);
        Assert.True(vm.HasActivePrayerNotification);
    }
}
