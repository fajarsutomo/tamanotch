using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NotchMusic.Helpers;
using NotchMusic.Views;
using Xunit;

namespace NotchMusic.Tests;

public class JellyfinTests
{
    // --- Alamat server ---

    [Theory]
    [InlineData(null, "https://myflix.tamahome.id/")]
    [InlineData("", "https://myflix.tamahome.id/")]
    [InlineData("   ", "https://myflix.tamahome.id/")]
    [InlineData("jellyfin.contoh.id", "https://jellyfin.contoh.id/")]
    [InlineData("  https://media.contoh.id/web/  ", "https://media.contoh.id/web/")]
    [InlineData("http://192.168.1.10:8096", "http://192.168.1.10:8096/")]
    [InlineData("ftp://contoh.id", "https://myflix.tamahome.id/")]
    [InlineData("https://", "https://myflix.tamahome.id/")]
    public void Normalize_ReturnsUsableHttpAddress(string? input, string expected)
    {
        Assert.Equal(expected, JellyfinAddress.Normalize(input).ToString());
    }

    [Theory]
    [InlineData("https://myflix.tamahome.id/", "https://myflix.tamahome.id/")]
    [InlineData("https://myflix.tamahome.id/web/", "https://myflix.tamahome.id/")]
    [InlineData("https://myflix.tamahome.id/web/index.html#!/home", "https://myflix.tamahome.id/")]
    [InlineData("https://contoh.id/jellyfin/web/", "https://contoh.id/jellyfin")]
    [InlineData("https://contoh.id/jellyfin/", "https://contoh.id/jellyfin")]
    [InlineData("http://192.168.1.10:8096/", "http://192.168.1.10:8096/")]
    public void ApiBase_StripsWebClientPath_KeepingSubPath(string address, string expected)
    {
        var baseUri = JellyfinServer.ApiBase(new Uri(address));

        Assert.Equal(expected.TrimEnd('/'), baseUri.ToString().TrimEnd('/'));
    }

    // --- Pemeriksaan server ---

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public string? LastUrl { get; private set; }
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastUrl = request.RequestUri?.ToString();
            return Task.FromResult(_respond(request));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new StringContent(body) };

    [Fact]
    public async Task Check_JellyfinServer_ReturnsNameAndVersion_AndCallsPublicInfoEndpoint()
    {
        var handler = new StubHandler(_ => Json("{\"ServerName\":\"myFlix\",\"Version\":\"12.4.0\",\"ProductName\":\"Jellyfin Server\"}"));
        using var http = new HttpClient(handler);

        var result = await JellyfinServer.CheckAsync(new Uri("https://myflix.tamahome.id/web/"), http);

        Assert.True(result.IsJellyfin);
        Assert.Equal("myFlix", result.ServerName);
        Assert.Equal("12.4.0", result.Version);
        Assert.Equal("https://myflix.tamahome.id/System/Info/Public", handler.LastUrl);
    }

    [Fact]
    public async Task Check_OtherProduct_IsNotJellyfin()
    {
        using var http = new HttpClient(new StubHandler(_ => Json("{\"ProductName\":\"Lainnya\",\"ServerName\":\"x\"}")));

        var result = await JellyfinServer.CheckAsync(new Uri("https://contoh.id/"), http);

        Assert.False(result.IsJellyfin);
        Assert.Contains("bukan server Jellyfin", result.Error);
    }

    [Fact]
    public async Task Check_HttpErrorAndNetworkFailure_AreReportedWithoutThrowing()
    {
        using var notFound = new HttpClient(new StubHandler(_ => Json("", HttpStatusCode.NotFound)));
        var r1 = await JellyfinServer.CheckAsync(new Uri("https://contoh.id/"), notFound);
        Assert.False(r1.IsJellyfin);
        Assert.Contains("404", r1.Error);

        using var broken = new HttpClient(new StubHandler(_ => throw new HttpRequestException("tidak ada koneksi")));
        var r2 = await JellyfinServer.CheckAsync(new Uri("https://contoh.id/"), broken);
        Assert.False(r2.IsJellyfin);
        Assert.Contains("tidak ada koneksi", r2.Error);

        using var garbage = new HttpClient(new StubHandler(_ => Json("<html>bukan json</html>")));
        var r3 = await JellyfinServer.CheckAsync(new Uri("https://contoh.id/"), garbage);
        Assert.False(r3.IsJellyfin);
    }

    // --- User-Agent mobile ---

    [Fact]
    public void MobileUserAgent_UsesBrowserMajorVersion_OrFallback()
    {
        Assert.Contains("Chrome/131.0.0.0", MobileUserAgent.Build("131.0.2903.51"));
        Assert.Contains("Mobile Safari", MobileUserAgent.Build("131.0.2903.51"));
        Assert.Contains("Android", MobileUserAgent.Build("131.0.2903.51"));
        Assert.Contains($"Chrome/{MobileUserAgent.FallbackChromeMajor}.0.0.0", MobileUserAgent.Build(null));
        Assert.Contains($"Chrome/{MobileUserAgent.FallbackChromeMajor}.0.0.0", MobileUserAgent.Build("abc.def"));
    }

    // --- ViewModel ---

    [Fact]
    public void MainViewModel_OpenJellyfinCommand_RaisesRequest_AndPlayingNotificationRaisesEvent()
    {
        var vm = ViewModelTestFactory.CreateMain();
        int opened = 0, played = 0;
        vm.RequestOpenJellyfin += () => opened++;
        vm.PlaybackStartedOnJellyfin += () => played++;

        vm.OpenJellyfinCommand.Execute(null);
        vm.NotifyJellyfinPlaying();

        Assert.Equal(1, opened);
        Assert.Equal(1, played);
    }

    [Fact]
    public async Task SettingsViewModel_JellyfinUrl_IsTrimmedPersistedAndResolved()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateSettings(settings);

        vm.JellyfinUrl = "  jellyfin.contoh.id  ";

        Assert.Equal("jellyfin.contoh.id", settings.CurrentSettings.JellyfinUrl);
        Assert.Equal("https://jellyfin.contoh.id/", vm.JellyfinResolvedUrl);

        vm.ResetJellyfinUrlCommand.Execute(null);
        Assert.Equal(JellyfinAddress.Default, settings.CurrentSettings.JellyfinUrl);
        Assert.Equal("https://myflix.tamahome.id/", vm.JellyfinResolvedUrl);

        await Task.CompletedTask;
    }

    [Fact]
    public async Task SettingsViewModel_CheckJellyfin_ShowsServerNameOrFailure()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateSettings(settings);

        using (var ok = new HttpClient(new StubHandler(_ => Json("{\"ServerName\":\"myFlix\",\"Version\":\"12.4.0\",\"ProductName\":\"Jellyfin Server\"}"))))
        {
            await vm.CheckJellyfinAsync(ok);
        }
        Assert.Contains("Terhubung", vm.JellyfinStatus);
        Assert.Contains("myFlix", vm.JellyfinStatus);
        Assert.False(vm.IsCheckingJellyfin);

        using (var bad = new HttpClient(new StubHandler(_ => Json("", HttpStatusCode.BadGateway))))
        {
            await vm.CheckJellyfinAsync(bad);
        }
        Assert.Contains("Gagal", vm.JellyfinStatus);
    }

    [Fact]
    public void SettingsViewModel_ChangingTheAddress_ClearsOldStatus()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateSettings(settings);
        vm.JellyfinStatus = "✓ Terhubung: lama";

        vm.JellyfinUrl = "baru.contoh.id";

        Assert.Equal(string.Empty, vm.JellyfinStatus);
    }

    // --- Jendela ---

    [Fact]
    public void JellyfinWindow_Loads_WithoutShowing()
    {
        System.Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var settings = new ViewModelTestFactory.MemorySettings();
                var window = new JellyfinWindow(null, settings);
                window.ApplyTemplate();
                window.Measure(new System.Windows.Size(390, 780));

                Assert.Equal("Jellyfin - TAMANOTCH Player", window.Title);
                Assert.Equal(390, window.Width);
                Assert.Equal(780, window.Height);
                window.Close();
            }
            catch (System.Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null) throw new Xunit.Sdk.XunitException("Gagal memuat JellyfinWindow: " + failure);
    }
}
