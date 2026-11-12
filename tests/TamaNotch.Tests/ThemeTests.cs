using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Markup;
using TamaNotch.Services.Theme;
using Xunit;

namespace TamaNotch.Tests;

public class ThemeTests
{
    private static string SrcDir([CallerFilePath] string path = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", "..", "src", "TamaNotch"));

    private static HashSet<string> Keys(string theme)
    {
        var file = Path.Combine(SrcDir(), "Themes", $"Colors.{theme}.xaml");
        var keys = new HashSet<string>();
        foreach (Match m in Regex.Matches(File.ReadAllText(file), "x:Key=\"([^\"]+)\"")) keys.Add(m.Groups[1].Value);
        return keys;
    }

    [Fact]
    public void AllThemes_DefineExactlyTheSameBrushKeys()
    {
        var dark = Keys("Dark");
        Assert.NotEmpty(dark);
        Assert.True(dark.SetEquals(Keys("Light")), "Light berbeda dari Dark");
        Assert.True(dark.SetEquals(Keys("Glass")), "Glass berbeda dari Dark");
        Assert.All(ThemeService.Choices, c => Assert.True(File.Exists(Path.Combine(SrcDir(), "Themes", $"Colors.{c.Id}.xaml"))));
    }

    [Fact]
    public void EveryThemeKeyReferencedInXaml_ExistsInTheThemeDictionaries()
    {
        var defined = Keys("Dark");
        var used = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(SrcDir(), "*.xaml", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}Themes{Path.DirectorySeparatorChar}")
                              && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                              && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"\{DynamicResource (Theme[A-Za-z]+)\}"))
                used.Add(m.Groups[1].Value);
        }

        Assert.NotEmpty(used);
        Assert.All(used, k => Assert.Contains(k, defined));
    }

    [Fact]
    public void ThemeDictionaries_ParseAsWpfResourceDictionaries()
    {
        foreach (var choice in ThemeService.Choices)
        {
            var file = Path.Combine(SrcDir(), "Themes", $"Colors.{choice.Id}.xaml");
            using var stream = File.OpenRead(file);
            var dictionary = Assert.IsType<ResourceDictionary>(XamlReader.Load(stream));
            Assert.True(dictionary.Contains("ThemeText"));
            Assert.True(dictionary.Contains("ThemeIslandBg"));
        }
    }

    [Theory]
    [InlineData(null, "Dark")]
    [InlineData("", "Dark")]
    [InlineData("nonsense", "Dark")]
    [InlineData("light", "Light")]
    [InlineData("GLASS", "Glass")]
    public void Normalize_FallsBackToDark(string? input, string expected)
        => Assert.Equal(expected, ThemeService.Normalize(input));

    [Fact]
    public void MaterialIcons_AllPathDataParse_AndEveryIconUsedInXamlExists()
    {
        Assert.All(TamaNotch.Helpers.MaterialIcons.Names, name =>
        {
            var geometry = TamaNotch.Helpers.MaterialIcons.Get(name);
            Assert.False(geometry.IsEmpty(), $"Ikon {name} kosong");
            var bounds = geometry.Bounds;
            Assert.True(bounds.Width > 1 && bounds.Height > 1 && bounds.Right <= 24.5 && bounds.Bottom <= 24.5, $"Ikon {name} di luar kanvas 24x24: {bounds}");
        });

        var used = new HashSet<string>();
        foreach (var file in Directory.EnumerateFiles(SrcDir(), "*.xaml", SearchOption.AllDirectories)
                     .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                              && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"\{icons:Icon (\w+)")) used.Add(m.Groups[1].Value);
            foreach (Match m in Regex.Matches(text, @"<icons:IconPath Kind=""(\w+)""")) used.Add(m.Groups[1].Value);
        }

        Assert.NotEmpty(used);
        Assert.All(used, k => Assert.True(TamaNotch.Helpers.MaterialIcons.Contains(k), $"Ikon {k} dipakai di XAML tetapi tidak ada"));
    }

    [Fact]
    public void SharedIconGeometries_ParseAndFitTheMaterialCanvas()
    {
        using var stream = File.OpenRead(Path.Combine(SrcDir(), "Themes", "Icons.xaml"));
        var dictionary = Assert.IsType<ResourceDictionary>(XamlReader.Load(stream));

        Assert.NotEmpty(dictionary.Keys);
        foreach (var key in dictionary.Keys)
        {
            var geometry = Assert.IsAssignableFrom<System.Windows.Media.Geometry>(dictionary[key]);
            var bounds = geometry.Bounds;
            Assert.True(bounds.Width > 1 && bounds.Height > 1 && bounds.Right <= 24.5 && bounds.Bottom <= 24.5, $"{key} di luar kanvas 24x24: {bounds}");
        }
    }

    [Fact]
    public void IconGeometries_AreDefinedOnlyInTheSharedDictionary()
    {
        foreach (var file in XamlFiles().Where(f => !f.EndsWith("Icons.xaml", StringComparison.OrdinalIgnoreCase)))
            Assert.DoesNotMatch(@"<Geometry x:Key=""(Luxury\w+Geometry|QuranIconGeometry|ClaudeUsageIconGeometry)""", File.ReadAllText(file));
    }

    [Fact]
    public void DynamicResource_IsNeverUsedInsideABinding()
    {
        // WPF melempar XamlParseException saat jendela dimuat (mis. TargetNullValue={DynamicResource ...})
        foreach (var file in XamlFiles())
            Assert.DoesNotMatch(@"\{(Multi)?Binding [^""]*\{DynamicResource", File.ReadAllText(file));
    }

    [Fact]
    public void IslandIcons_AreFlat_IconFillAndBadgeBackgroundsAreSolid()
    {
        // Ikon dan latar lencana polos (Material flat); hanya tepi kaca tema Glass yang boleh bergradasi
        foreach (var choice in ThemeService.Choices)
        {
            var text = File.ReadAllText(Path.Combine(SrcDir(), "Themes", $"Colors.{choice.Id}.xaml"));
            foreach (var key in new[] { "MetallicSilverBrush", "MetallicIconBadgeBgBrush", "LuxuryPillBackgroundBrush" })
                Assert.Matches($"<SolidColorBrush x:Key=\"{key}\"", text);
        }
    }

    [Fact]
    public void EverySettingsCategory_HasAMaterialIcon()
    {
        var vm = typeof(TamaNotch.ViewModels.SettingsViewModel);
        var categories = vm.GetFields().Where(f => f.IsLiteral && f.Name.StartsWith("Category")).Select(f => (string)f.GetRawConstantValue()!).ToList();

        Assert.NotEmpty(categories);
        Assert.All(categories, c =>
        {
            var icon = TamaNotch.ViewModels.SettingsViewModel.CategoryIcon(c);
            Assert.NotEqual("Settings", icon);
            Assert.True(TamaNotch.Helpers.MaterialIcons.Contains(icon), $"Ikon {icon} untuk {c} tidak ada");
        });
    }

    [Fact]
    public void SharedIconGeometries_AreReferencedDynamically_AndExist()
    {
        var defined = new HashSet<string>(Regex.Matches(File.ReadAllText(Path.Combine(SrcDir(), "Themes", "Icons.xaml")), "x:Key=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value));
        foreach (var file in XamlFiles())
        {
            var text = File.ReadAllText(file);
            // StaticResource ke kamus aplikasi gagal saat jendela dimuat tanpa App (tes); DynamicResource aman
            Assert.DoesNotMatch(@"\{StaticResource (Luxury\w+Geometry|QuranIconGeometry|ClaudeUsageIconGeometry)\}", text);
            foreach (Match m in Regex.Matches(text, @"\{DynamicResource (Luxury\w+Geometry|QuranIconGeometry|ClaudeUsageIconGeometry)\}"))
                Assert.Contains(m.Groups[1].Value, defined);
        }
    }

    [Fact]
    public void WindowControlButton_IsDefinedOnce_AndUsedByHeaderCloseButtons()
    {
        var controls = File.ReadAllText(Path.Combine(SrcDir(), "Themes", "Controls.xaml"));
        Assert.Contains("x:Key=\"WindowControlButton\"", controls);
        Assert.Contains("Value=\"28\"", controls);

        foreach (var name in new[] { "LiveTvWindow", "BmkgWindow", "RadioPanel", "QuranPanel", "PrayerPanel", "ClaudeUsagePanel" })
        {
            var text = File.ReadAllText(Path.Combine(SrcDir(), "Views", name + ".xaml"));
            Assert.Contains("WindowControlButton", text);
            Assert.DoesNotMatch(@"SmallPillButton\}""[^>]*Click=""CloseButton_Click""", text);
        }
    }

    private static IEnumerable<string> XamlFiles()

        => Directory.EnumerateFiles(SrcDir(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));
}
