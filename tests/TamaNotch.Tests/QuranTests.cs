using System.Net;
using System.Net.Http;
using TamaNotch.Models;
using TamaNotch.Services.Quran;
using TamaNotch.ViewModels;
using Xunit;

namespace TamaNotch.Tests;

public class QuranTests
{
    private sealed class FakePlayer : IQuranAudioPlayer
    {
        public event Action? Opened;
        public event Action? Ended;
        public event Action<string>? Failed;

        public List<Uri> Opens { get; } = new();
        public int Pauses { get; private set; }
        public int Resumes { get; private set; }
        public int Stops { get; private set; }
        public TimeSpan? SeekedTo { get; private set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; set; }
        public double Volume { get; set; }
        public bool IsMuted { get; set; }

        public void Open(Uri uri) => Opens.Add(uri);
        public void Pause() => Pauses++;
        public void Resume() => Resumes++;
        public void Stop() => Stops++;
        public void Seek(TimeSpan position) => SeekedTo = position;
        public void Dispose() { }

        public void RaiseOpened() => Opened?.Invoke();
        public void RaiseEnded() => Ended?.Invoke();
        public void RaiseFailed(string message = "boom") => Failed?.Invoke(message);
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private static HttpResponseMessage Audio(HttpRequestMessage request)
    {
        var chapter = request.RequestUri!.Segments.Last();
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"{{\"audio_file\":{{\"id\":1,\"chapter_id\":{chapter},\"format\":\"mp3\",\"audio_url\":\"https://download.quranicaudio.com/qdc/mishari_al_afasy/murattal/{chapter}.mp3\"}}}}")
        };
    }

    private static List<QuranSurah> Surahs(int count = 5)
        => Enumerable.Range(1, count).Select(n => new QuranSurah
        {
            Number = n,
            NameSimple = n switch { 1 => "Al-Fatihah", 2 => "Al-Baqarah", 3 => "Ali 'Imran", _ => $"Surah-{n}" },
            NameArabic = n switch { 1 => "الفاتحة", 2 => "البقرة", _ => string.Empty },
            TranslatedName = n == 2 ? "Sapi Betina" : $"Arti {n}",
            VersesCount = 7 + n,
            RevelationPlace = n == 1 ? "makkah" : "madinah"
        }).ToList();

    private static (QuranService Service, FakePlayer Player, Handler Http, ViewModelTestFactory.MemorySettings Settings) Create(
        Func<HttpRequestMessage, HttpResponseMessage>? respond = null, int count = 5, Random? random = null)
    {
        var player = new FakePlayer();
        var handler = new Handler(respond ?? Audio);
        var settings = new ViewModelTestFactory.MemorySettings();
        var service = new QuranService(settings, player, new HttpClient(handler), Surahs(count), random, useTimer: false);
        return (service, player, handler, settings);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        for (int i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition(), "kondisi tidak terpenuhi tepat waktu");
    }

    [Fact]
    public void BundledCatalog_Has114Surahs_WithArabicNames()
    {
        var all = TamaNotch.Helpers.CatalogLoader.Load<QuranSurah>("quran-surahs.json");

        Assert.Equal(114, all.Count);
        Assert.Equal(Enumerable.Range(1, 114), all.Select(s => s.Number));
        Assert.All(all, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.NameSimple));
            Assert.False(string.IsNullOrWhiteSpace(s.NameArabic));
            Assert.True(s.VersesCount > 0);
        });
        Assert.Equal(6236, all.Sum(s => s.VersesCount));
    }

    [Fact]
    public void ParseAudioUri_ReadsHttpsUrl_AndRejectsOthers()
    {
        var uri = QuranService.ParseAudioUri("{\"audio_file\":{\"audio_url\":\"https://download.quranicaudio.com/qdc/x/1.mp3\"}}");
        Assert.Equal("https://download.quranicaudio.com/qdc/x/1.mp3", uri.ToString());

        Assert.Throws<InvalidOperationException>(() => QuranService.ParseAudioUri("{}"));
        Assert.Throws<InvalidOperationException>(() => QuranService.ParseAudioUri("{\"audio_file\":{\"audio_url\":\"http://insecure/1.mp3\"}}"));
        Assert.Throws<InvalidOperationException>(() => QuranService.ParseAudioUri("{\"audio_file\":{\"audio_url\":\"file:///c:/x.mp3\"}}"));
        Assert.Throws<InvalidOperationException>(() => QuranService.ParseAudioUri("{\"audio_file\":{\"audio_url\":5}}"));
    }

    [Fact]
    public async Task PlaySurah_ResolvesUrlFromApi_OpensPlayer_AndBecomesPlayingOnOpened()
    {
        var (service, player, http, settings) = Create();

        service.PlaySurah(service.Surahs[1]);

        Assert.Equal(QuranPlaybackState.Loading, service.PlaybackState);
        await WaitUntil(() => player.Opens.Count == 1);
        Assert.Equal("https://api.quran.com/api/v4/chapter_recitations/7/2", http.Requests.Single().ToString());
        Assert.Equal("https://download.quranicaudio.com/qdc/mishari_al_afasy/murattal/2.mp3", player.Opens.Single().ToString());

        player.RaiseOpened();
        Assert.Equal(QuranPlaybackState.Playing, service.PlaybackState);
        Assert.True(service.Surahs[1].IsPlaying);
        Assert.True(service.Surahs[1].IsCurrent);
        Assert.False(service.Surahs[0].IsCurrent);
        Assert.Equal(2, settings.CurrentSettings.LastQuranSurah);
    }

    [Fact]
    public async Task PlaySurah_CachesUrl_ForRepeatedPlays()
    {
        var (service, player, http, _) = Create();

        service.PlaySurah(service.Surahs[0]);
        await WaitUntil(() => player.Opens.Count == 1);
        service.PlaySurah(service.Surahs[0]);
        await WaitUntil(() => player.Opens.Count == 2);

        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task ApiFailure_ShowsError_AndNeverOpensPlayer()
    {
        var (service, player, _, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        service.PlaySurah(service.Surahs[0]);

        await WaitUntil(() => service.PlaybackState == QuranPlaybackState.Error);
        Assert.Empty(player.Opens);
        Assert.Contains("internet", service.StatusMessage);
    }

    [Fact]
    public async Task StaleResolve_IsIgnored_WhenAnotherSurahWasChosenMeanwhile()
    {
        var gate = new TaskCompletionSource();
        var player = new FakePlayer();
        var handler = new Handler(Audio);
        var slow = new HttpClient(new SlowFirstHandler(handler, gate.Task));
        var service = new QuranService(new ViewModelTestFactory.MemorySettings(), player, slow, Surahs(), useTimer: false);

        service.PlaySurah(service.Surahs[0]);   // tertahan
        service.PlaySurah(service.Surahs[1]);   // selesai lebih dulu
        await WaitUntil(() => player.Opens.Count == 1);
        gate.SetResult();
        await Task.Delay(100);

        Assert.Single(player.Opens);
        Assert.EndsWith("/2.mp3", player.Opens[0].ToString());
    }

    private sealed class SlowFirstHandler(HttpMessageHandler inner, Task gate) : DelegatingHandler(inner)
    {
        private int _calls;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _calls) == 1) await gate;
            return await base.SendAsync(request, cancellationToken);
        }
    }

    [Fact]
    public async Task TogglePlayPause_PausesResumes_AndRestartsFromStopped()
    {
        var (service, player, _, _) = Create();
        service.PlaySurah(service.Surahs[0]);
        await WaitUntil(() => player.Opens.Count == 1);
        player.RaiseOpened();

        service.TogglePlayPause();
        Assert.Equal(QuranPlaybackState.Paused, service.PlaybackState);
        Assert.Equal(1, player.Pauses);

        service.TogglePlayPause();
        Assert.Equal(QuranPlaybackState.Playing, service.PlaybackState);
        Assert.Equal(1, player.Resumes);

        service.Stop();
        Assert.Equal(QuranPlaybackState.Stopped, service.PlaybackState);

        service.TogglePlayPause();
        Assert.Equal(QuranPlaybackState.Loading, service.PlaybackState);
    }

    [Fact]
    public void NextAndPrevious_WrapAroundTheMushaf()
    {
        var (service, _, _, _) = Create(count: 3);

        service.PlaySurah(service.Surahs[2]);
        service.Next();
        Assert.Equal(1, service.CurrentSurah!.Number);

        service.Previous();
        Assert.Equal(3, service.CurrentSurah!.Number);
    }

    [Fact]
    public void PlayRandom_NeverRepeatsTheCurrentSurah()
    {
        var (service, _, _, _) = Create(count: 4, random: new Random(1));
        service.PlaySurah(service.Surahs[0]);

        for (int i = 0; i < 50; i++)
        {
            var before = service.CurrentSurah;
            service.PlayRandom();
            Assert.NotSame(before, service.CurrentSurah);
        }
    }

    [Fact]
    public async Task Ended_FollowsTheChosenMode()
    {
        var (service, player, _, _) = Create();
        service.PlaySurah(service.Surahs[0]);
        await WaitUntil(() => player.Opens.Count == 1);
        player.RaiseOpened();

        service.AfterPlayback = QuranAfterPlayback.Next;
        player.RaiseEnded();
        Assert.Equal(2, service.CurrentSurah!.Number);
        Assert.Equal(QuranPlaybackState.Loading, service.PlaybackState);

        await WaitUntil(() => player.Opens.Count == 2);
        player.RaiseOpened();
        service.AfterPlayback = QuranAfterPlayback.Stop;
        player.RaiseEnded();
        Assert.Equal(QuranPlaybackState.Stopped, service.PlaybackState);
        Assert.Equal(2, service.CurrentSurah!.Number);
    }

    [Fact]
    public async Task PlayerFailure_ShowsError()
    {
        var (service, player, _, _) = Create();
        service.PlaySurah(service.Surahs[0]);
        await WaitUntil(() => player.Opens.Count == 1);

        player.RaiseFailed();

        Assert.Equal(QuranPlaybackState.Error, service.PlaybackState);
    }

    [Fact]
    public async Task SeekToFraction_UsesDuration_OnlyWhilePlayingOrPaused()
    {
        var (service, player, _, _) = Create();
        player.Duration = TimeSpan.FromMinutes(10);

        service.SeekToFraction(0.5);
        Assert.Null(player.SeekedTo);

        service.PlaySurah(service.Surahs[0]);
        await WaitUntil(() => player.Opens.Count == 1);
        player.RaiseOpened();

        service.SeekToFraction(0.25);
        Assert.Equal(TimeSpan.FromMinutes(2.5), player.SeekedTo);

        service.SeekToFraction(7);
        Assert.Equal(TimeSpan.FromMinutes(10), player.SeekedTo);
    }

    [Fact]
    public void Volume_IsPersisted_AndForwardedToThePlayer()
    {
        var (service, player, _, settings) = Create();

        service.Volume = 0.4;
        service.IsMuted = true;

        Assert.Equal(0.4, settings.CurrentSettings.QuranVolume);
        Assert.True(settings.CurrentSettings.QuranMuted);
        Assert.Equal(0.4, player.Volume);
        Assert.True(player.IsMuted);
    }

    [Fact]
    public void Service_RestoresTheLastSurah()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        settings.CurrentSettings.LastQuranSurah = 3;

        var service = new QuranService(settings, new FakePlayer(), new HttpClient(new Handler(Audio)), Surahs(), useTimer: false);

        Assert.Equal(3, service.CurrentSurah!.Number);
        Assert.True(service.CurrentSurah.IsCurrent);
    }

    // --- ViewModel: pencarian, saling jeda ---

    private static MainViewModel CreateVm(out QuranService service, out FakePlayer player)
    {
        var parts = Create();
        service = parts.Service;
        player = parts.Player;
        return ViewModelTestFactory.CreateMain(quran: service);
    }

    private static List<int> Visible(MainViewModel vm)
        => vm.FilteredQuranSurahs.Cast<QuranSurah>().Select(s => s.Number).ToList();

    [Theory]
    [InlineData("baqarah", new[] { 2 })]
    [InlineData("al baqarah", new[] { 2 })]
    [InlineData("AL-FATIHAH", new[] { 1 })]
    [InlineData("imran", new[] { 3 })]
    [InlineData("sapi", new[] { 2 })]
    [InlineData("3", new[] { 3 })]
    [InlineData("البقرة", new[] { 2 })]
    [InlineData("", new[] { 1, 2, 3, 4, 5 })]
    [InlineData("zzz", new int[0])]
    public void Search_MatchesNameMeaningNumberAndArabic(string query, int[] expected)
    {
        var vm = CreateVm(out _, out _);

        vm.QuranSearchQuery = query;

        Assert.Equal(expected, Visible(vm));
    }

    [Fact]
    public async Task StartingQuran_PausesOtherPlayers_AndRaisesEvent()
    {
        var vm = CreateVm(out var service, out var player);
        int raised = 0;
        vm.PlaybackStartedOnQuran += () => raised++;

        vm.PlayQuranSurahCommand.Execute(service.Surahs[0]);
        await WaitUntil(() => player.Opens.Count == 1);
        player.RaiseOpened();

        Assert.Equal(1, raised);
        Assert.True(vm.IsQuranPlaying);
        Assert.True(vm.IsAudioActive);
        Assert.Equal("MEMUTAR", vm.QuranStateBadgeText);
    }

    [Fact]
    public async Task OtherPlayersStarting_StopQuran()
    {
        var vm = CreateVm(out var service, out var player);
        vm.PlayQuranSurahCommand.Execute(service.Surahs[0]);
        await WaitUntil(() => player.Opens.Count == 1);
        player.RaiseOpened();
        Assert.True(vm.IsQuranActive);

        vm.NotifyYouTubeMusicPlaying();
        Assert.False(vm.IsQuranActive);

        vm.PlayQuranSurahCommand.Execute(service.Surahs[1]);
        Assert.True(vm.IsQuranActive);
        vm.NotifySpotifyPlaying();
        Assert.False(vm.IsQuranActive);

        vm.PlayQuranSurahCommand.Execute(service.Surahs[2]);
        Assert.True(vm.IsQuranActive);
        vm.NotifyJellyfinPlaying();
        Assert.False(vm.IsQuranActive);
    }

    [Fact]
    public void FormatTime_UsesHoursOnlyWhenNeeded()
    {
        Assert.Equal("0:00", MainViewModel.FormatQuranTime(TimeSpan.Zero));
        Assert.Equal("5:07", MainViewModel.FormatQuranTime(TimeSpan.FromSeconds(307)));
        Assert.Equal("1:32:05", MainViewModel.FormatQuranTime(TimeSpan.FromSeconds(5525)));
    }

    [Fact]
    public void AfterPlaybackButton_CyclesThroughTheThreeModes()
    {
        var vm = CreateVm(out var service, out _);
        service.AfterPlayback = QuranAfterPlayback.Stop;

        vm.CycleQuranAfterPlaybackCommand.Execute(null);
        Assert.Equal(QuranAfterPlayback.Next, service.AfterPlayback);
        vm.CycleQuranAfterPlaybackCommand.Execute(null);
        Assert.Equal(QuranAfterPlayback.Random, service.AfterPlayback);
        vm.CycleQuranAfterPlaybackCommand.Execute(null);
        Assert.Equal(QuranAfterPlayback.Stop, service.AfterPlayback);
    }
}
