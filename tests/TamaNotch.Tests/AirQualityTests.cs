using System.Net;
using System.Net.Http;
using TamaNotch.Services.AirQuality;
using Xunit;

namespace TamaNotch.Tests;

public class AirQualityTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 12, 0, 0);

    private const string Sample =
        "{\"latitude\":-6.2,\"current\":{\"time\":\"2026-11-04T12:00\",\"interval\":3600,\"us_aqi\":57,\"pm2_5\":14.24,\"pm10\":20.1}}";

    [Fact]
    public void Parse_ReadsAqiAndParticles()
    {
        var r = AirQualityService.Parse(Sample, Now);

        Assert.NotNull(r);
        Assert.Equal(57, r!.UsAqi);
        Assert.Equal(14.24, r.Pm25);
        Assert.Equal(20.1, r.Pm10);
        Assert.Equal(Now, r.FetchedAt);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"current\":{\"us_aqi\":null}}")]
    [InlineData("{\"current\":{\"pm10\":3}}")]
    [InlineData("{\"error\":true,\"reason\":\"out of range\"}")]
    public void Parse_ReturnsNull_WhenAqiMissing(string json)
        => Assert.Null(AirQualityService.Parse(json, Now));

    [Fact]
    public void Parse_AllowsMissingParticles()
    {
        var r = AirQualityService.Parse("{\"current\":{\"us_aqi\":40}}", Now);

        Assert.NotNull(r);
        Assert.Null(r!.Pm25);
        Assert.Null(r.Pm10);
    }

    [Fact]
    public void BuildUrl_UsesInvariantCoordinates()
    {
        var url = AirQualityService.BuildUrl(-6.2088, 106.8456);

        Assert.Equal(
            "https://air-quality-api.open-meteo.com/v1/air-quality?latitude=-6.2088&longitude=106.8456&current=us_aqi,pm2_5,pm10&timezone=auto",
            url);
    }

    [Theory]
    [InlineData(0, "Baik")]
    [InlineData(50, "Baik")]
    [InlineData(51, "Sedang")]
    [InlineData(100, "Sedang")]
    [InlineData(101, "Tidak sehat bagi kelompok sensitif")]
    [InlineData(150, "Tidak sehat bagi kelompok sensitif")]
    [InlineData(151, "Tidak sehat")]
    [InlineData(201, "Sangat tidak sehat")]
    [InlineData(301, "Berbahaya")]
    public void Scale_MapsUsAqiBands(int aqi, string label)
        => Assert.Equal(label, AirQualityScale.Describe(aqi).Label);

    [Fact]
    public void Reading_GoesStaleAfterTwoHours()
    {
        var r = new AirQualityReading(50, null, null, Now);

        Assert.True(r.IsFresh(Now.AddHours(2)));
        Assert.False(r.IsFresh(Now.AddHours(2).AddSeconds(1)));
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

    private static HttpResponseMessage Ok(string json)
        => new(HttpStatusCode.OK) { Content = new StringContent(json) };

    [Fact]
    public async Task Refresh_FetchesForCurrentLocation_AndRaisesUpdated()
    {
        var handler = new Handler(_ => Ok(Sample));
        using var service = new AirQualityService(() => (-6.2, 106.8), () => true, new HttpClient(handler), useTimer: false);
        int raised = 0;
        service.Updated += (_, _) => raised++;

        await service.RefreshAsync();

        Assert.Equal(57, service.Current!.UsAqi);
        Assert.Equal(1, raised);
        Assert.Contains("latitude=-6.2&longitude=106.8", handler.Requests.Single().Query);
    }

    [Fact]
    public async Task Refresh_DoesNothing_WhenDisabled()
    {
        var handler = new Handler(_ => Ok(Sample));
        using var service = new AirQualityService(() => (0, 0), () => false, new HttpClient(handler), useTimer: false);

        await service.RefreshAsync();

        Assert.Null(service.Current);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Refresh_KeepsPreviousReading_OnServerErrorOrBadJson()
    {
        bool fail = false;
        var handler = new Handler(_ => fail ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Ok(Sample));
        using var service = new AirQualityService(() => (0, 0), () => true, new HttpClient(handler), useTimer: false);
        await service.RefreshAsync();

        fail = true;
        await service.RefreshAsync();

        Assert.Equal(57, service.Current!.UsAqi);

        var bad = new Handler(_ => Ok("not json"));
        using var service2 = new AirQualityService(() => (0, 0), () => true, new HttpClient(bad), useTimer: false);
        await service2.RefreshAsync();
        Assert.Null(service2.Current);
    }

    [Fact]
    public void Island_ShowsBadgeOnlyWithFreshReading_AndWhenEnabled()
    {
        var air = new ViewModelTestFactory.StubAirQuality();
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateMain(settings, air);
        Assert.False(vm.ShowAirQualityBadge);

        air.Current = new AirQualityReading(57, 14.2, 20.1, DateTime.Now);
        air.Raise();
        // Updated dapat dialihkan lewat dispatcher; panggil pembaruan langsung bila belum terjadi.
        vm.ShowAirQualityInIsland = true;

        Assert.True(vm.ShowAirQualityBadge);
        Assert.Equal("AQI 57", vm.AirQualityBadgeText);
        Assert.Contains("Sedang", vm.AirQualityTooltip);
        Assert.Contains("PM2.5 14.2", vm.AirQualityTooltip);
        Assert.Contains("AQI 57", vm.BmkgNotchTooltip);

        vm.ShowAirQualityInIsland = false;
        Assert.False(vm.ShowAirQualityBadge);

        vm.ShowAirQualityInIsland = true;
        air.Current = new AirQualityReading(57, null, null, DateTime.Now.AddHours(-3));
        Assert.False(vm.ShowAirQualityBadge);
    }
}
