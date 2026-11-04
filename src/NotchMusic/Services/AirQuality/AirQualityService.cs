using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Threading;
using NotchMusic.Helpers;

namespace NotchMusic.Services.AirQuality;

/// <summary>Pembacaan kualitas udara saat ini (estimasi model Open-Meteo/CAMS, bukan alat ukur stasiun).</summary>
public sealed record AirQualityReading(int UsAqi, double? Pm25, double? Pm10, DateTime FetchedAt)
{
    /// <summary>Data yang lebih tua dari ini dianggap basi dan tidak ditampilkan.</summary>
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(2);

    public bool IsFresh(DateTime now) => now - FetchedAt <= MaxAge;
}

/// <summary>Kategori US AQI: nama, warna, dan batas atas.</summary>
public static class AirQualityScale
{
    public static (string Label, string Color) Describe(int aqi) => aqi switch
    {
        <= 50 => ("Baik", "#30D158"),
        <= 100 => ("Sedang", "#FFD43B"),
        <= 150 => ("Tidak sehat bagi kelompok sensitif", "#FF9F0A"),
        <= 200 => ("Tidak sehat", "#FF453A"),
        <= 300 => ("Sangat tidak sehat", "#BF5AF2"),
        _ => ("Berbahaya", "#C2334D")
    };
}

public interface IAirQualityService : IDisposable
{
    AirQualityReading? Current { get; }

    event EventHandler? Updated;

    void Start();

    void Stop();

    Task RefreshAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Kualitas udara dari Open-Meteo Air Quality API (gratis, tanpa kunci, non-komersial, lisensi data CC-BY 4.0):
/// US AQI, PM2.5, dan PM10 untuk koordinat lokasi aktif, disegarkan tiap 30 menit.
/// </summary>
public sealed class AirQualityService : IAirQualityService
{
    public const string Endpoint = "https://air-quality-api.open-meteo.com/v1/air-quality";

    private static readonly HttpClient SharedClient = CreateClient();

    private readonly Func<(double Latitude, double Longitude)> _location;
    private readonly Func<bool> _isEnabled;
    private readonly HttpClient _http;
    private readonly DispatcherTimer? _timer;
    private int _refreshing;

    public AirQualityService(Func<(double Latitude, double Longitude)> location, Func<bool> isEnabled, HttpClient? http = null, bool useTimer = true)
    {
        _location = location;
        _isEnabled = isEnabled;
        _http = http ?? SharedClient;

        if (useTimer)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
            _timer.Tick += async (s, e) => await RefreshAsync();
        }
    }

    public AirQualityReading? Current { get; private set; }

    public event EventHandler? Updated;

    public void Start()
    {
        _timer?.Start();
        _ = RefreshAsync();
    }

    public void Stop()
    {
        _timer?.Stop();
        if (Current != null)
        {
            Current = null;
            Updated?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!_isEnabled()) return;
        // Satu permintaan pada satu waktu; yang menyusul dibuang karena hasilnya sama.
        if (Interlocked.Exchange(ref _refreshing, 1) == 1) return;

        try
        {
            var (lat, lon) = _location();
            using var response = await _http.GetAsync(BuildUrl(lat, lon), cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) return;

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var reading = Parse(json, DateTime.Now);
            if (reading == null) return;

            Current = reading;
            Updated?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            // Jaringan mati atau layanan bermasalah: pembacaan lama dipertahankan sampai basi.
        }
        finally
        {
            Interlocked.Exchange(ref _refreshing, 0);
        }
    }

    public static string BuildUrl(double latitude, double longitude)
        => string.Create(CultureInfo.InvariantCulture,
            $"{Endpoint}?latitude={latitude:0.####}&longitude={longitude:0.####}&current=us_aqi,pm2_5,pm10&timezone=auto");

    /// <summary>Membaca blok "current" Open-Meteo; null bila US AQI tidak ada (mis. di luar cakupan model).</summary>
    public static AirQualityReading? Parse(string json, DateTime now)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("current", out var current) || current.ValueKind != JsonValueKind.Object)
            return null;

        if (!TryGetNumber(current, "us_aqi", out var aqi) || aqi < 0)
            return null;

        TryGetNumber(current, "pm2_5", out var pm25, nullable: true);
        TryGetNumber(current, "pm10", out var pm10, nullable: true);
        return new AirQualityReading(
            (int)Math.Round(aqi),
            double.IsNaN(pm25) ? null : pm25,
            double.IsNaN(pm10) ? null : pm10,
            now);
    }

    private static bool TryGetNumber(JsonElement obj, string name, out double value, bool nullable = false)
    {
        value = nullable ? double.NaN : 0;
        if (!obj.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Number) return false;
        value = el.GetDouble();
        return true;
    }

    public void Dispose() => _timer?.Stop();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(12) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppVersion.UserAgentToken} (Windows; Open-Meteo Air Quality)");
        return client;
    }
}
