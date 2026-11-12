using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace TamaNotch.Services.SystemMonitor;

public interface ISystemMonitorService : IDisposable
{
    /// <summary>Dipicu dari thread latar setiap kali ada cuplikan baru.</summary>
    event EventHandler? Updated;

    SystemSnapshot? Latest { get; }

    bool IsRunning { get; }

    /// <summary>Riwayat 60 titik terakhir per metrik (terlama ke terbaru).</summary>
    MonitorHistories GetHistories();

    void Start();

    void Stop();
}

public sealed record MonitorHistories(
    double[] Cpu, double[] Memory, double[] Disk, double[] NetDown, double[] NetUp, double[] Gpu);

/// <summary>
/// Sampler CPU, memori, disk, jaringan, dan GPU tanpa paket tambahan: P/Invoke <c>GetSystemTimes</c> dan
/// <c>GlobalMemoryStatusEx</c> (bebas lokalisasi), <see cref="NetworkInterface"/> untuk jaringan, serta PDH dengan nama
/// counter Inggris untuk disk dan GPU (gagal dengan sopan: metrik itu menjadi null dan kartunya disembunyikan).
/// </summary>
public sealed class SystemMonitorService : ISystemMonitorService
{
    private readonly object _lock = new();
    private readonly MetricHistory _cpu = new(), _memory = new(), _disk = new(), _netDown = new(), _netUp = new(), _gpu = new();
    private readonly Func<DateTime> _now;
    private System.Threading.Timer? _timer;
    private CpuTimes? _lastCpu;
    private ulong _lastNetRx, _lastNetTx;
    private DateTime _lastNetAt;
    private bool _hasLastNet;
    private PdhSampler? _pdh;
    private bool _disposed;

    public SystemMonitorService(Func<DateTime>? now = null) => _now = now ?? (() => DateTime.UtcNow);

    public event EventHandler? Updated;

    public SystemSnapshot? Latest { get; private set; }

    public bool IsRunning => _timer != null;

    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(1);

    public void Start()
    {
        lock (_lock)
        {
            if (_disposed || _timer != null) return;
            _timer = new System.Threading.Timer(_ => SafeSample(), null, TimeSpan.Zero, Interval);
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _timer?.Dispose();
            _timer = null;
            _lastCpu = null;     // jeda panjang membuat selisih tidak bermakna
            _hasLastNet = false;
            _pdh?.Dispose();
            _pdh = null;
        }
    }

    private void SafeSample()
    {
        lock (_lock)
        {
            if (_timer == null) return; // callback yang sudah antre saat Stop() dipanggil
        }

        try { Sample(); }
        catch (Exception) { /* satu cuplikan gagal tidak boleh menjatuhkan aplikasi */ }
    }

    public MonitorHistories GetHistories()
    {
        lock (_lock)
        {
            return new MonitorHistories(_cpu.ToArray(), _memory.ToArray(), _disk.ToArray(), _netDown.ToArray(), _netUp.ToArray(), _gpu.ToArray());
        }
    }

    /// <summary>Ambil satu cuplikan sekarang (dipakai timer; public agar bisa diuji).</summary>
    public SystemSnapshot Sample()
    {
        SystemSnapshot snapshot;
        lock (_lock)
        {
            // CPU
            var cpuNow = NativeMetrics.ReadCpuTimes();
            var cpuPercent = _lastCpu is { } last && cpuNow is { } n ? SystemMetrics.CpuPercent(last, n) : 0;
            _lastCpu = cpuNow;

            // Memori
            var (total, available) = NativeMetrics.ReadMemory();

            // Jaringan
            var now = _now();
            var (rx, tx) = ReadNetworkBytes();
            double down = 0, up = 0;
            if (_hasLastNet)
            {
                var elapsed = now - _lastNetAt;
                down = SystemMetrics.BytesPerSecond(_lastNetRx, rx, elapsed);
                up = SystemMetrics.BytesPerSecond(_lastNetTx, tx, elapsed);
            }
            _lastNetRx = rx; _lastNetTx = tx; _lastNetAt = now; _hasLastNet = true;

            // Disk dan GPU (PDH)
            _pdh ??= new PdhSampler();
            var (diskIdle, gpu) = _pdh.Read();
            double? disk = diskIdle is { } idle ? SystemMetrics.DiskActivityFromIdle(idle) : null;

            var memPercent = SystemMetrics.MemoryPercent(total, available);
            snapshot = new SystemSnapshot(
                cpuPercent, memPercent, total - Math.Min(total, available), total,
                disk, down, up, gpu is { } g ? SystemMetrics.Clamp(g) : null,
                TimeSpan.FromMilliseconds(Environment.TickCount64));

            _cpu.Add(cpuPercent);
            _memory.Add(memPercent);
            _disk.Add(disk ?? 0);
            _netDown.Add(down);
            _netUp.Add(up);
            _gpu.Add(snapshot.GpuPercent ?? 0);
            Latest = snapshot;
        }

        Updated?.Invoke(this, EventArgs.Empty);
        return snapshot;
    }

    private static (ulong Rx, ulong Tx) ReadNetworkBytes()
    {
        ulong rx = 0, tx = 0;
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                try
                {
                    var stats = nic.GetIPv4Statistics();
                    rx += (ulong)Math.Max(0, stats.BytesReceived);
                    tx += (ulong)Math.Max(0, stats.BytesSent);
                }
                catch (Exception) { /* adapter tanpa statistik IPv4 */ }
            }
        }
        catch (Exception) { }
        return (rx, tx);
    }

    public void Dispose()
    {
        lock (_lock) _disposed = true;
        Stop();
    }
}

internal static class NativeMetrics
{
    [DllImport("kernel32.dll")]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    private static ulong ToUInt64(FILETIME ft) => ((ulong)(uint)ft.dwHighDateTime << 32) | (uint)ft.dwLowDateTime;

    public static CpuTimes? ReadCpuTimes()
        => GetSystemTimes(out var idle, out var kernel, out var user)
            ? new CpuTimes(ToUInt64(idle), ToUInt64(kernel), ToUInt64(user))
            : null;

    public static (ulong Total, ulong Available) ReadMemory()
    {
        var status = new MemoryStatusEx { dwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        return GlobalMemoryStatusEx(ref status) ? (status.ullTotalPhys, status.ullAvailPhys) : (0, 0);
    }
}

/// <summary>Pembacaan counter disk dan GPU lewat PDH (pdh.dll) dengan nama counter Inggris.</summary>
internal sealed class PdhSampler : IDisposable
{
    private const uint PdhFmtDouble = 0x00000200;
    private const uint PdhMoreData = 0x800007D2;

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct FmtCounterValue
    {
        [FieldOffset(0)] public uint CStatus;
        [FieldOffset(8)] public double DoubleValue;
    }

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQueryW(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounterW(IntPtr query, string fullCounterPath, IntPtr userData, out IntPtr counter);

    [DllImport("pdh.dll")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterValue(IntPtr counter, uint format, out uint type, out FmtCounterValue value);

    [DllImport("pdh.dll")]
    private static extern uint PdhGetFormattedCounterArrayW(IntPtr counter, uint format, ref uint bufferSize, ref uint itemCount, IntPtr buffer);

    [DllImport("pdh.dll")]
    private static extern uint PdhCloseQuery(IntPtr query);

    private IntPtr _query;
    private IntPtr _diskIdle;
    private IntPtr _gpu;
    private bool _primed;

    public PdhSampler()
    {
        try
        {
            if (PdhOpenQueryW(null, IntPtr.Zero, out _query) != 0) { _query = IntPtr.Zero; return; }
            if (PdhAddEnglishCounterW(_query, @"\PhysicalDisk(_Total)\% Idle Time", IntPtr.Zero, out _diskIdle) != 0) _diskIdle = IntPtr.Zero;
            // Item GPU: struct 24 byte hanya dihitung untuk x64 (aplikasi ini x64).
            if (IntPtr.Size == 8 && PdhAddEnglishCounterW(_query, @"\GPU Engine(*)\Utilization Percentage", IntPtr.Zero, out _gpu) != 0) _gpu = IntPtr.Zero;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            _query = IntPtr.Zero;
        }
    }

    /// <summary>Persen waktu idle disk dan jumlah utilisasi engine 3D GPU; null bila tidak tersedia/belum siap.</summary>
    public (double? DiskIdle, double? Gpu) Read()
    {
        if (_query == IntPtr.Zero) return (null, null);
        try
        {
            if (PdhCollectQueryData(_query) != 0) return (null, null);
            if (!_primed) { _primed = true; return (null, null); } // counter laju butuh dua cuplikan

            double? disk = null;
            if (_diskIdle != IntPtr.Zero
                && PdhGetFormattedCounterValue(_diskIdle, PdhFmtDouble, out _, out var value) == 0
                && value.CStatus is 0 or 1)
            {
                disk = value.DoubleValue;
            }

            return (disk, ReadGpu());
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or AccessViolationException)
        {
            return (null, null);
        }
    }

    private double? ReadGpu()
    {
        if (_gpu == IntPtr.Zero) return null;

        uint size = 0, count = 0;
        var status = PdhGetFormattedCounterArrayW(_gpu, PdhFmtDouble, ref size, ref count, IntPtr.Zero);
        if (status != PdhMoreData || size == 0) return null;

        var buffer = Marshal.AllocHGlobal((int)size);
        try
        {
            if (PdhGetFormattedCounterArrayW(_gpu, PdhFmtDouble, ref size, ref count, buffer) != 0) return null;

            double sum = 0;
            var any = false;
            // PDH_FMT_COUNTERVALUE_ITEM_W (x64): nama (8) + CStatus (4, +4 padding) + double (8) = 24 byte.
            for (var i = 0; i < count; i++)
            {
                var item = buffer + i * 24;
                var namePtr = Marshal.ReadIntPtr(item);
                var name = namePtr == IntPtr.Zero ? string.Empty : Marshal.PtrToStringUni(namePtr) ?? string.Empty;
                if (!name.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase)) continue;
                var cStatus = (uint)Marshal.ReadInt32(item, 8);
                if (cStatus is not (0 or 1)) continue;
                sum += BitConverter.Int64BitsToDouble(Marshal.ReadInt64(item, 16));
                any = true;
            }

            return any ? sum : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void Dispose()
    {
        if (_query != IntPtr.Zero)
        {
            try { PdhCloseQuery(_query); } catch (Exception) { }
            _query = IntPtr.Zero;
        }
    }
}
