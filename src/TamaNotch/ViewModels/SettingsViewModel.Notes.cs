namespace TamaNotch.ViewModels;

public partial class SettingsViewModel
{
    public bool EnableNotes
    {
        get => Settings.EnableNotes;
        set => SetSetting(Settings.EnableNotes, value, v => Settings.EnableNotes = v, nameof(EnableNotes));
    }

    /// <summary>Lokasi notes.json untuk ditampilkan di Pengaturan (teks biasa, tanpa enkripsi).</summary>
    public string NotesFilePath => System.IO.Path.Combine(_settingsService.GetAppDataPath(), "notes.json");
}
