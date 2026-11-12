using Color = System.Windows.Media.Color;
using System.Globalization;
using System.Windows.Media;

namespace TamaNotch.Helpers;

/// <summary>Satu pilihan warna (nama tampilan + kode hex; "default" berarti tampilan bawaan aplikasi).</summary>
public sealed class ColorChoice
{
    public const string DefaultKey = "default";

    public string Name { get; }
    public string Hex { get; }

    public ColorChoice(string name, string hex)
    {
        Name = name;
        Hex = hex;
    }

    public bool IsDefault => string.Equals(Hex, DefaultKey, StringComparison.OrdinalIgnoreCase);

    // Dipakai di tampilan pemilih warna; default tidak punya warna sendiri.
    public string SwatchHex => IsDefault ? "#3C3C52" : Hex;
}

/// <summary>Preset warna neon dan utilitas warna untuk kustomisasi notch.</summary>
public static class NeonPalette
{
    public const string DefaultEyeHex = "#4FC3FF";
    public const string DefaultIdleFrameHex = "#35D6FF";

    public static readonly IReadOnlyList<ColorChoice> Presets = new[]
    {
        new ColorChoice("Biru muda", "#35D6FF"),
        new ColorChoice("Biru", "#3D7BFF"),
        new ColorChoice("Ungu", "#B44CFF"),
        new ColorChoice("Magenta", "#FF3DD0"),
        new ColorChoice("Merah muda", "#FF4D8D"),
        new ColorChoice("Merah", "#FF3B4F"),
        new ColorChoice("Oranye", "#FF8A1F"),
        new ColorChoice("Kuning", "#FFE600"),
        new ColorChoice("Hijau neon", "#39FF14"),
        new ColorChoice("Mint", "#2BFFB5"),
        new ColorChoice("Putih", "#F2F6FF"),
    };

    /// <summary>Pilihan untuk mode hover: ditambah "Bawaan" (tampilan asli island).</summary>
    public static readonly IReadOnlyList<ColorChoice> HoverChoices =
        new[] { new ColorChoice("Bawaan", ColorChoice.DefaultKey) }.Concat(Presets).ToArray();

    public static bool IsDefault(string? hex) =>
        string.IsNullOrWhiteSpace(hex) || string.Equals(hex.Trim(), ColorChoice.DefaultKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>Parse "#RRGGBB"; bila tidak valid, kembalikan <paramref name="fallback"/>.</summary>
    public static Color Parse(string? hex, string fallback)
    {
        if (TryParse(hex, out var color)) return color;
        TryParse(fallback, out color);
        return color;
    }

    public static bool TryParse(string? hex, out Color color)
    {
        color = Colors.White;
        if (string.IsNullOrWhiteSpace(hex)) return false;

        var s = hex.Trim().TrimStart('#');
        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb)) return false;

        color = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
        return true;
    }

    /// <summary>Campur warna dengan putih; amount 0 = warna asli, 1 = putih.</summary>
    public static Color Lighten(Color color, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        byte Mix(byte c) => (byte)Math.Round(c + (255 - c) * amount);
        return Color.FromRgb(Mix(color.R), Mix(color.G), Mix(color.B));
    }

    public static SolidColorBrush Freeze(SolidColorBrush brush)
    {
        brush.Freeze();
        return brush;
    }
}
