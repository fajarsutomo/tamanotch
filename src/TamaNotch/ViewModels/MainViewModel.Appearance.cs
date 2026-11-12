using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using TamaNotch.Helpers;

namespace TamaNotch.ViewModels;

public partial class MainViewModel
{
    // --- Kustomisasi notch: idle ---
    public Color IdleEyeColor => NeonPalette.Parse(Settings.IdleEyeColor, NeonPalette.DefaultEyeHex);

    private Color IdleFrameBaseColor => NeonPalette.Parse(Settings.IdleFrameColor, NeonPalette.DefaultIdleFrameHex);

    public Brush IdleFrameBrush => FrozenBrush(NeonPalette.Lighten(IdleFrameBaseColor, 0.45));

    public Effect IdleGlowEffect => Glow(IdleFrameBaseColor, 14, 0.9);

    // NaN = otomatis (mengikuti isi/ukuran mata).
    public double IdleFrameWidth => Settings.IdleFrameAutoSize ? double.NaN : Math.Clamp(Settings.IdleFrameWidth, 40, 240);

    public double IdleFrameHeight => Settings.IdleFrameAutoSize ? double.NaN : Math.Clamp(Settings.IdleFrameHeight, 20, 90);

    /// <summary>
    /// Bentuk "air menggantung" (sudut atas melengkung terbalik) untuk mode compact: idle, notch notifikasi, dan island hover.
    /// Mode expanded tetap persegi panjang membulat.
    /// </summary>
    public bool ShowDropShape => IsCompactMode && string.Equals(Settings.IdleShape, "Drop", StringComparison.OrdinalIgnoreCase);

    /// <summary>Dipanggil setelah tema berganti agar bentuk island dievaluasi ulang.</summary>
    public void NotifyThemeChanged()
    {
        OnPropertyChanged(nameof(ShowDropShape));
        OnPropertyChanged(nameof(DropFrameBrush));
        OnPropertyChanged(nameof(DropGlowEffect));
        OnPropertyChanged(nameof(UptimeBrush));
    }

    public const double MaxDropTopRadius = 16;

    public double DropTopRadiusLeft => Math.Clamp(Settings.IdleTopRadiusLeft, 0, MaxDropTopRadius);

    public double DropTopRadiusRight => Math.Clamp(Settings.IdleTopRadiusRight, 0, MaxDropTopRadius);

    /// <summary>Warna bingkai bentuk tetes mengikuti keadaan yang sedang tampil (null = bingkai bawaan island hover).</summary>
    public Brush? DropFrameBrush =>
        ShowAlertNotch ? AlertFrameBrush
        : ShowPrayerNotch ? PrayerFrameBrush
        : ShowWellnessNotch ? WellnessFrameBrush
        : ShowIdleEyes ? IdleFrameBrush
        : HoverFrameBrush;

    public Effect? DropGlowEffect =>
        ShowAlertNotch ? AlertGlowEffect
        : ShowPrayerNotch ? PrayerGlowEffect
        : ShowWellnessNotch ? WellnessGlowEffect
        : ShowIdleEyes ? IdleGlowEffect
        : HoverGlowEffect;

    // Urutan CornerRadius: kiri-atas, kanan-atas, kanan-bawah, kiri-bawah.
    /// <summary>Perkiraan ukuran kapsul idle (DIP) untuk animasi menciut; bila ukuran otomatis, mengikuti mata beserta ruang di sekitarnya.</summary>
    public (double Width, double Height) EstimateIdleCapsuleSize()
    {
        double width = Settings.IdleFrameAutoSize ? CenterIconWidth + 4 + 24 + 2.4 : Math.Clamp(Settings.IdleFrameWidth, 40, 240);
        double height = Settings.IdleFrameAutoSize ? CenterIconHeight + 8 + 1.2 : Math.Clamp(Settings.IdleFrameHeight, 20, 90);
        return (width, height);
    }

    public CornerRadius IdleFrameCornerRadius => new(0, 0, Math.Clamp(Settings.IdleFrameRadiusRight, 0, 60), Math.Clamp(Settings.IdleFrameRadiusLeft, 0, 60));

    // --- Kustomisasi notch: hover (null = tampilan bawaan) ---
    private string EffectiveHoverFrameColor => Settings.HoverSameAsIdle ? Settings.IdleFrameColor : Settings.HoverFrameColor;

    private string EffectiveHoverRingColor => Settings.HoverSameAsIdle ? Settings.IdleFrameColor : Settings.HoverRingColor;

    public Brush? HoverFrameBrush => NeonPalette.IsDefault(EffectiveHoverFrameColor)
        ? null
        : FrozenBrush(NeonPalette.Lighten(NeonPalette.Parse(EffectiveHoverFrameColor, NeonPalette.DefaultIdleFrameHex), 0.45));

    public Effect? HoverGlowEffect => NeonPalette.IsDefault(EffectiveHoverFrameColor)
        ? null
        : Glow(NeonPalette.Parse(EffectiveHoverFrameColor, NeonPalette.DefaultIdleFrameHex), 10, 0.65);

    public Brush? HoverRingBrush => NeonPalette.IsDefault(EffectiveHoverRingColor)
        ? null
        : FrozenBrush(NeonPalette.Parse(EffectiveHoverRingColor, NeonPalette.DefaultIdleFrameHex));

    private static Brush FrozenBrush(Color color) => NeonPalette.Freeze(new SolidColorBrush(color));

    private static Effect Glow(Color color, double blur, double opacity)
    {
        var effect = new DropShadowEffect { Color = color, BlurRadius = blur, ShadowDepth = 0, Opacity = opacity };
        effect.Freeze();
        return effect;
    }

    private void RaiseAppearanceChanged()
    {
        OnPropertyChanged(nameof(IdleEyeColor));
        OnPropertyChanged(nameof(IdleFrameBrush));
        OnPropertyChanged(nameof(IdleGlowEffect));
        OnPropertyChanged(nameof(IdleFrameWidth));
        OnPropertyChanged(nameof(IdleFrameHeight));
        OnPropertyChanged(nameof(IdleFrameCornerRadius));
        OnPropertyChanged(nameof(ShowDropShape));
        OnPropertyChanged(nameof(DropTopRadiusLeft));
        OnPropertyChanged(nameof(DropTopRadiusRight));
        OnPropertyChanged(nameof(DropFrameBrush));
        OnPropertyChanged(nameof(DropGlowEffect));
        OnPropertyChanged(nameof(HoverFrameBrush));
        OnPropertyChanged(nameof(HoverGlowEffect));
        OnPropertyChanged(nameof(HoverRingBrush));
    }
}
