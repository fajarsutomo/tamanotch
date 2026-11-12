namespace TamaNotch.ViewModels;

/// <summary>Sidebar Pengaturan: daftar kategori dan kategori yang sedang dipilih.</summary>
public partial class SettingsViewModel
{
    public const string CategoryGeneral = "Umum";
    public const string CategoryTheme = "Tema";
    public const string CategoryNotch = "Tampilan Notch";
    public const string CategoryMedia = "Musik & Media";
    public const string CategoryPrayer = "Waktu Sholat";
    public const string CategoryInfo = "Cuaca & Info";
    public const string CategoryHealth = "Kesehatan";
    public const string CategoryNetwork = "Sinkronisasi & Jaringan";
    public const string CategoryTools = "Sticky Note & Monitor";
    public const string CategoryUpdate = "Pembaruan";

    /// <summary>Urutan kategori di sidebar. Tag setiap kartu di SettingsWindow.xaml harus salah satu dari nilai ini.</summary>
    public IReadOnlyList<string> Categories { get; } = new[]
    {
        CategoryGeneral,
        CategoryTheme,
        CategoryNotch,
        CategoryMedia,
        CategoryPrayer,
        CategoryInfo,
        CategoryHealth,
        CategoryNetwork,
        CategoryTools,
        CategoryUpdate
    };

    private string _selectedCategory = CategoryGeneral;

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (string.IsNullOrEmpty(value)) return; // ListBox mengirim null saat pilihan hilang
            SetProperty(ref _selectedCategory, value);
        }
    }

    /// <summary>Ikon Material (nama di <see cref="Helpers.MaterialIcons"/>) untuk tiap kategori sidebar.</summary>
    public static string CategoryIcon(string? category) => category switch
    {
        CategoryGeneral => "Tune",
        CategoryTheme => "Palette",
        CategoryNotch => "DockTop",
        CategoryMedia => "MusicNote",
        CategoryPrayer => "WeatherNight",
        CategoryInfo => "Cloud",
        CategoryHealth => "CupWater",
        CategoryNetwork => "Sync",
        CategoryTools => "StickyNote",
        CategoryUpdate => "Download",
        _ => "Settings"
    };
}
