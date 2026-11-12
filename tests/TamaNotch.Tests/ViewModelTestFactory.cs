using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TamaNotch.Models;
using TamaNotch.Services.Lyrics;
using TamaNotch.Services.Media;
using TamaNotch.Services.Settings;
using TamaNotch.ViewModels;

namespace TamaNotch.Tests;

/// <summary>Pembuat MainViewModel/SettingsViewModel dengan fake ringan untuk tes baru.</summary>
internal static class ViewModelTestFactory
{
    public sealed class MemorySettings : ISettingsService
    {
        public AppSettings CurrentSettings { get; } = new();
        public int SaveCount { get; private set; }
        public void Save() => SaveCount++;
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tama_vm_" + Guid.NewGuid().ToString("N"));
        public string GetLyricsCachePath() => System.IO.Path.GetTempPath();
        public void ClearLyricsCache() { }
    }

    private sealed class StubMedia : IMediaSessionService
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

    private sealed class StubLyrics : ILyricsService
    {
        public Task<SongLyrics> GetLyricsAsync(string title, string artist, string album, TimeSpan duration, CancellationToken cancellationToken = default)
            => Task.FromResult(new SongLyrics());
        public Task<List<LrcLibSearchResult>> SearchCandidatesAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<LrcLibSearchResult>());
        public SongLyrics LoadFromLocalFile(string filePath) => new SongLyrics();
    }

    public sealed class StubAirQuality : TamaNotch.Services.AirQuality.IAirQualityService
    {
        public TamaNotch.Services.AirQuality.AirQualityReading? Current { get; set; }
        public int Refreshes { get; private set; }
        public event EventHandler? Updated;
        public void Start() { }
        public void Stop() { Current = null; Updated?.Invoke(this, EventArgs.Empty); }
        public Task RefreshAsync(CancellationToken cancellationToken = default) { Refreshes++; return Task.CompletedTask; }
        public void Raise() => Updated?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    public sealed class StubClaudeUsage : TamaNotch.Services.ClaudeUsage.IClaudeUsageService
    {
        public TamaNotch.Services.ClaudeUsage.ClaudeUsageSnapshot? Current { get; set; }
        public TamaNotch.Services.ClaudeUsage.ClaudeUsageStatus Status { get; set; }
        public DateTimeOffset? NextAllowedAt { get; set; }
        public int Starts { get; private set; }
        public int Stops { get; private set; }
        public int Refreshes { get; private set; }
        public event EventHandler? Updated;
        public void Start() { Starts++; Status = TamaNotch.Services.ClaudeUsage.ClaudeUsageStatus.Ok; }
        public void Stop() { Stops++; Current = null; Status = TamaNotch.Services.ClaudeUsage.ClaudeUsageStatus.Idle; }
        public Task RefreshAsync(CancellationToken cancellationToken = default) { Refreshes++; return Task.CompletedTask; }
        public void Raise() => Updated?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    public sealed class StubWelcomeBack : TamaNotch.Services.Wellness.IWelcomeBackService
    {
        public event Action<TimeSpan>? Returned;
        public void Start() { }
        public void Stop() { }
        public void Tick() { }
        public void Raise(TimeSpan away) => Returned?.Invoke(away);
        public void Dispose() { }
    }

    public static MainViewModel CreateMain(
        MemorySettings? settings = null,
        TamaNotch.Services.AirQuality.IAirQualityService? airQuality = null,
        TamaNotch.Services.Quran.IQuranService? quran = null,
        TamaNotch.Services.ClaudeUsage.IClaudeUsageService? claudeUsage = null,
        TamaNotch.Services.Wellness.IWelcomeBackService? welcomeBack = null,
        TamaNotch.Services.Notes.INotesService? notes = null,
        TamaNotch.Services.SystemMonitor.ISystemMonitorService? systemMonitor = null)
    {
        var vm = new MainViewModel(new StubMedia(), new StubLyrics(), settings ?? new MemorySettings(),
            airQualityService: airQuality ?? new StubAirQuality(),
            quranService: quran,
            claudeUsageService: claudeUsage ?? new StubClaudeUsage(),
            welcomeBackService: welcomeBack ?? new StubWelcomeBack(),
            notesService: notes,
            systemMonitorService: systemMonitor);
        vm.CurrentMode = PanelMode.Compact;
        return vm;
    }

    public static SettingsViewModel CreateSettings(MemorySettings settings) => new(settings, _ => { });
}
