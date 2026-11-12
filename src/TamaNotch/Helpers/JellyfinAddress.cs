using System.Net.Http;
namespace TamaNotch.Helpers;

/// <summary>Merapikan alamat server Jellyfin yang diketik pengguna.</summary>
public static class JellyfinAddress
{
    public const string Default = "https://myflix.tamahome.id/";

    /// <summary>
    /// Kosong -> <see cref="Default"/>; tanpa skema ditambah "https://"; hanya http/https yang diterima
    /// (selain itu kembali ke <see cref="Default"/>). Jalur seperti "/web/" dipertahankan.
    /// </summary>
    public static Uri Normalize(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text)) return new Uri(Default);

        if (!text.Contains("://", StringComparison.Ordinal))
        {
            text = "https://" + text;
        }

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && !string.IsNullOrEmpty(uri.Host))
        {
            return uri;
        }

        return new Uri(Default);
    }
}

/// <summary>Pemeriksaan server Jellyfin lewat endpoint publik /System/Info/Public (tanpa login).</summary>
public static class JellyfinServer
{
    public sealed record CheckResult(bool IsJellyfin, string? ServerName, string? Version, string? Error);

    /// <summary>Alamat dasar API: bagian sebelum "/web" bila ada (mendukung server di sub-jalur), tanpa garis miring akhir.</summary>
    public static Uri ApiBase(Uri address)
    {
        var path = address.AbsolutePath;
        var webIndex = path.IndexOf("/web", StringComparison.OrdinalIgnoreCase);
        if (webIndex >= 0) path = path[..webIndex];
        path = path.TrimEnd('/');
        return new Uri(address.GetLeftPart(UriPartial.Authority) + path);
    }

    public static async Task<CheckResult> CheckAsync(Uri address, HttpClient http, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = ApiBase(address).ToString().TrimEnd('/') + "/System/Info/Public";
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new CheckResult(false, null, null, $"Server menjawab {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            var product = root.TryGetProperty("ProductName", out var p) ? p.GetString() : null;
            var name = root.TryGetProperty("ServerName", out var n) ? n.GetString() : null;
            var version = root.TryGetProperty("Version", out var v) ? v.GetString() : null;

            return string.Equals(product, "Jellyfin Server", StringComparison.OrdinalIgnoreCase)
                ? new CheckResult(true, name, version, null)
                : new CheckResult(false, name, version, "Alamat ini bukan server Jellyfin.");
        }
        catch (OperationCanceledException)
        {
            return new CheckResult(false, null, null, "Waktu habis saat menghubungi server.");
        }
        catch (Exception ex)
        {
            return new CheckResult(false, null, null, ex.Message);
        }
    }
}
