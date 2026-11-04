using NotchMusic.Models;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Dns;

/// <summary>
/// Titik tunggal yang memasang proxy DoH lokal ke environment WebView2 (bila diaktifkan di Pengaturan).
/// Hanya lalu lintas https yang dialihkan; setelah aktif tidak ada kembali diam-diam ke DNS Windows:
/// bila proxy gagal dimulai, pembuatan WebView2 melempar kesalahan yang tampil di jendelanya.
/// </summary>
public static class WebViewDns
{
    private static readonly object Gate = new();
    private static DohProxyService? _proxy;
    private static DohResolver? _resolver;

    /// <summary>Diisi sekali saat aplikasi mulai agar jendela WebView2 bisa membaca pengaturan.</summary>
    public static ISettingsService? Settings { get; set; }

    public static bool IsEnabled(AppSettings? settings) => settings?.DohEnabled == true;

    /// <summary>Gabungkan argumen dasar dengan argumen proxy DoH bila fitur aktif; jika mati, kembalikan argumen dasar.</summary>
    public static string BuildArguments(string baseArguments)
    {
        var settings = Settings?.CurrentSettings;
        if (!IsEnabled(settings)) return baseArguments;

        var proxy = EnsureProxy(settings!.DohProvider);
        // "https=" hanya mengalihkan URL https/wss; http biasa tetap langsung dan loopback dikecualikan oleh Chromium.
        return $"{baseArguments} --proxy-server=\"https=127.0.0.1:{proxy.Port}\"";
    }

    private static DohProxyService EnsureProxy(string? provider)
    {
        lock (Gate)
        {
            if (_proxy is { IsRunning: true }) return _proxy;

            _resolver = new DohResolver(provider);
            var proxy = new DohProxyService(_resolver);
            proxy.Start(); // melempar InvalidOperationException bila gagal
            _proxy = proxy;
            return proxy;
        }
    }

    public static void Shutdown()
    {
        lock (Gate)
        {
            _proxy?.Dispose();
            _resolver?.Dispose();
            _proxy = null;
            _resolver = null;
        }
    }
}
