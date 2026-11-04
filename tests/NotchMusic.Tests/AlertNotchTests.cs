using NotchMusic.Models;
using NotchMusic.ViewModels;
using Xunit;

namespace NotchMusic.Tests;

/// <summary>Peringatan berat BMKG (gempa M >= 5, cuaca ekstrem) tampil sebagai notch notifikasi saat idle.</summary>
public class AlertNotchTests
{
    private static BmkgNotificationEvent Quake(bool high, string magnitude = "5.3") => new()
    {
        Id = "eq1",
        Title = $"⚠️ Info Gempa BMKG • M {magnitude}",
        Message = "1 km BaratDaya TOMOHON-SULUT • Kedalaman 148 km (08:15:02 WIB)",
        MagnitudeBadge = $"M {magnitude}",
        IsEarthquake = true,
        IsHighSeverity = high
    };

    private static BmkgNotificationEvent Weather() => new()
    {
        Id = "w1",
        Title = "⛈ Peringatan Cuaca BMKG • Jakarta",
        Message = "Hujan Petir (28°C)",
        IsEarthquake = false,
        IsHighSeverity = true
    };

    [Fact]
    public void HighSeverityQuake_WhenIdle_ShowsAlertNotch_AndHidesEyesAndBanner()
    {
        var vm = ViewModelTestFactory.CreateMain();

        vm.ShowBmkgNotification(Quake(high: true));

        Assert.True(vm.ShowAlertNotch);
        Assert.False(vm.ShowEyesInIdle);
        Assert.False(vm.ShowBmkgBanner);
        Assert.Equal("⚠ M 5.3 • 1 km BaratDaya TOMOHON-SULUT • Kedalaman 148 km (08:15:02 WIB)", vm.AlertNotchText);
        Assert.True(double.IsNaN(vm.AlertNotchWidth));
    }

    [Fact]
    public void ExtremeWeather_AlsoUsesAlertNotch()
    {
        var vm = ViewModelTestFactory.CreateMain();

        vm.ShowBmkgNotification(Weather());

        Assert.True(vm.ShowAlertNotch);
        Assert.StartsWith("⛈ Peringatan Cuaca BMKG", vm.AlertNotchText);
    }

    [Fact]
    public void SmallQuake_KeepsTheOldBanner_AndEyes()
    {
        var vm = ViewModelTestFactory.CreateMain();

        vm.ShowBmkgNotification(Quake(high: false, magnitude: "4.1"));

        Assert.False(vm.ShowAlertNotch);
        Assert.True(vm.ShowEyesInIdle);
        Assert.True(vm.ShowBmkgBanner);
    }

    [Fact]
    public void Hovering_SwitchesFromAlertNotchToFullBanner_ThenIdleAfterDismissAndLeave()
    {
        var vm = ViewModelTestFactory.CreateMain();
        vm.ShowBmkgNotification(Quake(high: true));

        vm.OnMouseEnter();
        Assert.False(vm.ShowAlertNotch);
        Assert.True(vm.ShowBmkgBanner);

        // Selama peringatan aktif island tetap terbuka; setelah ditutup dan kursor pergi, kembali ke idle.
        vm.OnMouseLeave();
        vm.CollapseHoverIfIdle();
        Assert.True(vm.ShowBmkgBanner);

        vm.DismissBmkgNotificationCommand.Execute(null);
        vm.CollapseHoverIfIdle();
        Assert.True(vm.ShowEyesInIdle);
        Assert.False(vm.ShowBmkgBanner);
    }

    [Fact]
    public void Dismissing_RestoresIdleEyes()
    {
        var vm = ViewModelTestFactory.CreateMain();
        vm.ShowBmkgNotification(Quake(high: true));

        vm.DismissBmkgNotificationCommand.Execute(null);

        Assert.False(vm.ShowAlertNotch);
        Assert.True(vm.ShowEyesInIdle);
        Assert.False(vm.ShowBmkgBanner);
        Assert.Equal(string.Empty, vm.AlertNotchText);
    }

    [Fact]
    public void NoAlert_MeansNothingChangesInIdle()
    {
        var vm = ViewModelTestFactory.CreateMain();

        Assert.False(vm.ShowAlertNotch);
        Assert.True(vm.ShowEyesInIdle);
        Assert.False(vm.ShowBmkgBanner);
    }

    private static PrayerNotificationEvent Adhan() => new()
    {
        PrayerName = "Dzuhur",
        PrayerTimeFormatted = "11:58",
        Type = PrayerReminderType.AtPrayerTime,
        Title = "🕌 Waktu Sholat Dzuhur Telah Tiba!",
        Message = "Pukul 11:58."
    };

    [Fact]
    public void PrayerNotification_WhenIdle_ShowsGreenNotch_AndHidesEyesAndBanner()
    {
        var vm = ViewModelTestFactory.CreateMain();

        vm.ShowPrayerNotification(Adhan());

        Assert.True(vm.ShowPrayerNotch);
        Assert.False(vm.ShowAlertNotch);
        Assert.False(vm.ShowEyesInIdle);
        Assert.False(vm.ShowPrayerBanner);
        Assert.Equal("🕌 Waktu Sholat Dzuhur Telah Tiba! • Pukul 11:58.", vm.PrayerNotchText);
    }

    [Fact]
    public void PrayerNotification_ClickOrDismiss_RestoresEyes()
    {
        var vm = ViewModelTestFactory.CreateMain();
        vm.ShowPrayerNotification(Adhan());

        vm.DismissPrayerNotificationCommand.Execute(null);

        Assert.False(vm.ShowPrayerNotch);
        Assert.True(vm.ShowEyesInIdle);
        Assert.Equal(string.Empty, vm.PrayerNotchText);
    }

    [Fact]
    public void Hovering_SwitchesFromPrayerNotchToFullBanner()
    {
        var vm = ViewModelTestFactory.CreateMain();
        vm.ShowPrayerNotification(Adhan());

        vm.OnMouseEnter();

        Assert.False(vm.ShowPrayerNotch);
        Assert.True(vm.ShowPrayerBanner);
    }

    [Fact]
    public void RedAlert_TakesPriorityOverPrayerNotch()
    {
        var vm = ViewModelTestFactory.CreateMain();
        vm.ShowPrayerNotification(Adhan());
        vm.ShowBmkgNotification(Quake(high: true));

        Assert.True(vm.ShowAlertNotch);
        Assert.False(vm.ShowPrayerNotch);
        Assert.False(vm.ShowEyesInIdle);
    }

    [Fact]
    public void ExpandedMode_LeavesIdleCapsule_SoTheExpandedIslandIsNotClipped()
    {
        var vm = ViewModelTestFactory.CreateMain();
        Assert.True(vm.ShowIdleEyes);

        vm.CurrentMode = PanelMode.Expanded;
        vm.IsPrayerPanelOpen = true;

        Assert.True(vm.IsExpandedMode);
        // Kapsul idle (ukuran tetap) hanya untuk mode compact; di mode expanded ia memotong seluruh isi island.
        Assert.False(vm.ShowIdleEyes);
        Assert.False(vm.ShowEyesInIdle);

        vm.SetModeCommand.Execute("Compact");
        Assert.True(vm.ShowIdleEyes);
    }

    [Fact]
    public void ClickingThePrayerPill_OpensItsOwnWindow_WithoutDetachingOrExpandingTheIsland()
    {
        var vm = ViewModelTestFactory.CreateMain();
        int requested = 0;
        vm.RequestOpenPrayerSchedule += () => requested++;

        vm.OpenPrayerScheduleCommand.Execute(null);

        Assert.Equal(1, requested);
        Assert.True(vm.IsCompactMode);
        Assert.False(vm.IsPrayerPanelOpen);
        Assert.False(vm.IsPrayerDetached);
    }

    [Fact]
    public void IslandEarthquakeBadge_HidesAfterThreeHours()
    {
        var now = new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);
        BmkgEarthquakeItem At(TimeSpan ago) => new() { EventDateTime = now - ago };

        Assert.True(MainViewModel.IsRecentEarthquake(At(TimeSpan.FromMinutes(5)), now));
        Assert.True(MainViewModel.IsRecentEarthquake(At(TimeSpan.FromHours(3)), now));
        Assert.False(MainViewModel.IsRecentEarthquake(At(TimeSpan.FromHours(3) + TimeSpan.FromSeconds(1)), now));
        Assert.False(MainViewModel.IsRecentEarthquake(null, now));
        // Waktu kejadian tidak diketahui: tidak ada dasar untuk menyembunyikan.
        Assert.True(MainViewModel.IsRecentEarthquake(new BmkgEarthquakeItem(), now));
    }

    [Fact]
    public void RaisesPropertyChanged_WhenAlertAppearsOrHoverChanges()
    {
        var vm = ViewModelTestFactory.CreateMain();
        var raised = new System.Collections.Generic.List<string?>();
        vm.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

        vm.ShowBmkgNotification(Quake(high: true));
        Assert.Contains(nameof(MainViewModel.ShowAlertNotch), raised);
        Assert.Contains(nameof(MainViewModel.ShowEyesInIdle), raised);

        raised.Clear();
        vm.OnMouseEnter();
        Assert.Contains(nameof(MainViewModel.ShowAlertNotch), raised);
        Assert.Contains(nameof(MainViewModel.ShowBmkgBanner), raised);
    }
}
