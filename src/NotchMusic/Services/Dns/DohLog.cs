using System.IO;

namespace NotchMusic.Services.Dns;

/// <summary>Log diagnostik DNS/proxy: hanya nama host, resolver, IP, status cache, dan hasil koneksi (tanpa data aplikasi).</summary>
public static class DohLog
{
    private const long MaxFileBytes = 512 * 1024;
    private static readonly object Gate = new();

    /// <summary>Tujuan log tambahan (tes memasangnya); null = hanya debug output dan berkas.</summary>
    public static Action<string>? Sink { get; set; }

    /// <summary>Berkas log; null mematikan penulisan berkas (tes).</summary>
    public static string? FilePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TAMANOTCH", "doh.log");

    public static void Write(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}";
        System.Diagnostics.Debug.WriteLine("[DoH] " + line);
        try { Sink?.Invoke(line); } catch { }

        var path = FilePath;
        if (path == null) return;
        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length > MaxFileBytes) File.Delete(path);
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch { }
    }
}
