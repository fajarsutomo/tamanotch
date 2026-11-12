using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TamaNotch.Models;
using TamaNotch.Services.Lyrics;
using TamaNotch.Services.Media;
using TamaNotch.ViewModels;
using TamaNotch.Services.Settings;
using TamaNotch.Services.Wellness;
using Xunit;

namespace TamaNotch.Tests;

public class WellnessReminderTests
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

    private class Harness
    {
        public FakeSettingsService Settings { get; } = new();
        public DateTime Now { get; set; } = new DateTime(2026, 11, 3, 9, 0, 0);
        public TimeSpan Idle { get; set; } = TimeSpan.Zero;
        public List<WellnessNotificationEvent> Events { get; } = new();
        public WellnessReminderService Service { get; }

        public Harness()
        {
            Service = new WellnessReminderService(Settings, () => Now, () => Idle);
            Service.ReminderTriggered += Events.Add;
        }

        public void At(int hour, int minute)
        {
            Now = new DateTime(Now.Year, Now.Month, Now.Day, hour, minute, 0);
            Service.Tick();
        }
    }

    [Fact]
    public void Disabled_NeverRaises()
    {
        var h = new Harness();
        h.At(9, 0);
        h.At(12, 0);
        h.At(16, 0);

        Assert.Empty(h.Events);
    }

    [Fact]
    public void Move_FiresAfterInterval_ThenRepeats()
    {
        var h = new Harness();
        h.Settings.CurrentSettings.MoveReminderEnabled = true;
        h.Settings.CurrentSettings.MoveReminderMinutes = 45;

        h.At(9, 0);    // menjadwalkan, jatuh tempo 09:45
        h.At(9, 44);
        Assert.Empty(h.Events);

        h.At(9, 45);
        Assert.Single(h.Events);
        Assert.Equal(WellnessReminderKind.Move, h.Events[0].Kind);

        h.At(10, 29);
        Assert.Single(h.Events);
        h.At(10, 30);
        Assert.Equal(2, h.Events.Count);
    }

    [Fact]
    public void Move_AwayFromComputer_CountsAsBreakAndRestartsTimer()
    {
        var h = new Harness();
        h.Settings.CurrentSettings.MoveReminderEnabled = true;
        h.Settings.CurrentSettings.MoveReminderMinutes = 45;

        h.At(9, 0);
        h.Idle = TimeSpan.FromMinutes(5);   // pergi, mis. ke dapur
        h.At(9, 44);                          // jatuh tempo diundur ke 10:29
        h.Idle = TimeSpan.Zero;

        h.At(9, 45);
        h.At(10, 28);
        Assert.Empty(h.Events);

        h.At(10, 29);
        Assert.Single(h.Events);
    }

    [Fact]
    public void Drink_FiresWithCountAndStopsAfterGoal()
    {
        var h = new Harness();
        var s = h.Settings.CurrentSettings;
        s.DrinkReminderEnabled = true;
        s.DrinkReminderMinutes = 60;
        s.DrinkDailyGoalGlasses = 2;
        s.DrinkGlassMl = 250;

        h.At(9, 0);
        h.At(10, 0);

        Assert.Single(h.Events);
        Assert.Equal(WellnessReminderKind.Drink, h.Events[0].Kind);
        Assert.Contains("250 ml", h.Events[0].Message);
        Assert.Contains("0/2", h.Events[0].Message);

        h.Service.LogDrink();
        h.Service.LogDrink();
        Assert.Equal(2, h.Service.DrinkCountToday);

        h.At(11, 0);
        h.At(12, 0);
        Assert.Single(h.Events);   // target tercapai: tidak lagi mengingatkan
    }

    [Fact]
    public void Drink_DueWhileAway_FiresWhenUserReturns()
    {
        var h = new Harness();
        h.Settings.CurrentSettings.DrinkReminderEnabled = true;
        h.Settings.CurrentSettings.DrinkReminderMinutes = 60;

        h.At(9, 0);
        h.Idle = TimeSpan.FromMinutes(10);
        h.At(10, 5);
        Assert.Empty(h.Events);

        h.Idle = TimeSpan.Zero;
        h.At(10, 6);
        Assert.Single(h.Events);
    }

    [Fact]
    public void OutsideWorkHours_StaysSilentAndSchedulingStartsAtWorkStart()
    {
        var h = new Harness();
        h.Settings.CurrentSettings.DrinkReminderEnabled = true;
        h.Settings.CurrentSettings.DrinkReminderMinutes = 60;

        h.At(7, 59);
        h.At(8, 0);     // jam kerja mulai: jatuh tempo 09:00
        h.At(8, 59);
        Assert.Empty(h.Events);

        h.At(9, 0);
        Assert.Single(h.Events);

        h.At(17, 30);   // di luar jam kerja
        h.At(18, 30);
        Assert.Single(h.Events);
    }

    [Fact]
    public void Snooze_DelaysNextReminder()
    {
        var h = new Harness();
        h.Settings.CurrentSettings.MoveReminderEnabled = true;
        h.Settings.CurrentSettings.MoveReminderMinutes = 45;

        h.At(9, 0);
        h.At(9, 45);
        Assert.Single(h.Events);

        h.Service.Snooze(WellnessReminderKind.Move, 10);
        h.At(9, 54);
        Assert.Single(h.Events);
        h.At(9, 55);
        Assert.Equal(2, h.Events.Count);
    }

    [Fact]
    public void LogDrink_SavesAndNewDayResetsCounter()
    {
        var h = new Harness();
        int stateChanged = 0;
        h.Service.StateChanged += () => stateChanged++;

        h.At(9, 0);
        int savesBefore = h.Settings.SaveCount;
        h.Service.LogDrink();
        h.Service.LogDrink();

        Assert.Equal(2, h.Service.DrinkCountToday);
        Assert.True(h.Settings.SaveCount > savesBefore);

        h.Now = h.Now.AddDays(1);
        h.Service.Tick();
        Assert.Equal(0, h.Service.DrinkCountToday);
        Assert.True(stateChanged >= 3);
    }

    [Fact]
    public void WorkHours_OvernightRangeIsSupported()
    {
        var h = new Harness();
        h.Settings.CurrentSettings.ReminderWorkStart = "22:00";
        h.Settings.CurrentSettings.ReminderWorkEnd = "06:00";

        Assert.True(h.Service.IsWithinWorkHours(new DateTime(2026, 11, 3, 23, 0, 0)));
        Assert.True(h.Service.IsWithinWorkHours(new DateTime(2026, 11, 3, 5, 0, 0)));
        Assert.False(h.Service.IsWithinWorkHours(new DateTime(2026, 11, 3, 12, 0, 0)));
    }

    private class SpyWellnessService : IWellnessReminderService
    {
        public event Action<WellnessNotificationEvent>? ReminderTriggered;
        public event Action? StateChanged { add { } remove { } }
        public int DrinkCountToday => 0;
        public int DrinkGoalGlasses => 8;
        public int DrinkGlassMl => 230;
        public int LogDrinkCalls { get; private set; }
        public int MovedCalls { get; private set; }
        public (WellnessReminderKind Kind, int Minutes)? LastSnooze { get; private set; }
        public void Start() { }
        public void Stop() { }
        public void Tick() { }
        public void LogDrink() => LogDrinkCalls++;
        public void ResetDrinkCount() { }
        public void MarkMoved() => MovedCalls++;
        public void Snooze(WellnessReminderKind kind, int minutes) => LastSnooze = (kind, minutes);
        public void Dispose() { }
        public void Raise(WellnessNotificationEvent evt) => ReminderTriggered?.Invoke(evt);
    }

    private class StubMedia : IMediaSessionService
    {
        public event EventHandler<TrackInfo?>? TrackChanged { add { } remove { } }
        public event EventHandler<TimeSpan>? TimelineUpdated { add { } remove { } }
        public event EventHandler<IReadOnlyList<MediaSessionInfo>>? SessionsChanged { add { } remove { } }
        public event EventHandler? PlaybackStartedOnSpotify { add { } remove { } }
        public event EventHandler? PlaybackStartedOnYouTubeMusic { add { } remove { } }
        public TrackInfo? CurrentTrack => null;
        public IReadOnlyList<MediaSessionInfo> AvailableSessions => new List<MediaSessionInfo>();
        public string? SelectedSessionId => null;
        public Task InitializeAsync() => Task.CompletedTask;
        public void SelectSession(string sessionId) { }
        public Task TogglePlayPauseAsync() => Task.CompletedTask;
        public Task PauseAsync() => Task.CompletedTask;
        public Task PreviousAsync() => Task.CompletedTask;
        public Task NextAsync() => Task.CompletedTask;
        public Task SeekAsync(TimeSpan position) => Task.CompletedTask;
        public Task PauseSessionByKeywordAsync(string keyword) => Task.CompletedTask;
        public void Dispose() { }
    }

    private class StubLyrics : ILyricsService
    {
        public Task<SongLyrics> GetLyricsAsync(string title, string artist, string album, TimeSpan duration, CancellationToken cancellationToken = default)
            => Task.FromResult(new SongLyrics());
        public Task<List<LrcLibSearchResult>> SearchCandidatesAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<LrcLibSearchResult>());
        public SongLyrics LoadFromLocalFile(string filePath) => new SongLyrics();
    }

    private static (MainViewModel Vm, SpyWellnessService Spy) CreateViewModel()
    {
        var spy = new SpyWellnessService();
        var vm = new MainViewModel(new StubMedia(), new StubLyrics(), new FakeSettingsService(), wellnessService: spy);
        vm.Settings.ReminderSoundEnabled = false;
        return (vm, spy);
    }

    [Fact]
    public void Banner_Drink_PrimaryActionLogsDrinkAndClears()
    {
        var (vm, spy) = CreateViewModel();
        spy.Raise(new WellnessNotificationEvent { Kind = WellnessReminderKind.Drink, Title = "Waktunya minum", PrimaryActionText = "Sudah minum" });

        Assert.True(vm.HasActiveWellnessNotification);
        Assert.Equal("#4FC3F7", vm.WellnessAccentColor);

        vm.WellnessPrimaryActionCommand.Execute(null);

        Assert.Equal(1, spy.LogDrinkCalls);
        Assert.False(vm.HasActiveWellnessNotification);
    }

    [Fact]
    public void Banner_Move_PrimaryActionMarksMoved_SnoozeDelaysTenMinutes()
    {
        var (vm, spy) = CreateViewModel();
        spy.Raise(new WellnessNotificationEvent { Kind = WellnessReminderKind.Move, Title = "Waktunya bergerak", PrimaryActionText = "Sudah bergerak" });
        vm.WellnessPrimaryActionCommand.Execute(null);
        Assert.Equal(1, spy.MovedCalls);

        spy.Raise(new WellnessNotificationEvent { Kind = WellnessReminderKind.Move, Title = "Waktunya bergerak" });
        vm.WellnessSnoozeCommand.Execute(null);

        Assert.Equal((WellnessReminderKind.Move, 10), spy.LastSnooze);
        Assert.False(vm.HasActiveWellnessNotification);
    }

    [Fact]
    public void Banner_Dismiss_ClearsWithoutAction()
    {
        var (vm, spy) = CreateViewModel();
        spy.Raise(new WellnessNotificationEvent { Kind = WellnessReminderKind.Drink, Title = "Waktunya minum" });

        vm.DismissWellnessNotificationCommand.Execute(null);

        Assert.False(vm.HasActiveWellnessNotification);
        Assert.Equal(0, spy.LogDrinkCalls);
        Assert.Null(spy.LastSnooze);
    }
}
