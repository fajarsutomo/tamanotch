using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using TamaNotch.Services.SystemMonitor;
using Xunit;

namespace TamaNotch.Tests;

public class SystemMonitorTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static readonly CultureInfo Id = CultureInfo.GetCultureInfo("id-ID");

    [Fact]
    public void CpuPercent_IsBusyShareOfKernelPlusUser()
    {
        // 100 satuan: kernel 40 (idle 30), user 60 => busy = 40 - 30 + 60 = 70 dari 100
        var prev = new CpuTimes(Idle: 1000, Kernel: 2000, User: 3000);
        var curr = new CpuTimes(Idle: 1030, Kernel: 2040, User: 3060);

        Assert.Equal(70, SystemMetrics.CpuPercent(prev, curr), 6);
    }

    [Fact]
    public void CpuPercent_FullyIdle_IsZero_AndNoDelta_IsZero()
    {
        var prev = new CpuTimes(0, 0, 0);
        Assert.Equal(0, SystemMetrics.CpuPercent(prev, new CpuTimes(100, 100, 0)), 6);
        Assert.Equal(0, SystemMetrics.CpuPercent(prev, prev));
    }

    [Fact]
    public void CpuPercent_CounterGoingBackwards_DoesNotUnderflow()
    {
        var prev = new CpuTimes(500, 500, 500);
        var curr = new CpuTimes(100, 100, 100);

        Assert.Equal(0, SystemMetrics.CpuPercent(prev, curr));
    }

    [Theory]
    [InlineData(1000UL, 3000UL, 2.0, 1000.0)]
    [InlineData(3000UL, 1000UL, 1.0, 0.0)]   // adapter reset
    [InlineData(10UL, 10UL, 1.0, 0.0)]
    [InlineData(0UL, 500UL, 0.0, 0.0)]        // tanpa selisih waktu
    public void BytesPerSecond_UsesDeltaOverElapsed(ulong previous, ulong current, double seconds, double expected)
        => Assert.Equal(expected, SystemMetrics.BytesPerSecond(previous, current, TimeSpan.FromSeconds(seconds)), 6);

    [Fact]
    public void MemoryPercent_IsUsedShare()
    {
        Assert.Equal(75, SystemMetrics.MemoryPercent(1000, 250), 6);
        Assert.Equal(0, SystemMetrics.MemoryPercent(0, 0));
        Assert.Equal(0, SystemMetrics.MemoryPercent(100, 200));
    }

    [Theory]
    [InlineData(100.0, 0.0)]
    [InlineData(0.0, 100.0)]
    [InlineData(37.5, 62.5)]
    [InlineData(150.0, 0.0)]
    [InlineData(-5.0, 100.0)]
    public void DiskActivity_IsHundredMinusIdle_Clamped(double idle, double expected)
        => Assert.Equal(expected, SystemMetrics.DiskActivityFromIdle(idle), 6);

    [Fact]
    public void FormatUptime_MatchesTaskManagerLayout()
    {
        var uptime = new TimeSpan(0, 14, 45, 58);

        Assert.Equal("0:14:45:58", SystemMetrics.FormatUptime(uptime, withSeconds: true));
        Assert.Equal("0:14:45", SystemMetrics.FormatUptime(uptime, withSeconds: false));
        Assert.Equal("3:02:05", SystemMetrics.FormatUptime(new TimeSpan(3, 2, 5, 59), withSeconds: false));
        Assert.Equal("0:00:00", SystemMetrics.FormatUptime(TimeSpan.FromSeconds(-5), withSeconds: false));
    }

    [Fact]
    public void FormatRate_PicksUnit()
    {
        Assert.Equal("512 B/s", SystemMetrics.FormatRate(512, Id));
        Assert.Equal("3,5 KB/s", SystemMetrics.FormatRate(3.5 * 1024, Id));
        Assert.Equal("5,1 MB/s", SystemMetrics.FormatRate(5.1 * 1024 * 1024, Id));
    }

    [Fact]
    public void FormatMemory_ShowsUsedOverTotalGb()
    {
        const ulong gb = 1024UL * 1024 * 1024;
        Assert.Equal("12,1 / 15,7 GB", SystemMetrics.FormatMemory((ulong)(12.1 * gb), (ulong)(15.7 * gb), Id));
    }

    [Fact]
    public void MetricHistory_KeepsOnlyTheLatestValues()
    {
        var history = new MetricHistory(3);
        for (var i = 1; i <= 5; i++) history.Add(i);

        Assert.Equal(new[] { 3.0, 4.0, 5.0 }, history.ToArray());
        Assert.Equal(3, history.Count);
    }

    // --- Pembacaan nyata di mesin ini (memastikan P/Invoke benar) ---

    [Fact]
    public void RealSampling_ReturnsPlausibleValues()
    {
        using var service = new SystemMonitorService();

        service.Sample();           // cuplikan pertama menyiapkan selisih CPU/jaringan/PDH
        Thread.Sleep(1100);
        var s = service.Sample();

        Assert.InRange(s.MemoryPercent, 1, 99.9);
        Assert.True(s.MemoryTotalBytes > 512UL * 1024 * 1024);
        Assert.InRange(s.MemoryUsedBytes, 1UL, s.MemoryTotalBytes);
        Assert.InRange(s.CpuPercent, 0, 100);
        Assert.True(s.NetDownBytesPerSecond >= 0 && s.NetUpBytesPerSecond >= 0);
        Assert.True(s.Uptime > TimeSpan.Zero);
        if (s.DiskPercent is { } disk) Assert.InRange(disk, 0, 100);
        if (s.GpuPercent is { } gpu) Assert.InRange(gpu, 0, 100);
        output.WriteLine($"cpu={s.CpuPercent:0.0} mem={s.MemoryPercent:0.0} disk={s.DiskPercent?.ToString("0.0") ?? "n/a"} gpu={s.GpuPercent?.ToString("0.0") ?? "n/a"} down={s.NetDownBytesPerSecond:0} up={s.NetUpBytesPerSecond:0} up={s.Uptime}");
        Assert.Same(s, service.Latest);

        var histories = service.GetHistories();
        Assert.Equal(2, histories.Cpu.Length);
        Assert.Equal(2, histories.Memory.Length);
    }

    [Fact]
    public void Updated_IsRaisedForEachSample_AndStartStopControlTheTimer()
    {
        using var service = new SystemMonitorService { Interval = TimeSpan.FromMilliseconds(50) };
        var count = 0;
        service.Updated += (s, e) => Interlocked.Increment(ref count);

        Assert.False(service.IsRunning);
        service.Start();
        Assert.True(service.IsRunning);
        SpinWait.SpinUntil(() => Volatile.Read(ref count) >= 3, TimeSpan.FromSeconds(5));
        service.Stop();

        Assert.False(service.IsRunning);
        Assert.True(count >= 3, $"hanya {count} cuplikan");
        var after = Volatile.Read(ref count);
        Thread.Sleep(200);
        Assert.InRange(Volatile.Read(ref count), after, after + 1); // berhenti setelah Stop
    }

    [Fact]
    public void MetricHistory_RejectsTinyCapacity()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new MetricHistory(1));
}
