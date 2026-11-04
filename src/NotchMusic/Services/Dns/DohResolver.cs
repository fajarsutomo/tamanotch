using System.IO;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

namespace NotchMusic.Services.Dns;

/// <summary>Satu endpoint DNS-over-HTTPS (format JSON). Dihubungi lewat IP literal supaya tidak butuh DNS Windows.</summary>
public sealed record DohEndpoint(string Name, string Url);

public sealed record DnsLookupResult(IReadOnlyList<IPAddress> Addresses, string Resolver, bool FromCache);

public interface IHostResolver
{
    Task<DnsLookupResult> ResolveAsync(string host, CancellationToken ct = default);
}

public sealed class DohException : Exception
{
    public DohException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Resolver nama host lewat DoH publik (Cloudflare/Google) dengan cache berdasar TTL.
/// Tidak pernah memanggil resolver DNS Windows; bila semua endpoint gagal, melempar <see cref="DohException"/>.
/// </summary>
public sealed class DohResolver : IHostResolver, IDisposable
{
    public const string CloudflareProvider = "Cloudflare";
    public const string GoogleProvider = "Google";

    private static readonly DohEndpoint[] Cloudflare =
    {
        new("Cloudflare 1.1.1.1", "https://1.1.1.1/dns-query"),
        new("Cloudflare 1.0.0.1", "https://1.0.0.1/dns-query")
    };

    private static readonly DohEndpoint[] Google =
    {
        new("Google 8.8.8.8", "https://8.8.8.8/resolve"),
        new("Google 8.8.4.4", "https://8.8.4.4/resolve")
    };

    private static readonly TimeSpan MinTtl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan MaxTtl = TimeSpan.FromHours(1);

    private sealed record CacheEntry(IReadOnlyList<IPAddress> Addresses, string Resolver, DateTime ExpiresUtc);

    private readonly HttpClient _http;
    private readonly bool _ownsClient;
    private readonly IReadOnlyList<DohEndpoint> _endpoints;
    private readonly Func<DateTime> _utcNow;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Urutan endpoint: penyedia utama dulu, lalu penyedia lainnya sebagai cadangan.</summary>
    public static IReadOnlyList<DohEndpoint> EndpointsFor(string? primaryProvider)
        => string.Equals(primaryProvider, GoogleProvider, StringComparison.OrdinalIgnoreCase)
            ? Google.Concat(Cloudflare).ToArray()
            : Cloudflare.Concat(Google).ToArray();

    public DohResolver(string? primaryProvider = null, HttpClient? http = null, IReadOnlyList<DohEndpoint>? endpoints = null, Func<DateTime>? utcNow = null)
    {
        _endpoints = endpoints ?? EndpointsFor(primaryProvider);
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
        _ownsClient = http == null;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(6) };
    }

    public async Task<DnsLookupResult> ResolveAsync(string host, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host)) throw new DohException("Nama host kosong.");
        host = host.Trim().TrimEnd('.');

        if (_cache.TryGetValue(host, out var cached) && cached.ExpiresUtc > _utcNow())
        {
            return new DnsLookupResult(cached.Addresses, cached.Resolver, true);
        }

        var errors = new List<string>();
        foreach (var endpoint in _endpoints)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var answer = await QueryBothAsync(endpoint, host, ct);
                if (answer.NxDomain)
                {
                    // Jawaban resmi "tidak ada": tidak perlu dicoba ke penyedia lain.
                    throw new DohException($"Nama host '{host}' tidak ditemukan (NXDOMAIN) menurut {endpoint.Name}.");
                }
                if (answer.Addresses.Count == 0)
                {
                    errors.Add($"{endpoint.Name}: tidak ada alamat IP untuk '{host}'");
                    continue;
                }

                var ttl = answer.MinTtl < MinTtl ? MinTtl : answer.MinTtl > MaxTtl ? MaxTtl : answer.MinTtl;
                _cache[host] = new CacheEntry(answer.Addresses, endpoint.Name, _utcNow() + ttl);
                return new DnsLookupResult(answer.Addresses, endpoint.Name, false);
            }
            catch (DohException) { throw; }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                errors.Add($"{endpoint.Name}: {ex.Message}");
            }
        }

        throw new DohException(
            "Semua endpoint DoH gagal dihubungi (jaringan mungkin memblokir DNS-over-HTTPS): " + string.Join("; ", errors));
    }

    public void ClearCache() => _cache.Clear();

    private sealed record Answer(List<IPAddress> Addresses, TimeSpan MinTtl, bool NxDomain);

    private async Task<Answer> QueryBothAsync(DohEndpoint endpoint, string host, CancellationToken ct)
    {
        var a = QueryAsync(endpoint, host, "A", ct);
        var aaaa = QueryAsync(endpoint, host, "AAAA", ct);
        var v4 = await a;
        // AAAA bersifat tambahan: kegagalannya tidak boleh menggagalkan hasil IPv4.
        Answer v6;
        try { v6 = await aaaa; } catch (Exception) when (!ct.IsCancellationRequested) { v6 = new Answer(new List<IPAddress>(), MaxTtl, false); }

        var all = new List<IPAddress>(v4.Addresses);
        all.AddRange(v6.Addresses);
        var ttl = v4.Addresses.Count > 0 && v6.Addresses.Count > 0
            ? (v4.MinTtl < v6.MinTtl ? v4.MinTtl : v6.MinTtl)
            : v4.Addresses.Count > 0 ? v4.MinTtl : v6.MinTtl;
        return new Answer(all, ttl, v4.NxDomain);
    }

    private async Task<Answer> QueryAsync(DohEndpoint endpoint, string host, string type, CancellationToken ct)
    {
        var url = $"{endpoint.Url}?name={Uri.EscapeDataString(host)}&type={type}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Accept.ParseAdd("application/dns-json");

        using var response = await _http.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"HTTP {(int)response.StatusCode}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return Parse(json, type == "A" ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6);
    }

    private static Answer Parse(string json, AddressFamily wanted)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var status = root.TryGetProperty("Status", out var st) && st.ValueKind == JsonValueKind.Number ? st.GetInt32() : -1;
        if (status == 3) return new Answer(new List<IPAddress>(), MaxTtl, true);
        if (status != 0) throw new InvalidDataException($"status DNS {status}");

        var addresses = new List<IPAddress>();
        var minTtl = MaxTtl;
        if (root.TryGetProperty("Answer", out var answers) && answers.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in answers.EnumerateArray())
            {
                var type = item.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : 0;
                if (type != (wanted == AddressFamily.InterNetwork ? 1 : 28)) continue; // abaikan CNAME dsb.

                var data = item.TryGetProperty("data", out var d) ? d.GetString() : null;
                if (!IPAddress.TryParse(data, out var ip) || ip.AddressFamily != wanted || !IsAcceptable(ip)) continue;

                addresses.Add(ip);
                if (item.TryGetProperty("TTL", out var ttl) && ttl.ValueKind == JsonValueKind.Number)
                {
                    var span = TimeSpan.FromSeconds(Math.Max(0, ttl.GetInt32()));
                    if (span < minTtl) minTtl = span;
                }
            }
        }

        return new Answer(addresses, minTtl, false);
    }

    /// <summary>Tolak jawaban yang tidak masuk akal untuk situs publik (cegah DNS rebinding ke mesin lokal).</summary>
    internal static bool IsAcceptable(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)) return false;
        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            if (b[0] == 0 || b[0] >= 224) return false; // 0.0.0.0/8, multicast, cadangan
        }
        else if (ip.IsIPv6Multicast)
        {
            return false;
        }
        return true;
    }

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
    }
}
