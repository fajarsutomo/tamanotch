using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace NotchMusic.Helpers;

/// <summary>
/// Memuat katalog bawaan (stasiun radio, saluran TV, daftar negara, kota) dari berkas JSON di
/// <c>Assets/Data</c>, sehingga daftar dapat diubah tanpa kompilasi ulang. Berkas yang hilang atau rusak
/// menghasilkan daftar kosong; aplikasi tetap berjalan dengan pencarian online dan item kustom.
/// </summary>
public static class CatalogLoader
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "Assets", "Data");

    public static List<T> Load<T>(string fileName) => LoadFile<T>(Path.Combine(DataDirectory, fileName));

    public static List<T> LoadFile<T>(string path)
    {
        try
        {
            if (!File.Exists(path)) return new List<T>();

            using var stream = File.OpenRead(path);
            return JsonSerializer.Deserialize<List<T>>(stream, Options) ?? new List<T>();
        }
        catch
        {
            return new List<T>();
        }
    }
}
