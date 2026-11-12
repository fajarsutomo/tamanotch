namespace TamaNotch.ViewModels;

/// <summary>
/// Panel tertanam: Radio, Al-Qur'an, Waktu Sholat, Pemakaian Claude (dan fitur island lain) dapat tampil di antara baris
/// tombol dan baris info island alih-alih jendela sendiri. Hanya satu panel aktif; panel tampil selama island terbuka
/// dan ditutup saat island menciut kembali ke kapsul idle.
/// </summary>
public partial class MainViewModel
{
    public const string PanelRadio = "Radio";
    public const string PanelQuran = "Quran";
    public const string PanelPrayer = "Prayer";
    public const string PanelClaude = "Claude";

    public const string DisplayWindow = "Window";
    public const string DisplayIsland = "Island";

    private string? _embeddedPanel;

    /// <summary>Nama panel yang sedang ditanam di island, atau null.</summary>
    public string? EmbeddedPanel
    {
        get => _embeddedPanel;
        private set
        {
            if (SetProperty(ref _embeddedPanel, value))
            {
                OnPropertyChanged(nameof(ShowEmbeddedPanel));
                OnPropertyChanged(nameof(IsEmbeddedPanelFullWidth));
                OnPropertyChanged(nameof(IsNotesPanelOpen));
                OnPropertyChanged(nameof(IsMonitorPanelOpen));
                if (_systemMonitor is not null) UpdateMonitorRunning();
                OnPropertyChanged(nameof(IsNotchActive));
            }
        }
    }

    /// <summary>Panel hanya terlihat saat island terbuka (hover atau pin) di mode compact.</summary>
    public bool ShowEmbeddedPanel => _embeddedPanel != null && ShowComponentIcons;

    /// <summary>Lebar tetap panel tertanam (sama dengan jendela Radio/Al-Qur'an).</summary>
    public double EmbeddedPanelWidth => 410;

    /// <summary>Sticky note dan monitor sistem memakai seluruh lebar island; panel lain lebar tetap.</summary>
    public bool IsEmbeddedPanelFullWidth => _embeddedPanel is PanelNotes or PanelMonitor;

    /// <summary>Penanda aktif (bingkai hijau) ikon sticky note dan monitor saat panelnya terbuka.</summary>
    public bool IsNotesPanelOpen => _embeddedPanel == PanelNotes;

    public bool IsMonitorPanelOpen => _embeddedPanel == PanelMonitor;

    public static bool IsIslandMode(string? displayMode)
        => string.Equals(displayMode, DisplayIsland, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Tanam panel di island. Hanya berhasil saat island sedang terbuka (pengguna menekan ikon di island);
    /// dari tray atau saat idle, pemanggil memakai jendela sendiri.
    /// </summary>
    public bool OpenEmbeddedPanel(string panel)
    {
        if (!ShowComponentIcons) return false;
        EmbeddedPanel = panel;
        return true;
    }

    public void CloseEmbeddedPanel() => EmbeddedPanel = null;
}
