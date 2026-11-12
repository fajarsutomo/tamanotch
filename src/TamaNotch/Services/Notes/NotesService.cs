using System.IO;
using System.Text.Json;
using TamaNotch.Models;
using TamaNotch.Services.Settings;

namespace TamaNotch.Services.Notes;

public interface INotesService
{
    /// <summary>Muat isi notes.json; berkas hilang atau rusak menghasilkan data kosong (berkas rusak dicadangkan).</summary>
    NotesData Load();

    /// <summary>Simpan seluruh isi secara atomik (tulis ke .tmp lalu ganti).</summary>
    void Save(NotesData data);
}

/// <summary>
/// Sticky note lokal di <c>%LOCALAPPDATA%\TAMANOTCH\notes.json</c>, tanpa enkripsi dan tanpa sinkronisasi Drive:
/// sengaja teks biasa (dipakai untuk akun intranet), jadi berkas ini tidak boleh dibagikan.
/// </summary>
public sealed class NotesService : INotesService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly object _lock = new();
    private readonly string _filePath;

    public NotesService(ISettingsService settingsService)
    {
        var dir = settingsService.GetAppDataPath();
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "notes.json");
    }

    public string FilePath => _filePath;

    public NotesData Load()
    {
        lock (_lock)
        {
            if (!File.Exists(_filePath)) return new NotesData();
            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<NotesData>(json) ?? new NotesData();
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                // Jangan menimpa berkas yang tidak terbaca: simpan salinannya agar isinya tidak hilang.
                try { File.Copy(_filePath, _filePath + ".bad", overwrite: true); } catch { }
                return new NotesData();
            }
        }
    }

    public void Save(NotesData data)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(data, JsonOptions);
            var temp = _filePath + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, _filePath, overwrite: true);
        }
    }
}
