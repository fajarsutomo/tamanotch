using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TamaNotch.Services.Dns;
using Xunit;

namespace TamaNotch.Tests;

public class DohTests
{
    // --- Resolver ---

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public List<string> Urls { get; } = new();
        public List<string?> Accepts { get; } = new();
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            lock (Urls)
            {
                Urls.Add(request.RequestUri!.ToString());
                Accepts.Add(request.Headers.Accept.ToString());
            }
            return Task.FromResult(_respond(request));
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode code = HttpStatusCode.OK)
        => new(code) { Content = new StringContent(body) };

    private static string Answer(string type, string data, int ttl = 300)
        => $"{{\"Status\":0,\"Answer\":[{{\"name\":\"x\",\"type\":{type},\"TTL\":{ttl},\"data\":\"{data}\"}}]}}";

    private static HttpResponseMessage Dns(HttpRequestMessage r, string v4, string? v6 = null, int ttl = 300)
    {
        var query = r.RequestUri!.Query;
        if (query.Contains("type=AAAA")) return v6 == null ? Json("{\"Status\":0}") : Json(Answer("28", v6, ttl));
        return Json(Answer("1", v4, ttl));
    }

    [Fact]
    public async Task Resolve_UsesDohEndpointByIpLiteral_AndReturnsIPv4ThenIPv6()
    {
        var handler = new StubHandler(r => Dns(r, "104.21.56.92", "2606:4700:3036::ac43:b7b9"));
        using var resolver = new DohResolver(null, new HttpClient(handler));

        var result = await resolver.ResolveAsync("myflix.tamahome.id");

        Assert.False(result.FromCache);
        Assert.Equal("Cloudflare 1.1.1.1", result.Resolver);
        Assert.Equal(new[] { "104.21.56.92", "2606:4700:3036::ac43:b7b9" }, result.Addresses.Select(a => a.ToString()));
        // Bukti permintaan DNS dikirim ke endpoint DoH publik lewat IP literal, bukan lewat nama host.
        Assert.All(handler.Urls, u => Assert.StartsWith("https://1.1.1.1/dns-query?name=myflix.tamahome.id&type=", u));
        Assert.Contains(handler.Urls, u => u.EndsWith("type=A"));
        Assert.Contains(handler.Urls, u => u.EndsWith("type=AAAA"));
        Assert.All(handler.Accepts, a => Assert.Equal("application/dns-json", a));
    }

    [Fact]
    public void EndpointsFor_PutsChosenProviderFirst_AndOtherAsFallback()
    {
        Assert.StartsWith("https://1.1.1.1", DohResolver.EndpointsFor("Cloudflare")[0].Url);
        Assert.StartsWith("https://8.8.8.8", DohResolver.EndpointsFor("Google")[0].Url);
        Assert.StartsWith("https://8.8.8.8", DohResolver.EndpointsFor("Cloudflare")[2].Url);
        Assert.StartsWith("https://1.1.1.1", DohResolver.EndpointsFor("Google")[2].Url);
        Assert.StartsWith("https://1.1.1.1", DohResolver.EndpointsFor("tidak dikenal")[0].Url);
    }

    [Fact]
    public async Task Resolve_CachesByTtl_AndRefetchesAfterExpiry()
    {
        var now = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var handler = new StubHandler(r => Dns(r, "104.21.56.92", ttl: 120));
        using var resolver = new DohResolver(null, new HttpClient(handler), utcNow: () => now);

        await resolver.ResolveAsync("a.contoh.id");
        var second = await resolver.ResolveAsync("A.contoh.id");
        Assert.True(second.FromCache);
        var calls = handler.Urls.Count;

        now = now.AddSeconds(121);
        var third = await resolver.ResolveAsync("a.contoh.id");
        Assert.False(third.FromCache);
        Assert.True(handler.Urls.Count > calls);
    }

    [Fact]
    public async Task Resolve_FallsBackToSecondEndpoint_WhenFirstFails()
    {
        var handler = new StubHandler(r => r.RequestUri!.Host == "1.1.1.1"
            ? throw new HttpRequestException("diblokir")
            : Dns(r, "172.67.183.185"));
        using var resolver = new DohResolver("Cloudflare", new HttpClient(handler),
            new[] { new DohEndpoint("A", "https://1.1.1.1/dns-query"), new DohEndpoint("B", "https://8.8.8.8/resolve") });

        var result = await resolver.ResolveAsync("x.contoh.id");

        Assert.Equal("B", result.Resolver);
    }

    [Fact]
    public async Task Resolve_WhenEveryEndpointFails_Throws_InsteadOfUsingWindowsDns()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("diblokir firewall"));
        using var resolver = new DohResolver(null, new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<DohException>(() => resolver.ResolveAsync("localhost"));

        Assert.Contains("DNS-over-HTTPS", ex.Message);
        Assert.Contains("diblokir firewall", ex.Message);
    }

    [Fact]
    public async Task Resolve_Nxdomain_StopsWithoutTryingOtherProviders()
    {
        var handler = new StubHandler(_ => Json("{\"Status\":3}"));
        using var resolver = new DohResolver(null, new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<DohException>(() => resolver.ResolveAsync("tidak-ada.contoh.id"));

        Assert.Contains("NXDOMAIN", ex.Message);
        Assert.All(handler.Urls, u => Assert.StartsWith("https://1.1.1.1", u));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    public async Task Resolve_RejectsLoopbackAndNonsenseAnswers(string bad)
    {
        var handler = new StubHandler(r => Dns(r, bad));
        using var resolver = new DohResolver(null, new HttpClient(handler));

        await Assert.ThrowsAsync<DohException>(() => resolver.ResolveAsync("rebind.contoh.id"));
    }

    [Fact]
    public async Task Resolve_IgnoresCnameRecords_AndFailureOfAaaaDoesNotBreakIPv4()
    {
        var handler = new StubHandler(r =>
        {
            if (r.RequestUri!.Query.Contains("type=AAAA")) throw new HttpRequestException("aaaa gagal");
            return Json("{\"Status\":0,\"Answer\":[{\"type\":5,\"TTL\":60,\"data\":\"alias.contoh.id.\"},{\"type\":1,\"TTL\":60,\"data\":\"104.21.56.92\"}]}");
        });
        using var resolver = new DohResolver(null, new HttpClient(handler));

        var result = await resolver.ResolveAsync("x.contoh.id");

        Assert.Equal(new[] { "104.21.56.92" }, result.Addresses.Select(a => a.ToString()));
    }

    // --- Proxy ---

    private sealed class FakeResolver : IHostResolver
    {
        public List<string> Asked { get; } = new();
        public Task<DnsLookupResult> ResolveAsync(string host, CancellationToken ct = default)
        {
            Asked.Add(host);
            if (host == "gagal.contoh.id") throw new DohException("DoH diblokir");
            return Task.FromResult(new DnsLookupResult(new[] { IPAddress.Loopback }, "Fake DoH", false));
        }
    }

    private static async Task<string> SendRawAsync(int proxyPort, string request)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxyPort);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request));
        var buffer = new byte[512];
        var read = await stream.ReadAsync(buffer);
        return Encoding.ASCII.GetString(buffer, 0, read);
    }

    [Theory]
    [InlineData("CONNECT myflix.tamahome.id:443 HTTP/1.1\r\nHost: x", "myflix.tamahome.id", 443)]
    [InlineData("CONNECT [2606:4700::1]:443 HTTP/1.1", "2606:4700::1", 443)]
    [InlineData("connect 1.2.3.4:8443 HTTP/1.1", "1.2.3.4", 8443)]
    public void ParseConnect_AcceptsValidTargets(string head, string host, int port)
    {
        Assert.True(DohProxyService.TryParseConnect(head, out var h, out var p, out _));
        Assert.Equal(host, h);
        Assert.Equal(port, p);
    }

    [Theory]
    [InlineData("GET http://contoh.id/ HTTP/1.1", 405)]
    [InlineData("CONNECT contoh.id HTTP/1.1", 400)]
    [InlineData("CONNECT contoh.id:abc HTTP/1.1", 400)]
    [InlineData("CONNECT :443 HTTP/1.1", 400)]
    [InlineData("CONNECT contoh.id:99999 HTTP/1.1", 400)]
    [InlineData("", 400)]
    public void ParseConnect_RejectsInvalidRequests(string head, int status)
    {
        Assert.False(DohProxyService.TryParseConnect(head, out _, out _, out var failure));
        Assert.Equal(status, failure.status);
    }

    [Fact]
    public async Task Proxy_TunnelsThroughResolver_ForHostnameWindowsCannotResolve()
    {
        // Server "asli" lokal yang memantulkan data.
        var echo = new TcpListener(IPAddress.Loopback, 0);
        echo.Start();
        var echoPort = ((IPEndPoint)echo.LocalEndpoint).Port;
        _ = Task.Run(async () =>
        {
            using var c = await echo.AcceptTcpClientAsync();
            var s = c.GetStream();
            var buf = new byte[64];
            var n = await s.ReadAsync(buf);
            await s.WriteAsync(buf.AsMemory(0, n));
        });

        var resolver = new FakeResolver();
        using var proxy = new DohProxyService(resolver, new[] { echoPort });
        proxy.Start();

        using var client = new TcpClient();
        await client.ConnectAsync(IPAddress.Loopback, proxy.Port);
        var stream = client.GetStream();
        await stream.WriteAsync(Encoding.ASCII.GetBytes($"CONNECT tidak-ada-di-dns-windows.invalid:{echoPort} HTTP/1.1\r\n\r\n"));

        var buffer = new byte[256];
        var read = await stream.ReadAsync(buffer);
        Assert.StartsWith("HTTP/1.1 200", Encoding.ASCII.GetString(buffer, 0, read));

        await stream.WriteAsync(Encoding.ASCII.GetBytes("halo"));
        read = await stream.ReadAsync(buffer);
        Assert.Equal("halo", Encoding.ASCII.GetString(buffer, 0, read));

        // Nama yang mustahil di-resolve Windows tetap tersambung, dan resolver DoH yang ditanya.
        Assert.Equal(new[] { "tidak-ada-di-dns-windows.invalid" }, resolver.Asked);
        echo.Stop();
    }

    [Fact]
    public async Task Proxy_RefusesOtherPortsAndMethods_SoItIsNotAnOpenProxy()
    {
        var resolver = new FakeResolver();
        using var proxy = new DohProxyService(resolver);
        proxy.Start();

        Assert.StartsWith("HTTP/1.1 403", await SendRawAsync(proxy.Port, "CONNECT contoh.id:80 HTTP/1.1\r\n\r\n"));
        Assert.StartsWith("HTTP/1.1 403", await SendRawAsync(proxy.Port, "CONNECT contoh.id:22 HTTP/1.1\r\n\r\n"));
        Assert.StartsWith("HTTP/1.1 405", await SendRawAsync(proxy.Port, "GET http://contoh.id/ HTTP/1.1\r\n\r\n"));
        Assert.Empty(resolver.Asked); // ditolak sebelum ada resolusi DNS
    }

    [Fact]
    public async Task Proxy_ReportsDohFailure_AsBadGateway_WithoutFallingBackToWindowsDns()
    {
        using var proxy = new DohProxyService(new FakeResolver());
        proxy.Start();

        Assert.StartsWith("HTTP/1.1 502", await SendRawAsync(proxy.Port, "CONNECT gagal.contoh.id:443 HTTP/1.1\r\n\r\n"));
    }

    [Fact]
    public async Task Proxy_ListensOnLoopbackOnly_AndStopsOnDispose()
    {
        var proxy = new DohProxyService(new FakeResolver());
        proxy.Start();
        var port = proxy.Port;

        Assert.True(proxy.IsRunning);
        Assert.True(port > 0);

        proxy.Dispose();

        Assert.False(proxy.IsRunning);
        using var client = new TcpClient();
        await Assert.ThrowsAnyAsync<SocketException>(() => client.ConnectAsync(IPAddress.Loopback, port));
    }

    [Fact]
    public async Task Proxy_LogsHostResolverCacheAndResult()
    {
        var lines = new List<string>();
        DohLog.FilePath = null;
        DohLog.Sink = l => { lock (lines) lines.Add(l); };
        try
        {
            using var proxy = new DohProxyService(new FakeResolver(), new[] { 1 });
            proxy.Start();
            await SendRawAsync(proxy.Port, "CONNECT gagal.contoh.id:1 HTTP/1.1\r\n\r\n");
            await Task.Delay(100);

            lock (lines) Assert.Contains(lines, l => l.Contains("gagal gagal.contoh.id") && l.Contains("DoH diblokir"));
        }
        finally { DohLog.Sink = null; }
    }

    // --- Integrasi WebView2 & pengaturan ---

    [Fact]
    public void BuildArguments_OnlyAddsProxy_WhenEnabled_AndOnlyForHttps()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        WebViewDns.Settings = settings;
        try
        {
            Assert.Equal("--x", WebViewDns.BuildArguments("--x")); // bawaan: mati

            settings.CurrentSettings.DohEnabled = true;
            var args = WebViewDns.BuildArguments("--x");

            Assert.StartsWith("--x --proxy-server=\"https=127.0.0.1:", args);
            Assert.DoesNotContain("socks", args);
            Assert.Equal(args, WebViewDns.BuildArguments("--x")); // proxy yang sama dipakai bersama
        }
        finally
        {
            WebViewDns.Shutdown();
            WebViewDns.Settings = null;
        }
    }

    [Fact]
    public void SettingsViewModel_DohOptions_ArePersisted_AndOffByDefault()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateSettings(settings);

        Assert.False(vm.DohEnabled);
        Assert.Equal("Cloudflare", vm.DohProvider);

        vm.DohEnabled = true;
        vm.DohProvider = "Google";

        Assert.True(settings.CurrentSettings.DohEnabled);
        Assert.Equal("Google", settings.CurrentSettings.DohProvider);
    }
}
