using System;
using System.Collections.Generic;
using System.Linq;
using TamaNotch.Services.SystemMonitor;
using TamaNotch.ViewModels;
using TamaNotch.Views;
using Xunit;

namespace TamaNotch.Tests;

public class SystemMonitorViewModelTests
{
    private sealed class StubMonitor : ISystemMonitorService
    {
        public event EventHandler? Updated;
        public SystemSnapshot? Latest { get; set; }
        public bool IsRunning { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public MonitorHistories Histories { get; set; } = new(new[] { 10.0, 20.0 }, new[] { 50.0 }, new[] { 1.0 }, new[] { 100.0 }, new[] { 50.0 }, new[] { 5.0 });
        public MonitorHistories GetHistories() => Histories;
        public void Start() { IsRunning = true; StartCount++; }
        public void Stop() { IsRunning = false; StopCount++; }
        public void Raise() => Updated?.Invoke(this, EventArgs.Empty);
        public void Dispose() { }
    }

    private static SystemSnapshot Snapshot(double? disk = 12, double? gpu = 3) => new(
        CpuPercent: 28.4, MemoryPercent: 77, MemoryUsedBytes: 12UL * 1024 * 1024 * 1024, MemoryTotalBytes: 16UL * 1024 * 1024 * 1024,
        DiskPercent: disk, NetDownBytesPerSecond: 2048, NetUpBytesPerSecond: 512, GpuPercent: gpu,
        Uptime: new TimeSpan(0, 14, 45, 58));

    private static (MainViewModel Vm, StubMonitor Monitor) Create()
    {
        var monitor = new StubMonitor();
        var vm = ViewModelTestFactory.CreateMain(systemMonitor: monitor);
        return (vm, monitor);
    }

    [Fact]
    public void Sampling_RunsOnlyWhileTheMonitorPanelIsShown()
    {
        var (vm, monitor) = Create();
        Assert.False(monitor.IsRunning);

        vm.OnMouseEnter();
        vm.OpenMonitorCommand.Execute(null);
        Assert.Equal(MainViewModel.PanelMonitor, vm.EmbeddedPanel);
        Assert.True(monitor.IsRunning);

        vm.CloseEmbeddedPanel();
        Assert.False(monitor.IsRunning);
    }

    [Fact]
    public void CollapsingTheIsland_StopsSampling()
    {
        var (vm, monitor) = Create();
        vm.OnMouseEnter();
        vm.OpenMonitorCommand.Execute(null);
        Assert.True(monitor.IsRunning);

        vm.OnMouseLeave();
        vm.CollapseHoverIfIdle();

        Assert.False(monitor.IsRunning);
        Assert.Null(vm.EmbeddedPanel);
    }

    [Fact]
    public void OtherPanels_DoNotStartSampling()
    {
        var (vm, monitor) = Create();
        vm.OnMouseEnter();
        vm.OpenEmbeddedPanel(MainViewModel.PanelRadio);

        Assert.Equal(0, monitor.StartCount);
    }

    [Fact]
    public void Snapshot_IsFormattedIntoTextsAndHistories()
    {
        var (vm, monitor) = Create();
        monitor.Latest = Snapshot();

        monitor.Raise();

        Assert.Equal("28%", vm.MonitorCpuText);
        Assert.Equal("77%", vm.MonitorMemoryText);
        Assert.Contains("12", vm.MonitorMemoryDetail);
        Assert.Contains("16", vm.MonitorMemoryDetail);
        Assert.Equal("12%", vm.MonitorDiskText);
        Assert.Equal("3%", vm.MonitorGpuText);
        Assert.True(vm.MonitorHasDisk && vm.MonitorHasGpu);
        Assert.Contains("↓", vm.MonitorNetText);
        Assert.Contains("↑", vm.MonitorNetText);
        Assert.Equal("0:14:45:58", vm.MonitorUptimeFull);
        Assert.Equal(new[] { 10.0, 20.0 }, vm.MonitorCpuHistory.ToArray());
    }

    [Fact]
    public void MissingDiskAndGpu_AreMarkedUnavailable()
    {
        var (vm, monitor) = Create();
        monitor.Latest = Snapshot(disk: null, gpu: null);

        monitor.Raise();

        Assert.False(vm.MonitorHasDisk);
        Assert.False(vm.MonitorHasGpu);
        Assert.Equal("Tidak tersedia", vm.MonitorGpuText);
    }

    [Fact]
    public void RaisedWithoutAnySnapshot_IsIgnored()
    {
        var (vm, monitor) = Create();
        var before = vm.MonitorCpuText;

        monitor.Raise();

        Assert.Equal(before, vm.MonitorCpuText);
    }

    [Fact]
    public void UptimePill_ShowsOnlyWhileTheIslandIsOpen_AndFormatsHoursAndMinutes()
    {
        var (vm, _) = Create();
        vm.SetUptimeProvider(() => new TimeSpan(0, 14, 45, 58));
        Assert.False(vm.ShowUptimePill);

        vm.OnMouseEnter();

        Assert.True(vm.ShowUptimePill);
        Assert.Equal("Up 0:14:45", vm.UptimeText);
        Assert.Equal("0:14:45:58", vm.MonitorUptimeFull);
    }

    [Fact]
    public void UptimePill_FollowsTheSetting()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateMain(settings, systemMonitor: new StubMonitor());
        vm.OnMouseEnter();
        Assert.True(vm.ShowUptimePill);

        settings.CurrentSettings.ShowUptimeInIsland = false;
        vm.SyncSystemMonitorWithSettings();

        Assert.False(vm.ShowUptimePill);
    }

    [Fact]
    public void DisablingTheMonitor_ClosesItsPanelAndHidesTheBadge()
    {
        var (vm, monitor) = Create();
        vm.OnMouseEnter();
        Assert.True(vm.ShowMonitorBadge);
        vm.OpenMonitorCommand.Execute(null);

        vm.EnableSystemMonitor = false;

        Assert.Null(vm.EmbeddedPanel);
        Assert.False(vm.ShowMonitorBadge);
        Assert.False(monitor.IsRunning);
    }

    [Fact]
    public void SettingsViewModel_PersistsMonitorOptions()
    {
        var settings = new ViewModelTestFactory.MemorySettings();
        var vm = ViewModelTestFactory.CreateSettings(settings);
        Assert.True(vm.EnableSystemMonitor);
        Assert.True(vm.ShowUptimeInIsland);

        vm.EnableSystemMonitor = false;
        vm.ShowUptimeInIsland = false;

        Assert.False(settings.CurrentSettings.EnableSystemMonitor);
        Assert.False(settings.CurrentSettings.ShowUptimeInIsland);
    }

    // --- Grafik ---

    [Fact]
    public void Sparkline_NewestPointSticksToTheRightEdge_AndScalesToMax()
    {
        var points = SparklineChart.ComputePoints(new[] { 0.0, 50.0, 100.0 }, width: 118, height: 40, capacity: 60, max: 100);

        Assert.Equal(3, points.Count);
        Assert.Equal(118, points[2].X, 6);
        Assert.Equal(118 - 2 * (118.0 / 59), points[0].X, 6);
        Assert.Equal(40, points[0].Y, 6);   // 0% di dasar
        Assert.Equal(20, points[1].Y, 6);   // 50% di tengah
        Assert.Equal(0, points[2].Y, 6);    // 100% di puncak
    }

    [Fact]
    public void Sparkline_ClampsOutOfRangeValues_AndHandlesEmptyInput()
    {
        var clamped = SparklineChart.ComputePoints(new[] { -10.0, 500.0 }, 100, 50, 10, 100);
        Assert.Equal(50, clamped[0].Y, 6);
        Assert.Equal(0, clamped[1].Y, 6);

        Assert.Empty(SparklineChart.ComputePoints(null, 100, 50, 10, 100));
        Assert.Empty(SparklineChart.ComputePoints(Array.Empty<double>(), 100, 50, 10, 100));
        Assert.Empty(SparklineChart.ComputePoints(new[] { 1.0 }, 0, 50, 10, 100));
    }

    [Fact]
    public void Sparkline_UsesOnlyTheLatestCapacityPoints()
    {
        var values = Enumerable.Range(0, 100).Select(i => (double)i).ToArray();

        var points = SparklineChart.ComputePoints(values, 590, 100, 60, 100);

        Assert.Equal(60, points.Count);
        Assert.Equal(590, points[^1].X, 6);
        Assert.Equal(0, points[0].X, 6);
    }

    [Fact]
    public void Sparkline_AutoScale_UsesPeakWithHeadroomAndMinimum()
    {
        Assert.Equal(100, SparklineChart.ResolveMax(new[] { 1.0 }, 100, 1));
        Assert.Equal(10240, SparklineChart.ResolveMax(new[] { 5.0, 100.0 }, 0, 10240));
        Assert.Equal(2000 * 1.15, SparklineChart.ResolveMax(new[] { 2000.0, 10.0 }, 0, 100), 6);
        Assert.Equal(1, SparklineChart.ResolveMax(null, 0, 0));
    }
}
