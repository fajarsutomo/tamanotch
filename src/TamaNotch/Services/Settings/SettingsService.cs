using System.IO;
using System.Text.Json;
using TamaNotch.Models;

namespace TamaNotch.Services.Settings;

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    void Save();
    void SetStartWithWindows(bool enable);
    string GetAppDataPath();
    string GetLyricsCachePath();
    void ClearLyricsCache();
}

public class SettingsService : ISettingsService
{
    private readonly string _settingsFilePath;
    private readonly string _appDataDir;
    private readonly string _lyricsCacheDir;
    private AppSettings _settings;

    public AppSettings CurrentSettings => _settings;

    public SettingsService()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _appDataDir = Path.Combine(localAppData, "TAMANOTCH");
        var legacyDir = Path.Combine(localAppData, "NOTCHMusic");
        if (!Directory.Exists(_appDataDir) && Directory.Exists(legacyDir))
        {
            try { Directory.Move(legacyDir, _appDataDir); } catch { }
        }
        Directory.CreateDirectory(_appDataDir);

        _lyricsCacheDir = Path.Combine(_appDataDir, "LyricsCache");
        Directory.CreateDirectory(_lyricsCacheDir);

        _settingsFilePath = Path.Combine(_appDataDir, "settings.json");
        _settings = LoadSettings();
    }

    private AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(_settingsFilePath))
            {
                var json = File.ReadAllText(_settingsFilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json);
                if (loaded != null)
                {
                    return loaded;
                }
            }
        }
        catch
        {
            // Fallback to default
        }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsFilePath, json);
        }
        catch
        {
            // Handle or log if needed
        }
    }

    public void SetStartWithWindows(bool enable)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (key != null)
            {
                var exePath = Environment.ProcessPath;
                if (enable && !string.IsNullOrEmpty(exePath))
                {
                    key.SetValue("TAMANOTCH", $"\"{exePath}\"");
                }
                else
                {
                    key.DeleteValue("TAMANOTCH", false);
                }
            }
            _settings.StartWithWindows = enable;
            Save();
        }
        catch
        {
            // Silently fail if registry is blocked
        }
    }

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AutostartValueName = "TAMANOTCH";

    /// <summary>
    /// Autostart perlu ditulis ulang bila nilainya menunjuk exe yang sudah tidak ada atau exe lama (NotchMusic.exe,
    /// sebelum diganti nama menjadi TamaNotch.exe). Nilai yang menunjuk exe lain yang masih ada dibiarkan, supaya
    /// menjalankan versi dev tidak menimpa autostart versi terpasang.
    /// </summary>
    public static bool ShouldRefreshAutostart(string? currentValue, Func<string, bool> fileExists)
    {
        if (string.IsNullOrWhiteSpace(currentValue)) return true;
        var path = currentValue.Trim().Trim('"');
        if (string.Equals(Path.GetFileName(path), "NotchMusic.exe", StringComparison.OrdinalIgnoreCase)) return true;
        return !fileExists(path);
    }

    /// <summary>Perbaiki nilai Run bila autostart aktif tetapi menunjuk exe lama/hilang. Dipanggil sekali saat aplikasi mulai.</summary>
    public static void RefreshAutostartRegistration(bool enabled)
    {
        var exePath = Environment.ProcessPath;
        if (!enabled || string.IsNullOrEmpty(exePath)) return;
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            if (key == null) return;
            if (ShouldRefreshAutostart(key.GetValue(AutostartValueName) as string, File.Exists))
            {
                key.SetValue(AutostartValueName, $"\"{exePath}\"");
            }
        }
        catch
        {
            // Registri diblokir: autostart tetap seperti sebelumnya
        }
    }

    public string GetAppDataPath() => _appDataDir;
    public string GetLyricsCachePath() => _lyricsCacheDir;

    public void ClearLyricsCache()
    {
        try
        {
            if (Directory.Exists(_lyricsCacheDir))
            {
                var files = Directory.GetFiles(_lyricsCacheDir);
                foreach (var file in files)
                {
                    File.Delete(file);
                }
            }
        }
        catch
        {
        }
    }
}
