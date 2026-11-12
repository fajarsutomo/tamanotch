using System.Windows;

namespace TamaNotch.Services.Theme;

public sealed record ThemeChoice(string Id, string Label);

/// <summary>
/// Tema tampilan: kamus warna semantik (Themes/Colors.*.xaml) yang sama kuncinya di tiap tema. Jendela memakai
/// <c>DynamicResource</c>, sehingga mengganti kamus di <c>Application.Resources</c> langsung mengubah semua
/// jendela yang terbuka.
/// </summary>
public static class ThemeService
{
    public const string Dark = "Dark";
    public const string Light = "Light";
    public const string Glass = "Glass";

    public static IReadOnlyList<ThemeChoice> Choices { get; } = new[]
    {
        new ThemeChoice(Dark, "Gelap"),
        new ThemeChoice(Light, "Putih"),
        new ThemeChoice(Glass, "Liquid Glass")
    };

    public static string Normalize(string? theme) =>
        Choices.FirstOrDefault(c => string.Equals(c.Id, theme, StringComparison.OrdinalIgnoreCase))?.Id ?? Dark;

    public static Uri GetUri(string theme) =>
        new($"pack://application:,,,/TamaNotch;component/Themes/Colors.{Normalize(theme)}.xaml", UriKind.Absolute);

    public static string Current { get; private set; } = Dark;

    /// <summary>Dipicu setelah tema diterapkan (mis. untuk menyalakan/mematikan blur Glass).</summary>
    public static event Action<string>? Changed;

    /// <summary>Terapkan tema ke aplikasi. Aman dipanggil tanpa Application (mis. di tes).</summary>
    public static void Apply(string? theme, ResourceDictionary? appResources = null)
    {
        var id = Normalize(theme);
        var resources = appResources ?? System.Windows.Application.Current?.Resources;
        if (resources == null)
        {
            Current = id;
            return;
        }

        var dictionary = new ResourceDictionary { Source = GetUri(id) };
        var merged = resources.MergedDictionaries;
        var index = -1;
        for (var i = 0; i < merged.Count; i++)
        {
            if (merged[i].Source?.OriginalString.Contains("Themes/Colors.", StringComparison.OrdinalIgnoreCase) == true)
            {
                index = i;
                break;
            }
        }

        if (index >= 0) merged[index] = dictionary;
        else merged.Insert(0, dictionary);
        Current = id;
        Changed?.Invoke(id);
    }
}
