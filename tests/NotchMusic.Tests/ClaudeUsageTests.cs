using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NotchMusic.Services.ClaudeUsage;
using Xunit;

namespace NotchMusic.Tests;

public class ClaudeUsageTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 5, 6, 37, 0, TimeSpan.Zero);

    // Bentuk respons nyata (dipangkas) dari endpoint usage akun Pro.
    private const string Sample =
        "{\"five_hour\":{\"utilization\":47.0,\"resets_at\":\"2026-11-05T07:00:00.367042+00:00\"}," +
        "\"seven_day\":{\"utilization\":6.0,\"resets_at\":\"2026-11-12T03:00:00.367063+00:00\"}," +
        "\"seven_day_oauth_apps\":null,\"seven_day_opus\":null,\"seven_day_sonnet\":null,\"tangelo\":null," +
        "\"extra_usage\":{\"is_enabled\":false}," +
        "\"seven_day_breakdown\":{\"rows\":[{\"key\":\"claude_code\",\"display_name\":\"Claude Code\",\"percent\":100}," +
        "{\"key\":\"chat\",\"display_name\":\"Chats\",\"percent\":0}]}}";

    // --- Parse ---

    [Fact]
    public void Parse_ReadsSessionAndWeekly_AndSkipsNullWindows()
    {
        var snapshot = ClaudeUsageService.Parse(Sample, DateTime.Now, "pro");

        Assert.NotNull(snapshot);
        Assert.Equal(new[] { "five_hour", "seven_day" }, snapshot!.Windows.Select(w => w.Key));
        Assert.Equal(47, snapshot.Windows[0].Percent);
        Assert.Equal(ClaudeUsageService.SessionPeriod, snapshot.Windows[0].Period);
        Assert.Equal(6, snapshot.Windows[1].Percent);
        Assert.Equal(ClaudeUsageService.WeeklyPeriod, snapshot.Windows[1].Period);
        Assert.Equal(new DateTime(2026, 11, 5, 7, 0, 0, DateTimeKind.Utc), snapshot.Windows[0].ResetsAt!.Value.UtcDateTime.Date.AddHours(7));
        Assert.Equal(7, snapshot.Windows[0].ResetsAt!.Value.UtcDateTime.Hour);
        Assert.Equal("pro", snapshot.Plan);
    }

    [Fact]
    public void Parse_AddsScopedWeeklyWindows_WhenPresent()
    {
        var json = "{\"five_hour\":{\"utilization\":10,\"resets_at\":null},\"seven_day_opus\":{\"utilization\":33.5,\"resets_at\":\"2026-11-12T03:00:00+00:00\"},\"seven_day_fable\":{\"utilization\":2,\"resets_at\":null}}";

        var snapshot = ClaudeUsageService.Parse(json, DateTime.Now)!;

        Assert.Equal(new[] { "five_hour", "seven_day_opus", "seven_day_fable" }, snapshot.Windows.Select(w => w.Key));
        Assert.Equal("Mingguan Opus", snapshot.Windows[1].Label);
        Assert.Equal("Mingguan Fable", snapshot.Windows[2].Label);
        Assert.Null(snapshot.Windows[0].ResetsAt);
    }

    [Fact]
    public void Parse_ReadsBreakdownShares()
    {
        var snapshot = ClaudeUsageService.Parse(Sample, DateTime.Now)!;

        Assert.Equal(2, snapshot.Shares.Count);
        Assert.Equal(new ClaudeUsageShare("Claude Code", 100), snapshot.Shares[0]);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"five_hour\":null,\"seven_day\":null}")]
    [InlineData("{\"five_hour\":{\"utilization\":null}}")]
    [InlineData("{\"error\":{\"type\":\"authentication_error\"}}")]
    public void Parse_ReturnsNull_WhenNoUsableWindow(string json)
        => Assert.Null(ClaudeUsageService.Parse(json, DateTime.Now));

    [Fact]
    public void Parse_ClampsOutOfRangePercent()
    {
        var snapshot = ClaudeUsageService.Parse("{\"five_hour\":{\"utilization\":140}}", DateTime.Now)!;

        Assert.Equal(100, snapshot.Windows[0].Percent);
    }

    // --- Credentials ---

    [Fact]
    public void ParseCredentials_ReadsTokenExpiryAndPlan()
    {
        var json = "{\"claudeAiOauth\":{\"accessToken\":\"tok\",\"refreshToken\":\"r\",\"expiresAt\":1793865508000,\"subscriptionType\":\"pro\"}}";

        var c = ClaudeUsageService.ParseCredentials(json)!;

        Assert.Equal("tok", c.AccessToken);
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1793865508000), c.ExpiresAt);
        Assert.Equal("pro", c.SubscriptionType);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"claudeAiOauth\":{}}")]
    [InlineData("{\"claudeAiOauth\":{\"accessToken\":\"\"}}")]
    public void ParseCredentials_ReturnsNull_WhenTokenMissing(string json)
        => Assert.Null(ClaudeUsageService.ParseCredentials(json));

    [Fact]
    public void ReadCredentials_ReturnsNull_ForMissingOrBrokenFile()
    {
        Assert.Null(ClaudeUsageService.ReadCredentials(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tama_none_" + Guid.NewGuid().ToString("N"))));

        var broken = System.IO.Path.GetTempFileName();
        try
        {
            System.IO.File.WriteAllText(broken, "{ not json");
            Assert.Null(ClaudeUsageService.ReadCredentials(broken));
        }
        finally
        {
            System.IO.File.Delete(broken);
        }
    }

    // --- Elapsed, level, interval ---

    [Fact]
    public void ElapsedFraction_TracksTheTimeWithinThePeriod()
    {
        var window = new ClaudeUsageWindow("five_hour", "Sesi", 10, Now.AddHours(1), ClaudeUsageService.SessionPeriod);

        Assert.Equal(0.8, window.ElapsedFraction(Now)!.Value, 3);
        Assert.Equal(1.0, window.ElapsedFraction(Now.AddHours(2))!.Value, 3);
        Assert.Null((window with { ResetsAt = null }).ElapsedFraction(Now));
    }

    [Theory]
    [InlineData(10, 0.5, ClaudeUsageLevel.Normal)]
    [InlineData(74, 0.9, ClaudeUsageLevel.Normal)]
    [InlineData(75, 0.9, ClaudeUsageLevel.Warning)]
    [InlineData(89, 0.95, ClaudeUsageLevel.Warning)]
    [InlineData(90, 0.1, ClaudeUsageLevel.Critical)]
    [InlineData(40, 0.1, ClaudeUsageLevel.Warning)]   // jauh mendahului waktu
    [InlineData(40, 0.5, ClaudeUsageLevel.Normal)]
    [InlineData(20, 0.0, ClaudeUsageLevel.Normal)]    // terlalu kecil untuk dianggap mendahului
    [InlineData(40, null, ClaudeUsageLevel.Normal)]
    public void Classify_UsesThresholdsAndPace(double percent, double? elapsed, ClaudeUsageLevel expected)
        => Assert.Equal(expected, ClaudeUsageService.Classify(percent, elapsed));

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 2)]
    [InlineData(5, 5)]
    [InlineData(120, 30)]
    public void ClampInterval_KeepsPollingBetweenTwoAndThirtyMinutes(int minutes, int expected)
        => Assert.Equal(TimeSpan.FromMinutes(expected), ClaudeUsageService.ClampInterval(TimeSpan.FromMinutes(minutes)));

    // --- Format ---

    [Theory]
    [InlineData(0, "sebentar lagi")]
    [InlineData(30, "kurang dari 1m")]
    [InlineData(300, "5m")]
    [InlineData(12600, "3j 30m")]
    [InlineData(5 * 86400 + 4 * 3600, "5h 4j")]
    public void Duration_IsCompactIndonesian(int seconds, string expected)
        => Assert.Equal(expected, ClaudeUsageFormat.Duration(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Reset_ShowsCountdownWithinADay_AndWeekdayBeyond()
    {
        Assert.StartsWith("Reset dalam 23m (", ClaudeUsageFormat.Reset(Now.AddMinutes(23), Now));
        Assert.StartsWith("Reset ", ClaudeUsageFormat.Reset(Now.AddDays(3), Now));
        Assert.Contains("dalam 3h", ClaudeUsageFormat.Reset(Now.AddDays(3), Now));
        Assert.Matches(@"\(\d{2}:\d{2}\)$", ClaudeUsageFormat.Reset(Now.AddMinutes(23), Now));   // jam selalu "HH:mm", bukan "HH.mm" ala id-ID`n        Assert.Equal("Segera direset", ClaudeUsageFormat.Reset(Now.AddMinutes(-1), Now));
        Assert.Equal("Waktu reset tidak diketahui", ClaudeUsageFormat.Reset(null, Now));
    }

    [Fact]
    public void Updated_FlagsStaleData()
    {
        var now = new DateTime(2026, 11, 5, 12, 0, 0);

        Assert.Equal("Diperbarui baru saja", ClaudeUsageFormat.Updated(now.AddSeconds(-20), now));
        Assert.Equal("Diperbarui 5 menit lalu", ClaudeUsageFormat.Updated(now.AddMinutes(-5), now));
        Assert.EndsWith("(data lama)", ClaudeUsageFormat.Updated(now.AddMinutes(-20), now));
        Assert.Equal(string.Empty, ClaudeUsageFormat.Updated(null, now));
    }

    [Theory]
    [InlineData("pro", "Claude Pro")]
    [InlineData("MAX", "Claude Max")]
    [InlineData(null, "")]
    public void PlanName_Capitalizes(string? raw, string expected)
        => Assert.Equal(expected, ClaudeUsageFormat.PlanName(raw));

    // --- Service ---

    private sealed class FakeHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.OK);
        public List<HttpRequestMessage> Requests { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(Respond(request));
        }
    }

    private static ClaudeUsageService CreateService(FakeHandler handler, Func<ClaudeCredentials?>? credentials = null, Func<bool>? enabled = null, Func<DateTimeOffset>? now = null)
        => new(enabled ?? (() => true), () => TimeSpan.FromMinutes(3),
            credentials ?? (() => new ClaudeCredentials("secret-token", Now.AddHours(2), "pro")),
            new HttpClient(handler), now ?? (() => Now), useTimer: false);

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body) };

    [Fact]
    public async Task Refresh_SendsBearerTokenAndBetaHeader_AndStoresSnapshot()
    {
        var handler = new FakeHandler { Respond = _ => Json(Sample) };
        var service = CreateService(handler);
        var raised = 0;
        service.Updated += (s, e) => raised++;

        await service.RefreshAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(ClaudeUsageService.Endpoint, request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("secret-token", request.Headers.Authorization.Parameter);
        Assert.Contains("oauth-2025-04-20", request.Headers.GetValues("anthropic-beta"));
        Assert.Equal(ClaudeUsageStatus.Ok, service.Status);
        Assert.Equal(47, service.Current!.Windows[0].Percent);
        Assert.Equal("pro", service.Current.Plan);
        Assert.Equal(1, raised);
    }

    [Fact]
    public async Task Refresh_DoesNothing_WhenDisabled()
    {
        var handler = new FakeHandler();
        var service = CreateService(handler, enabled: () => false);

        await service.RefreshAsync();

        Assert.Empty(handler.Requests);
        Assert.Equal(ClaudeUsageStatus.Idle, service.Status);
    }

    [Fact]
    public async Task Refresh_ReportsNoCredentials_WithoutCallingTheApi()
    {
        var handler = new FakeHandler();
        var service = CreateService(handler, credentials: () => null);

        await service.RefreshAsync();

        Assert.Empty(handler.Requests);
        Assert.Equal(ClaudeUsageStatus.NoCredentials, service.Status);
    }

    [Fact]
    public async Task Refresh_ReportsExpiredToken_WithoutCallingTheApi()
    {
        var handler = new FakeHandler();
        var service = CreateService(handler, credentials: () => new ClaudeCredentials("old", Now.AddMinutes(-1), null));

        await service.RefreshAsync();

        Assert.Empty(handler.Requests);
        Assert.Equal(ClaudeUsageStatus.TokenExpired, service.Status);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Refresh_ReportsUnauthorized(HttpStatusCode code)
    {
        var service = CreateService(new FakeHandler { Respond = _ => new HttpResponseMessage(code) });

        await service.RefreshAsync();

        Assert.Equal(ClaudeUsageStatus.Unauthorized, service.Status);
    }

    [Fact]
    public async Task Refresh_KeepsOldNumbers_AndFlagsError_WhenServerFails()
    {
        var ok = true;
        var handler = new FakeHandler { Respond = _ => ok ? Json(Sample) : new HttpResponseMessage(HttpStatusCode.InternalServerError) };
        var service = CreateService(handler);
        await service.RefreshAsync();

        ok = false;
        await service.RefreshAsync();

        Assert.Equal(ClaudeUsageStatus.Error, service.Status);
        Assert.NotNull(service.Current);
    }

    [Fact]
    public async Task Refresh_TreatsUnusableBodyAsError()
    {
        var service = CreateService(new FakeHandler { Respond = _ => Json("{\"error\":\"x\"}") });

        await service.RefreshAsync();

        Assert.Equal(ClaudeUsageStatus.Error, service.Status);
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task Refresh_HonoursRetryAfter_AndSkipsRequestsWhileHeld()
    {
        var clock = Now;
        var handler = new FakeHandler
        {
            Respond = _ =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                r.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMinutes(5));
                return r;
            }
        };
        var service = CreateService(handler, now: () => clock);

        await service.RefreshAsync();
        Assert.Equal(ClaudeUsageStatus.RateLimited, service.Status);
        Assert.Equal(Now.AddMinutes(5), service.NextAllowedAt);

        clock = Now.AddMinutes(2);
        await service.RefreshAsync();
        Assert.Single(handler.Requests);          // masih ditahan, tidak menembak lagi

        handler.Respond = _ => Json(Sample);
        clock = Now.AddMinutes(6);
        await service.RefreshAsync();
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(ClaudeUsageStatus.Ok, service.Status);
        Assert.Null(service.NextAllowedAt);
    }

    [Fact]
    public async Task Refresh_BacksOffExponentially_WithoutRetryAfterHeader()
    {
        var clock = Now;
        var handler = new FakeHandler { Respond = _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests) };
        var service = CreateService(handler, now: () => clock);

        await service.RefreshAsync();
        var first = service.NextAllowedAt!.Value - clock;
        clock = service.NextAllowedAt!.Value.AddSeconds(1);
        await service.RefreshAsync();
        var second = service.NextAllowedAt!.Value - clock;

        Assert.Equal(TimeSpan.FromSeconds(30), first);
        Assert.Equal(TimeSpan.FromSeconds(60), second);
    }

    [Fact]
    public async Task Refresh_NetworkFailure_IsReportedAsError()
    {
        var service = CreateService(new FakeHandler { Respond = _ => throw new HttpRequestException("offline") });

        await service.RefreshAsync();

        Assert.Equal(ClaudeUsageStatus.Error, service.Status);
    }

    [Fact]
    public async Task Stop_ClearsSnapshot_AndRaisesUpdated()
    {
        var service = CreateService(new FakeHandler { Respond = _ => Json(Sample) });
        await service.RefreshAsync();
        var raised = 0;
        service.Updated += (s, e) => raised++;

        service.Stop();

        Assert.Null(service.Current);
        Assert.Equal(ClaudeUsageStatus.Idle, service.Status);
        Assert.Equal(1, raised);
    }

    // --- MainViewModel ---

    [Fact]
    public void ViewModel_DisabledByDefault_ShowsConsentOnly()
    {
        var vm = ViewModelTestFactory.CreateMain();

        Assert.False(vm.EnableClaudeUsage);
        Assert.True(vm.IsClaudeUsageDisabled);
        Assert.False(vm.HasClaudeUsageData);
        Assert.Empty(vm.ClaudeUsageBars);
    }

    [Fact]
    public void ViewModel_EnableAndDisable_StartAndStopTheService_AndPersist()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var stub = new ViewModelTestFactory.StubClaudeUsage();
        var vm = ViewModelTestFactory.CreateMain(settings, claudeUsage: stub);

        vm.EnableClaudeUsageCommand.Execute(null);
        Assert.True(settings.CurrentSettings.EnableClaudeUsage);
        Assert.Equal(1, stub.Starts);

        vm.EnableClaudeUsage = false;
        Assert.False(settings.CurrentSettings.EnableClaudeUsage);
        Assert.Equal(1, stub.Stops);
    }

    [Fact]
    public void ViewModel_BuildsBarsWithLevelColorAndMarker()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        settings.CurrentSettings.EnableClaudeUsage = true;
        var stub = new ViewModelTestFactory.StubClaudeUsage
        {
            Status = ClaudeUsageStatus.Ok,
            Current = new ClaudeUsageSnapshot("pro",
                new[]
                {
                    new ClaudeUsageWindow("five_hour", "Sesi (5 jam)", 47, DateTimeOffset.Now.AddHours(2), ClaudeUsageService.SessionPeriod),
                    new ClaudeUsageWindow("seven_day", "Mingguan (7 hari)", 92, DateTimeOffset.Now.AddDays(3), ClaudeUsageService.WeeklyPeriod)
                },
                new[] { new ClaudeUsageShare("Claude Code", 100), new ClaudeUsageShare("Chats", 0) },
                DateTime.Now)
        };
        var vm = ViewModelTestFactory.CreateMain(settings, claudeUsage: stub);

        var bars = vm.ClaudeUsageBars;

        Assert.True(vm.HasClaudeUsageData);
        Assert.Equal("Claude Pro", vm.ClaudeUsagePlanText);
        Assert.Equal(2, bars.Count);
        Assert.Equal("47%", bars[0].PercentText);
        Assert.Equal(ClaudeUsageLevel.Normal, bars[0].Level);
        Assert.NotNull(bars[0].Elapsed);
        Assert.Equal(ClaudeUsageLevel.Critical, bars[1].Level);
        Assert.NotEqual(((System.Windows.Media.SolidColorBrush)bars[0].Brush).Color, ((System.Windows.Media.SolidColorBrush)bars[1].Brush).Color);
        Assert.Equal(new[] { "Claude Code 100%" }, vm.ClaudeUsageShares);   // porsi 0% disembunyikan
        Assert.False(vm.HasClaudeUsageStatus);
    }

    [Fact]
    public void ViewModel_ShowsStatusMessage_WhenServiceReportsAProblem()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        settings.CurrentSettings.EnableClaudeUsage = true;
        var stub = new ViewModelTestFactory.StubClaudeUsage { Status = ClaudeUsageStatus.TokenExpired };
        var vm = ViewModelTestFactory.CreateMain(settings, claudeUsage: stub);

        Assert.True(vm.HasClaudeUsageStatus);
        Assert.Contains("kedaluwarsa", vm.ClaudeUsageStatusText);
        Assert.False(vm.HasClaudeUsageData);
    }

    [Fact]
    public void ViewModel_SyncWithSettings_StartsWhenEnabledInSettings_AndStopsWhenDisabled()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var stub = new ViewModelTestFactory.StubClaudeUsage();
        var vm = ViewModelTestFactory.CreateMain(settings, claudeUsage: stub);

        settings.CurrentSettings.EnableClaudeUsage = true;
        vm.SyncClaudeUsageWithSettings();
        Assert.Equal(1, stub.Starts);

        settings.CurrentSettings.EnableClaudeUsage = false;
        vm.SyncClaudeUsageWithSettings();
        Assert.Equal(1, stub.Stops);
    }

    [Fact]
    public void ViewModel_WindowOpenState_PersistsAndKeepsTheNotchActive()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateMain(settings);

        vm.IsClaudeUsageWindowOpen = true;

        Assert.True(settings.CurrentSettings.IsClaudeUsageWindowOpen);
        Assert.True(vm.IsNotchActive);
        vm.CloseClaudeUsageWindowCommand.Execute(null);
        Assert.False(settings.CurrentSettings.IsClaudeUsageWindowOpen);
    }
}
