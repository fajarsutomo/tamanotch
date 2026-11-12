using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TamaNotch.Models;
using TamaNotch.Services.Lyrics;
using TamaNotch.Services.Media;
using TamaNotch.Services.Radio;
using TamaNotch.Services.Settings;
using TamaNotch.ViewModels;
using Xunit;

#pragma warning disable CS0067

namespace TamaNotch.Tests;

public class RadioTests
{
    private class FakeMediaSessionService : IMediaSessionService
    {
        public event EventHandler<TrackInfo?>? TrackChanged;
        public event EventHandler<TimeSpan>? TimelineUpdated;
        public event EventHandler<IReadOnlyList<MediaSessionInfo>>? SessionsChanged;

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
        public void Dispose() { }
        public event EventHandler? PlaybackStartedOnSpotify;
        public event EventHandler? PlaybackStartedOnYouTubeMusic;
        public Task PauseSessionByKeywordAsync(string keyword) => Task.CompletedTask;
    }

    private class FakeLyricsService : ILyricsService
    {
        public Task<SongLyrics> GetLyricsAsync(string title, string artist, string album, TimeSpan duration, CancellationToken cancellationToken = default)
            => Task.FromResult(new SongLyrics());
        public Task<List<LrcLibSearchResult>> SearchCandidatesAsync(string query, CancellationToken cancellationToken = default)
            => Task.FromResult(new List<LrcLibSearchResult>());
        public SongLyrics LoadFromLocalFile(string filePath) => new SongLyrics();
    }

    private class FakeSettingsService : ISettingsService
    {
        public AppSettings CurrentSettings { get; } = new();
        public void Save() { }
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => System.IO.Path.GetTempPath();
        public string GetLyricsCachePath() => System.IO.Path.GetTempPath();
        public void ClearLyricsCache() { }
    }

    [Fact]
    public void RadioService_BuiltInStations_ContainsIndonesianAndInternationalStations()
    {
        var settings = new FakeSettingsService();
        using var radioService = new RadioService(settings);

        Assert.NotEmpty(radioService.Stations);
        Assert.Contains(radioService.Stations, s => s.Category == "Indonesia");
        Assert.Contains(radioService.Stations, s => s.Category == "Religi");
        Assert.Contains(radioService.Stations, s => s.Category == "Musik & Lofi");
        Assert.Contains(radioService.Stations, s => s.Category == "Internasional");
        Assert.NotNull(radioService.CurrentStation);
    }

    [Fact]
    public void RadioService_PlayStopNextPrevious_AndFavorites_WorkCorrectly()
    {
        var settings = new FakeSettingsService();
        using var radioService = new RadioService(settings);

        var first = radioService.Stations[0];
        var second = radioService.Stations[1];

        radioService.PlayStation(first);
        Assert.Equal(first, radioService.CurrentStation);
        Assert.True(first.IsCurrent);

        radioService.NextStation();
        Assert.Equal(second, radioService.CurrentStation);
        Assert.True(second.IsCurrent);

        radioService.PreviousStation();
        Assert.Equal(first, radioService.CurrentStation);

        // Favorite toggle
        Assert.False(first.IsFavorite);
        radioService.ToggleFavorite(first);
        Assert.True(first.IsFavorite);
        Assert.Contains(first.Id, settings.CurrentSettings.FavoriteRadioStationIds);

        radioService.Stop();
        Assert.Equal(RadioPlaybackState.Stopped, radioService.PlaybackState);
    }

    [Fact]
    public void MainViewModel_RadioFilterAndCustomStation_WorkCorrectly()
    {
        var settings = new FakeSettingsService();
        var vm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            settings);

        int initialCount = vm.RadioStations.Count;
        Assert.True(initialCount > 0);

        // Filter by Indonesia
        vm.SetRadioCategoryCommand.Execute("Indonesia");
        var filtered = vm.FilteredRadioStations.Cast<RadioStation>().ToList();
        Assert.NotEmpty(filtered);
        Assert.All(filtered, s => Assert.Equal("Indonesia", s.Category));

        // Add custom station
        vm.CustomRadioName = "Radio Test Nusantara";
        vm.CustomRadioUrl = "https://example.com/live.mp3";
        vm.SaveCustomRadioCommand.Execute(null);

        Assert.Equal(initialCount + 1, vm.RadioStations.Count);
        var added = vm.RadioStations.FirstOrDefault(s => s.Name == "Radio Test Nusantara");
        Assert.NotNull(added);
        Assert.True(added.IsCustom);

        // Delete custom station
        vm.DeleteCustomRadioStationCommand.Execute(added);
        Assert.Equal(initialCount, vm.RadioStations.Count);
    }

    [Fact]
    public void RadioWindow_InitializesWithoutErrors()
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            try
            {
                var settings = new FakeSettingsService();
                var mainVm = new MainViewModel(
                    new FakeMediaSessionService(),
                    new FakeLyricsService(),
                    settings);
                var window = new Views.RadioWindow(mainVm, settings);
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        bool finished = thread.Join(5000);

        Assert.True(finished);
        Assert.Null(caught);
    }

    [Fact]
    public void RadioService_NextAndPrevious_WithSingleOrUnmatchedPool_DoesNotThrowIndexOutOfRange()
    {
        var settings = new FakeSettingsService();
        using var service = new RadioService(settings);

        var custom = service.AddCustomStation("Isolated Station", "https://example.com/stream.mp3", "Custom");
        var singlePool = new List<RadioStation> { custom };

        // Current station is not in singlePool
        service.PreviousStation(singlePool);
        Assert.Equal(custom, service.CurrentStation);

        service.NextStation(singlePool);
        Assert.Equal(custom, service.CurrentStation);

        // Empty pool falls back safely to all stations
        service.PreviousStation(Array.Empty<RadioStation>());
        Assert.NotNull(service.CurrentStation);
    }

    [Fact]
    public void MainViewModel_TogglePin_PinsAndPersistsTheSetting()
    {
        var settings = new FakeSettingsService();
        using var radioService = new RadioService(settings);
        var mainVm = new MainViewModel(
            new FakeMediaSessionService(),
            new FakeLyricsService(),
            settings,
            radioService: radioService);

        Assert.False(mainVm.IsPinned);
        Assert.True(mainVm.ShowCompactMusicView);

        mainVm.TogglePinCommand.Execute(null);

        Assert.True(mainVm.IsPinned);
        Assert.True(settings.CurrentSettings.IsPinned);
        Assert.True(mainVm.ShowComponentIcons);
    }
}
