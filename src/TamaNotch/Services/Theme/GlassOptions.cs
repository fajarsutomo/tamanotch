using TamaNotch.Models;

namespace TamaNotch.Services.Theme;

/// <summary>
/// Pengaturan lanjutan tema Liquid Glass (dari <see cref="AppSettings"/>), dibaca oleh <see cref="IslandGlass"/>
/// dan <see cref="GlassBackdrop"/>. <see cref="Changed"/> dipicu setiap kali nilainya dimuat ulang.
/// </summary>
public static class GlassOptions
{
    public const int MinBlur = 1, MaxBlur = 10, DefaultBlur = 4;
    public const int MinTint = 0, MaxTint = 80, DefaultTint = 15;
    public const int MinRefreshMs = 100, MaxRefreshMs = 1000, DefaultRefreshMs = 300;

    /// <summary>Island memakai blur latar (salinan layar). Mati: kaca tembus pandang tanpa blur dan island terlihat saat berbagi layar.</summary>
    public static bool IslandBlur { get; private set; } = true;

    /// <summary>Kekuatan blur island 1..10 (radius box blur pada salinan yang diperkecil).</summary>
    public static int BlurStrength { get; private set; } = DefaultBlur;

    /// <summary>Kepekatan warna kaca island dalam persen (0 = sebening mungkin).</summary>
    public static int TintPercent { get; private set; } = DefaultTint;

    /// <summary>Warna kaca menebal otomatis di atas latar terang agar teks putih terbaca.</summary>
    public static bool AdaptiveTint { get; private set; } = true;

    /// <summary>Selang pembaruan salinan layar di belakang island.</summary>
    public static int RefreshMs { get; private set; } = DefaultRefreshMs;

    /// <summary>Blur bawaan Windows untuk jendela lain (Radio, Al-Qur'an, BMKG, Pengaturan, ...).</summary>
    public static bool WindowBlur { get; private set; } = true;

    public static event Action? Changed;

    public static void Load(AppSettings settings)
    {
        IslandBlur = settings.GlassIslandBlur;
        BlurStrength = Math.Clamp(settings.GlassBlurStrength, MinBlur, MaxBlur);
        TintPercent = Math.Clamp(settings.GlassTintPercent, MinTint, MaxTint);
        AdaptiveTint = settings.GlassAdaptiveTint;
        RefreshMs = Math.Clamp(settings.GlassRefreshMs, MinRefreshMs, MaxRefreshMs);
        WindowBlur = settings.GlassWindowBlur;
        Changed?.Invoke();
    }

    /// <summary>
    /// Alpha warna kaca island: <paramref name="tintPercent"/> di atas latar gelap; bila adaptif, ditambah sampai 35 poin
    /// di atas latar putih (maksimal 90%).
    /// </summary>
    public static byte TintAlpha(double luminance, int tintPercent, bool adaptive)
    {
        var fraction = Math.Clamp(tintPercent, MinTint, MaxTint) / 100.0;
        if (adaptive) fraction += 0.35 * Math.Clamp(luminance, 0, 1);
        return (byte)Math.Round(255 * Math.Min(fraction, 0.9));
    }
}
