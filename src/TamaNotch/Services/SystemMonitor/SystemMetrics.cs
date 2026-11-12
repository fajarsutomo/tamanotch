namespace TamaNotch.Services.SystemMonitor;

/// <summary>Cuplikan waktu CPU kumulatif (satuan 100 ns) dari GetSystemTimes. Kernel sudah termasuk idle.</summary>
public readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);

/// <summary>Satu pembacaan metrik sistem. Nilai null berarti tidak tersedia (mis. tanpa GPU atau counter gagal).</summary>
public sealed record SystemSnapshot(
    double CpuPercent,
    double MemoryPercent,
    ulong MemoryUsedBytes,
    ulong MemoryTotalBytes,
    double? DiskPercent,
    double NetDownBytesPerSecond,
    double NetUpBytesPerSecond,
    double? GpuPercent,
    TimeSpan Uptime);

/// <summary>Perhitungan murni (mudah diuji) di balik monitor sistem.</summary>
public static class SystemMetrics
{
    /// <summary>Persentase CPU terpakai antara dua cuplikan; 0 bila belum ada selisih.</summary>
    public static double CpuPercent(CpuTimes previous, CpuTimes current)
    {
        var idle = Delta(current.Idle, previous.Idle);
        var kernel = Delta(current.Kernel, previous.Kernel);
        var user = Delta(current.User, previous.User);
        var total = kernel + user; // kernel sudah mencakup idle
        if (total == 0) return 0;
        var busy = total > idle ? total - idle : 0;
        return Clamp(busy * 100.0 / total);
    }

    /// <summary>Byte per detik dari selisih penghitung; penghitung turun (adapter reset) dianggap 0.</summary>
    public static double BytesPerSecond(ulong previousBytes, ulong currentBytes, TimeSpan elapsed)
    {
        if (elapsed <= TimeSpan.Zero || currentBytes < previousBytes) return 0;
        return (currentBytes - previousBytes) / elapsed.TotalSeconds;
    }

    public static double MemoryPercent(ulong totalBytes, ulong availableBytes)
    {
        if (totalBytes == 0 || availableBytes > totalBytes) return 0;
        return Clamp((totalBytes - availableBytes) * 100.0 / totalBytes);
    }

    /// <summary>Aktivitas disk seperti Task Manager: 100 dikurangi persen waktu idle.</summary>
    public static double DiskActivityFromIdle(double idlePercent) => Clamp(100 - idlePercent);

    public static double Clamp(double percent) => double.IsNaN(percent) ? 0 : Math.Clamp(percent, 0, 100);

    /// <summary>"0:14:45:58" (hari:jam:menit:detik, sama dengan Task Manager) atau "0:14:45" tanpa detik.</summary>
    public static string FormatUptime(TimeSpan uptime, bool withSeconds)
    {
        if (uptime < TimeSpan.Zero) uptime = TimeSpan.Zero;
        var text = $"{(int)uptime.TotalDays}:{uptime.Hours:00}:{uptime.Minutes:00}";
        return withSeconds ? $"{text}:{uptime.Seconds:00}" : text;
    }

    /// <summary>"512 B/s", "3,4 KB/s", "5,1 MB/s" (satuan 1024).</summary>
    public static string FormatRate(double bytesPerSecond, System.Globalization.CultureInfo? culture = null)
    {
        culture ??= System.Globalization.CultureInfo.CurrentCulture;
        if (bytesPerSecond < 1024) return string.Format(culture, "{0:0} B/s", bytesPerSecond);
        if (bytesPerSecond < 1024 * 1024) return string.Format(culture, "{0:0.0} KB/s", bytesPerSecond / 1024);
        if (bytesPerSecond < 1024L * 1024 * 1024) return string.Format(culture, "{0:0.0} MB/s", bytesPerSecond / (1024 * 1024));
        return string.Format(culture, "{0:0.0} GB/s", bytesPerSecond / (1024.0 * 1024 * 1024));
    }

    /// <summary>"12,1 / 15,7 GB".</summary>
    public static string FormatMemory(ulong usedBytes, ulong totalBytes, System.Globalization.CultureInfo? culture = null)
    {
        culture ??= System.Globalization.CultureInfo.CurrentCulture;
        const double gb = 1024.0 * 1024 * 1024;
        return string.Format(culture, "{0:0.0} / {1:0.0} GB", usedBytes / gb, totalBytes / gb);
    }

    private static ulong Delta(ulong current, ulong previous) => current >= previous ? current - previous : 0;
}

/// <summary>Riwayat nilai berukuran tetap (terlama dibuang) untuk grafik.</summary>
public sealed class MetricHistory
{
    private readonly Queue<double> _values = new();
    private readonly int _capacity;

    public MetricHistory(int capacity = 60)
    {
        if (capacity < 2) throw new ArgumentOutOfRangeException(nameof(capacity));
        _capacity = capacity;
    }

    public int Capacity => _capacity;

    public int Count => _values.Count;

    public void Add(double value)
    {
        _values.Enqueue(value);
        while (_values.Count > _capacity) _values.Dequeue();
    }

    /// <summary>Salinan nilai terlama ke terbaru (aman untuk dipakai di thread lain).</summary>
    public double[] ToArray() => _values.ToArray();
}
