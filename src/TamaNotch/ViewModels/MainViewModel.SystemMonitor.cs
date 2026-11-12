using System.Globalization;
using System.Windows.Input;
using System.Windows.Threading;
using TamaNotch.Helpers;
using TamaNotch.Services.SystemMonitor;

namespace TamaNotch.ViewModels;

/// <summary>
/// Monitor sistem (CPU, memori, disk, jaringan, GPU) sebagai panel tertanam di island, plus pil uptime di baris info.
/// Sampling hanya berjalan selama panelnya terbuka; uptime cukup dari <see cref="Environment.TickCount64"/>.
/// </summary>
public partial class MainViewModel
{
    public const string PanelMonitor = "Monitor";

    private ISystemMonitorService _systemMonitor = null!;
    private Dispatcher _monitorDispatcher = null!;
    private DispatcherTimer? _uptimeTimer;
    private Func<TimeSpan> _uptimeProvider = () => TimeSpan.FromMilliseconds(Environment.TickCount64);

    private static readonly double[] NoHistory = Array.Empty<double>();

    private string _monitorCpuText = "—";
    private string _monitorMemoryText = "—";
    private string _monitorMemoryDetail = string.Empty;
    private string _monitorDiskText = "—";
    private string _monitorNetText = "—";
    private string _monitorGpuText = "—";
    private bool _monitorHasDisk;
    private bool _monitorHasGpu;
    private IReadOnlyList<double> _monitorCpuHistory = NoHistory;
    private IReadOnlyList<double> _monitorMemoryHistory = NoHistory;
    private IReadOnlyList<double> _monitorDiskHistory = NoHistory;
    private IReadOnlyList<double> _monitorNetDownHistory = NoHistory;
    private IReadOnlyList<double> _monitorNetUpHistory = NoHistory;
    private IReadOnlyList<double> _monitorGpuHistory = NoHistory;
    private string _monitorUptimeFull = string.Empty;

    public ICommand OpenMonitorCommand { get; private set; } = null!;

    private void InitializeSystemMonitor(ISystemMonitorService? service)
    {
        _systemMonitor = service ?? new SystemMonitorService();
        _monitorDispatcher = Dispatcher.CurrentDispatcher;
        _systemMonitor.Updated += (s, e) =>
        {
            if (_monitorDispatcher.CheckAccess()) ApplyMonitorSnapshot();
            else _monitorDispatcher.BeginInvoke(ApplyMonitorSnapshot);
        };

        OpenMonitorCommand = new RelayCommand(() => OpenEmbeddedPanel(PanelMonitor));
    }

    /// <summary>Penyedia uptime yang bisa diganti di tes.</summary>
    public void SetUptimeProvider(Func<TimeSpan> provider) => _uptimeProvider = provider;

    public bool EnableSystemMonitor
    {
        get => Settings.EnableSystemMonitor;
        set
        {
            if (Settings.EnableSystemMonitor == value) return;
            Settings.EnableSystemMonitor = value;
            _settingsService.Save();
            SyncSystemMonitorWithSettings();
        }
    }

    /// <summary>Dipanggil setelah Pengaturan ditutup (atau EnableSystemMonitor diubah).</summary>
    public void SyncSystemMonitorWithSettings()
    {
        if (!Settings.EnableSystemMonitor && EmbeddedPanel == PanelMonitor) CloseEmbeddedPanel();
        OnPropertyChanged(nameof(EnableSystemMonitor));
        OnPropertyChanged(nameof(ShowMonitorBadge));
        OnPropertyChanged(nameof(ShowUptimePill));
        RefreshUptime();
    }

    public bool ShowMonitorBadge => ShowComponentIcons && Settings.EnableSystemMonitor;

    /// <summary>Pil uptime di baris info island (sebaris dengan sholat, cuaca, Claude).</summary>
    public bool ShowUptimePill => ShowComponentIcons && Settings.ShowUptimeInIsland;

    public string UptimeText => "Up " + SystemMetrics.FormatUptime(_uptimeProvider(), withSeconds: false);

    public string UptimeToolTip => UptimeLevel(_uptimeProvider()) switch
    {
        0 => "Uptime komputer (hari:jam:menit) sejak Windows menyala. Kurang dari 1 minggu.",
        1 => "Uptime 1-2 minggu. Pertimbangkan restart.",
        2 => "Uptime 2-3 minggu. Sebaiknya restart.",
        _ => "Uptime lebih dari 3 minggu. Restart komputer disarankan."
    };

    /// <summary>Tingkat uptime: 0 = &lt;1 minggu, 1 = 1-2 minggu, 2 = 2-3 minggu, 3 = 3 minggu ke atas.</summary>
    public static int UptimeLevel(TimeSpan uptime) => uptime.TotalDays switch
    {
        < 7 => 0,
        < 14 => 1,
        < 21 => 2,
        _ => 3
    };

    /// <summary>Warna neon per tingkat: hijau, kuning, oranye, merah.</summary>
    public static readonly System.Windows.Media.Color[] UptimeColors =
    {
        System.Windows.Media.Color.FromRgb(0x39, 0xFF, 0x14),
        System.Windows.Media.Color.FromRgb(0xFF, 0xE6, 0x00),
        System.Windows.Media.Color.FromRgb(0xFF, 0x8C, 0x00),
        System.Windows.Media.Color.FromRgb(0xFF, 0x31, 0x31)
    };

    /// <summary>Versi lebih gelap untuk tema Putih (neon tidak terbaca di atas pil putih).</summary>
    public static readonly System.Windows.Media.Color[] UptimeColorsLight =
    {
        System.Windows.Media.Color.FromRgb(0x1A, 0x9E, 0x00),
        System.Windows.Media.Color.FromRgb(0xA8, 0x8C, 0x00),
        System.Windows.Media.Color.FromRgb(0xD9, 0x62, 0x00),
        System.Windows.Media.Color.FromRgb(0xD6, 0x1F, 0x1F)
    };

    private static readonly System.Windows.Media.SolidColorBrush[] UptimeBrushes = ToBrushes(UptimeColors);
    private static readonly System.Windows.Media.SolidColorBrush[] UptimeBrushesLight = ToBrushes(UptimeColorsLight);

    private static System.Windows.Media.SolidColorBrush[] ToBrushes(System.Windows.Media.Color[] colors) => colors.Select(c =>
    {
        var brush = new System.Windows.Media.SolidColorBrush(c);
        brush.Freeze();
        return brush;
    }).ToArray();

    public System.Windows.Media.Brush UptimeBrush
        => (Services.Theme.ThemeService.Current == Services.Theme.ThemeService.Light ? UptimeBrushesLight : UptimeBrushes)
            [UptimeLevel(_uptimeProvider())];

    public string MonitorUptimeFull
    {
        get => _monitorUptimeFull;
        private set => SetProperty(ref _monitorUptimeFull, value);
    }

    public string MonitorCpuText { get => _monitorCpuText; private set => SetProperty(ref _monitorCpuText, value); }
    public string MonitorMemoryText { get => _monitorMemoryText; private set => SetProperty(ref _monitorMemoryText, value); }
    public string MonitorMemoryDetail { get => _monitorMemoryDetail; private set => SetProperty(ref _monitorMemoryDetail, value); }
    public string MonitorDiskText { get => _monitorDiskText; private set => SetProperty(ref _monitorDiskText, value); }
    public string MonitorNetText { get => _monitorNetText; private set => SetProperty(ref _monitorNetText, value); }
    public string MonitorGpuText { get => _monitorGpuText; private set => SetProperty(ref _monitorGpuText, value); }
    public bool MonitorHasDisk { get => _monitorHasDisk; private set => SetProperty(ref _monitorHasDisk, value); }
    public bool MonitorHasGpu { get => _monitorHasGpu; private set => SetProperty(ref _monitorHasGpu, value); }
    public IReadOnlyList<double> MonitorCpuHistory { get => _monitorCpuHistory; private set => SetProperty(ref _monitorCpuHistory, value); }
    public IReadOnlyList<double> MonitorMemoryHistory { get => _monitorMemoryHistory; private set => SetProperty(ref _monitorMemoryHistory, value); }
    public IReadOnlyList<double> MonitorDiskHistory { get => _monitorDiskHistory; private set => SetProperty(ref _monitorDiskHistory, value); }
    public IReadOnlyList<double> MonitorNetDownHistory { get => _monitorNetDownHistory; private set => SetProperty(ref _monitorNetDownHistory, value); }
    public IReadOnlyList<double> MonitorNetUpHistory { get => _monitorNetUpHistory; private set => SetProperty(ref _monitorNetUpHistory, value); }
    public IReadOnlyList<double> MonitorGpuHistory { get => _monitorGpuHistory; private set => SetProperty(ref _monitorGpuHistory, value); }

    /// <summary>Mulai/hentikan sampling sesuai panel: berjalan hanya selama panel Monitor tampil.</summary>
    private void UpdateMonitorRunning()
    {
        var wanted = EmbeddedPanel == PanelMonitor && ShowEmbeddedPanel;
        if (wanted && !_systemMonitor.IsRunning)
        {
            _systemMonitor.Start();
        }
        else if (!wanted && _systemMonitor.IsRunning)
        {
            _systemMonitor.Stop();
        }
    }

    private void RefreshUptime()
    {
        OnPropertyChanged(nameof(UptimeText));
        OnPropertyChanged(nameof(UptimeBrush));
        OnPropertyChanged(nameof(UptimeToolTip));
        MonitorUptimeFull = SystemMetrics.FormatUptime(_uptimeProvider(), withSeconds: true);
    }

    /// <summary>Pil uptime diperbarui tiap 30 detik selama island terbuka.</summary>
    private void UpdateUptimeTimer()
    {
        if (ShowComponentIcons)
        {
            RefreshUptime();
            if (_uptimeTimer == null)
            {
                _uptimeTimer = new DispatcherTimer(DispatcherPriority.Background, _monitorDispatcher) { Interval = TimeSpan.FromSeconds(30) };
                _uptimeTimer.Tick += (s, e) => RefreshUptime();
            }
            _uptimeTimer.Start();
        }
        else
        {
            _uptimeTimer?.Stop();
        }
    }

    private void ApplyMonitorSnapshot()
    {
        var snapshot = _systemMonitor.Latest;
        if (snapshot == null) return;
        var culture = CultureInfo.CurrentCulture;

        MonitorCpuText = string.Format(culture, "{0:0}%", snapshot.CpuPercent);
        MonitorMemoryText = string.Format(culture, "{0:0}%", snapshot.MemoryPercent);
        MonitorMemoryDetail = SystemMetrics.FormatMemory(snapshot.MemoryUsedBytes, snapshot.MemoryTotalBytes, culture);
        MonitorHasDisk = snapshot.DiskPercent.HasValue;
        MonitorDiskText = snapshot.DiskPercent is { } disk ? string.Format(culture, "{0:0}%", disk) : "Tidak tersedia";
        MonitorNetText = $"↓ {SystemMetrics.FormatRate(snapshot.NetDownBytesPerSecond, culture)}   ↑ {SystemMetrics.FormatRate(snapshot.NetUpBytesPerSecond, culture)}";
        MonitorHasGpu = snapshot.GpuPercent.HasValue;
        MonitorGpuText = snapshot.GpuPercent is { } gpu ? string.Format(culture, "{0:0}%", gpu) : "Tidak tersedia";
        MonitorUptimeFull = SystemMetrics.FormatUptime(snapshot.Uptime, withSeconds: true);

        var history = _systemMonitor.GetHistories();
        MonitorCpuHistory = history.Cpu;
        MonitorMemoryHistory = history.Memory;
        MonitorDiskHistory = history.Disk;
        MonitorNetDownHistory = history.NetDown;
        MonitorNetUpHistory = history.NetUp;
        MonitorGpuHistory = history.Gpu;
    }
}
