using System.Diagnostics;
using System.IO;

namespace TamaNotch.Services.Taskbar;

/// <summary>
/// Menjalankan TranslucentTB (aplikasi pihak ketiga) bersamaan dengan TAMANOTCH agar taskbar bisa transparan.
/// Di Windows 11 versi baru taskbar digambar XAML sehingga tidak bisa diubah lewat API biasa; TranslucentTB
/// memakai injeksi ke explorer.exe untuk itu. TAMANOTCH hanya meluncurkannya, tidak menyalin cara kerjanya.
/// </summary>
public static class TranslucentTbLauncher
{
    public const string ProcessName = "TranslucentTB";

    /// <summary>Lokasi umum TranslucentTB, urut dari yang paling mungkin (diteruskan agar mudah diuji).</summary>
    public static IEnumerable<string> CandidatePaths(string localAppData, string programFiles, string programFilesX86)
    {
        yield return Path.Combine(localAppData, "TranslucentTB", "TranslucentTB.exe");
        yield return Path.Combine(localAppData, "Microsoft", "WindowsApps", "TranslucentTB.exe"); // versi Microsoft Store
        yield return Path.Combine(programFiles, "TranslucentTB", "TranslucentTB.exe");
        yield return Path.Combine(programFilesX86, "TranslucentTB", "TranslucentTB.exe");
    }

    public static string? FindExecutable()
    {
        var candidates = CandidatePaths(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
        return candidates.FirstOrDefault(File.Exists);
    }

    public static bool IsInstalled => FindExecutable() != null;

    public static bool IsRunning()
    {
        var procs = Process.GetProcessesByName(ProcessName);
        try { return procs.Length > 0; }
        finally { foreach (var p in procs) p.Dispose(); }
    }

    /// <summary>Menjalankan TranslucentTB bila terpasang dan belum berjalan. True bila sudah/sedang berjalan.</summary>
    public static bool EnsureRunning()
    {
        try
        {
            if (IsRunning()) return true;
            var exe = FindExecutable();
            if (exe == null) return false;
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(exe)! })?.Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }
}
