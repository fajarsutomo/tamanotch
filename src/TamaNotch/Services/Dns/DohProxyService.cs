using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace TamaNotch.Services.Dns;

/// <summary>
/// Proxy HTTP CONNECT lokal (hanya 127.0.0.1) yang me-resolve nama host lewat DoH, lalu menyalurkan byte TLS apa adanya.
/// Tidak ada intersepsi TLS: SNI dan validasi sertifikat tetap dilakukan WebView2 terhadap server asli.
/// Hanya CONNECT ke port yang diizinkan (bawaan 443); selain itu ditolak supaya bukan open proxy.
/// </summary>
public sealed class DohProxyService : IDisposable
{
    private const int MaxHeaderBytes = 8192;
    private static readonly TimeSpan HeaderTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(8);

    private readonly IHostResolver _resolver;
    private readonly HashSet<int> _allowedPorts;
    private readonly CancellationTokenSource _cts = new();
    private TcpListener? _listener;

    public DohProxyService(IHostResolver resolver, IEnumerable<int>? allowedPorts = null)
    {
        _resolver = resolver;
        _allowedPorts = new HashSet<int>(allowedPorts ?? new[] { 443 });
    }

    public bool IsRunning { get; private set; }

    /// <summary>Port loopback yang dipakai; valid setelah <see cref="Start"/>.</summary>
    public int Port { get; private set; }

    public void Start()
    {
        if (IsRunning) return;
        try
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            IsRunning = true;
        }
        catch (Exception ex)
        {
            _listener = null;
            throw new InvalidOperationException("Proxy DNS lokal gagal dimulai: " + ex.Message, ex);
        }

        DohLog.Write($"proxy mulai di 127.0.0.1:{Port}");
        _ = Task.Run(() => AcceptLoopAsync(_listener, _cts.Token));
    }

    private async Task AcceptLoopAsync(TcpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync(ct); }
            catch (Exception) when (ct.IsCancellationRequested) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { if (ct.IsCancellationRequested) return; continue; }

            _ = Task.Run(() => HandleClientAsync(client, ct));
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken ct)
    {
        using (client)
        {
            client.NoDelay = true;
            var stream = client.GetStream();
            string host = "?";
            try
            {
                var (head, leftover) = await ReadHeadAsync(stream, ct);
                if (!TryParseConnect(head, out host, out var port, out var failure))
                {
                    await RespondAsync(stream, failure.status, failure.reason, ct);
                    DohLog.Write($"tolak {host}: {failure.reason}");
                    return;
                }
                if (!_allowedPorts.Contains(port))
                {
                    await RespondAsync(stream, 403, "Port tidak diizinkan", ct);
                    DohLog.Write($"tolak {host}:{port}: port tidak diizinkan");
                    return;
                }

                IReadOnlyList<IPAddress> addresses;
                string resolver;
                bool cached;
                if (IPAddress.TryParse(host, out var literal))
                {
                    addresses = new[] { literal };
                    resolver = "literal";
                    cached = false;
                }
                else
                {
                    DnsLookupResult lookup;
                    try { lookup = await _resolver.ResolveAsync(host, ct); }
                    catch (DohException ex)
                    {
                        await RespondAsync(stream, 502, "Gagal resolve DNS", ct);
                        DohLog.Write($"gagal {host}: {ex.Message}");
                        return;
                    }
                    addresses = lookup.Addresses;
                    resolver = lookup.Resolver;
                    cached = lookup.FromCache;
                }

                var upstream = await ConnectAsync(addresses, port, ct);
                if (upstream == null)
                {
                    await RespondAsync(stream, 502, "Tidak bisa terhubung ke server", ct);
                    DohLog.Write($"gagal {host}:{port} resolver={resolver} ip={string.Join(',', addresses)}: koneksi ke server gagal atau diblokir jaringan");
                    return;
                }

                using (upstream)
                {
                    DohLog.Write($"ok {host}:{port} resolver={resolver} cache={(cached ? "hit" : "miss")} ip={((IPEndPoint)upstream.Client.RemoteEndPoint!).Address}");
                    var established = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                    await stream.WriteAsync(established, ct);

                    var upstreamStream = upstream.GetStream();
                    if (leftover.Length > 0) await upstreamStream.WriteAsync(leftover, ct);
                    await RelayAsync(stream, upstreamStream, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                DohLog.Write($"kesalahan {host}: {ex.Message}");
            }
        }
    }

    private static async Task<(string Head, byte[] Leftover)> ReadHeadAsync(NetworkStream stream, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(HeaderTimeout);

        var buffer = new byte[MaxHeaderBytes];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total), timeout.Token);
            if (read == 0) break;
            total += read;

            var end = IndexOfHeaderEnd(buffer, total);
            if (end >= 0)
            {
                var head = Encoding.ASCII.GetString(buffer, 0, end);
                return (head, buffer.AsSpan(end + 4, total - end - 4).ToArray());
            }
        }
        throw new InvalidDataException("header permintaan tidak lengkap");
    }

    private static int IndexOfHeaderEnd(byte[] buffer, int length)
    {
        for (var i = 0; i + 3 < length; i++)
        {
            if (buffer[i] == '\r' && buffer[i + 1] == '\n' && buffer[i + 2] == '\r' && buffer[i + 3] == '\n') return i;
        }
        return -1;
    }

    public static bool TryParseConnect(string head, out string host, out int port, out (int status, string reason) failure)
    {
        host = "?";
        port = 0;
        failure = (400, "Permintaan tidak valid");

        var firstLine = head.Split("\r\n", 2)[0];
        var parts = firstLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) return false;

        if (!string.Equals(parts[0], "CONNECT", StringComparison.OrdinalIgnoreCase))
        {
            failure = (405, "Hanya CONNECT yang didukung");
            host = parts[0];
            return false;
        }

        var target = parts[1];
        string hostPart;
        string portPart;
        if (target.StartsWith('['))
        {
            var close = target.IndexOf(']');
            if (close < 0 || close + 2 > target.Length || target[close + 1] != ':') return false;
            hostPart = target[1..close];
            portPart = target[(close + 2)..];
        }
        else
        {
            var colon = target.LastIndexOf(':');
            if (colon <= 0) return false;
            hostPart = target[..colon];
            portPart = target[(colon + 1)..];
        }

        if (hostPart.Length == 0 || hostPart.Length > 253 || !int.TryParse(portPart, out port) || port is < 1 or > 65535) return false;
        host = hostPart;
        return true;
    }

    private static async Task RespondAsync(NetworkStream stream, int status, string reason, CancellationToken ct)
    {
        try
        {
            var body = $"HTTP/1.1 {status} {reason}\r\nConnection: close\r\nContent-Length: 0\r\n\r\n";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(body), ct);
        }
        catch { }
    }

    private static async Task<TcpClient?> ConnectAsync(IReadOnlyList<IPAddress> addresses, int port, CancellationToken ct)
    {
        foreach (var ip in addresses)
        {
            var client = new TcpClient(ip.AddressFamily) { NoDelay = true };
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(ConnectTimeout);
                await client.ConnectAsync(ip, port, timeout.Token);
                return client;
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                client.Dispose();
            }
        }
        return null;
    }

    private static async Task RelayAsync(NetworkStream a, NetworkStream b, CancellationToken ct)
    {
        var up = a.CopyToAsync(b, ct);
        var down = b.CopyToAsync(a, ct);
        // Selesai bila salah satu arah ditutup; dispose pemanggil menutup sisanya.
        await Task.WhenAny(up, down);
    }

    public void Dispose()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _cts.Cancel();
        try { _listener?.Stop(); } catch { }
        DohLog.Write("proxy berhenti");
    }
}
