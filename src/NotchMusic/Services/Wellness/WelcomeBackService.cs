using System.Windows.Threading;
using Microsoft.Win32;
using NotchMusic.Helpers;
using NotchMusic.Services.Settings;

namespace NotchMusic.Services.Wellness;

public interface IWelcomeBackService : IDisposable
{
    /// <summary>Dipicu saat pengguna kembali setelah pergi sepanjang ambang; argumen = lama pergi.</summary>
    event Action<TimeSpan>? Returned;

    void Start();
    void Stop();

    /// <summary>Satu putaran pemeriksaan (dipanggil timer; dibuka agar bisa dites).</summary>
    void Tick();
}

/// <summary>
/// Mendeteksi pengguna kembali setelah pergi lama (default 1 jam), tanpa kaitan dengan sholat atau jam kerja.
/// Tiga keadaan ditangani:
/// 1) Layar tidak dikunci: tanpa input mouse/keyboard sepanjang ambang, lalu ada input lagi.
/// 2) Layar dikunci: sapaan ditahan sampai benar-benar login (mengetik password di layar kunci ikut mereset hitungan
///    idle, jadi tanpa penahanan sapaan muncul saat layar kunci masih tampil dan terlewat); lama pergi dihitung sejak terkunci.
/// 3) Sleep/hibernasi: timer ikut berhenti, jadi lama pergi dihitung dari saat sleep; sapaan muncul begitu ada input
///    setelah bangun (atau setelah login bila layar terkunci saat bangun).
/// </summary>
public class WelcomeBackService : IWelcomeBackService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(3);

    /// <summary>Jeda setelah bangun dari sleep sebelum input dianggap kembalinya pengguna; memberi waktu layar kunci muncul.</summary>
    internal static readonly TimeSpan ResumeSettle = TimeSpan.FromSeconds(6);

    private static readonly TimeSpan ActiveInputWindow = TimeSpan.FromSeconds(10);

    private readonly ISettingsService _settingsService;
    private readonly Func<TimeSpan> _idleTime;
    private readonly Func<DateTime> _now;
    private readonly object _gate = new();
    private DispatcherTimer? _timer;
    private TimeSpan _longestIdle = TimeSpan.Zero;
    private DateTime? _awaySince;
    private DateTime _resumedAt = DateTime.MinValue;
    private bool _locked;
    private bool _suspended;
    private bool _hooked;

    public event Action<TimeSpan>? Returned;

    public WelcomeBackService(ISettingsService settingsService, Func<TimeSpan>? idleTime = null, Func<DateTime>? now = null)
    {
        _settingsService = settingsService;
        _idleTime = idleTime ?? UserIdleHelper.GetIdleTime;
        _now = now ?? (() => DateTime.Now);
    }

    public void Start()
    {
        if (_timer != null) return;
        _timer = new DispatcherTimer { Interval = TickInterval };
        _timer.Tick += (s, e) => Tick();
        _timer.Start();

        if (_hooked) return;
        try
        {
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            _hooked = true;
        }
        catch
        {
            // Tanpa peristiwa sistem, deteksi idle biasa tetap berjalan.
        }
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
        if (!_hooked) return;
        _hooked = false;
        try
        {
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        }
        catch
        {
        }
    }

    private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
            case SessionSwitchReason.ConsoleDisconnect:
            case SessionSwitchReason.RemoteDisconnect:
                OnLocked();
                break;
            case SessionSwitchReason.SessionUnlock:
            case SessionSwitchReason.SessionLogon:
            case SessionSwitchReason.ConsoleConnect:
            case SessionSwitchReason.RemoteConnect:
                OnUnlocked();
                break;
        }
    }

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Suspend) OnSuspended();
        else if (e.Mode == PowerModes.Resume) OnResumed();
    }

    // --- Peristiwa sistem (publik agar bisa dites) ---

    public void OnLocked()
    {
        lock (_gate)
        {
            _locked = true;
            _awaySince ??= _now();
        }
    }

    public void OnSuspended()
    {
        lock (_gate)
        {
            _suspended = true;
            _awaySince ??= _now();
        }
    }

    public void OnResumed()
    {
        lock (_gate)
        {
            _suspended = false;
            _resumedAt = _now();
        }
    }

    /// <summary>Login/buka kunci: saatnya menyapa bila total waktu pergi melewati ambang.</summary>
    public void OnUnlocked()
    {
        TimeSpan away;
        lock (_gate)
        {
            _locked = false;
            if (_suspended) return;
            away = TakeAway();
        }

        FireIfLongEnough(away);
    }

    /// <summary>Ambil lama pergi terpanjang dari semua sumber lalu bersihkan keadaan (di dalam kunci).</summary>
    private TimeSpan TakeAway()
    {
        var fromEvents = _awaySince is { } since ? _now() - since : TimeSpan.Zero;
        var away = fromEvents > _longestIdle ? fromEvents : _longestIdle;
        _awaySince = null;
        _longestIdle = TimeSpan.Zero;
        return away;
    }

    private void FireIfLongEnough(TimeSpan away)
    {
        var settings = _settingsService.CurrentSettings;
        if (!settings.EnableWelcomeBack) return;
        var threshold = TimeSpan.FromMinutes(Math.Max(1, settings.WelcomeBackIdleMinutes));
        if (away >= threshold) Returned?.Invoke(away);
    }

    public void Tick()
    {
        var settings = _settingsService.CurrentSettings;
        TimeSpan away;
        lock (_gate)
        {
            if (!settings.EnableWelcomeBack)
            {
                _longestIdle = TimeSpan.Zero;
                _awaySince = null;
                return;
            }

            var threshold = TimeSpan.FromMinutes(Math.Max(1, settings.WelcomeBackIdleMinutes));
            var idle = _idleTime();

            // Terkunci/sleep: input di layar kunci bukan kembalinya pengguna. Catat saja, sapaan menunggu login.
            if (_locked || _suspended)
            {
                if (idle > _longestIdle) _longestIdle = idle;
                return;
            }

            // Masih pergi tanpa kunci: catat idle terpanjang (itulah lama pergi saat nanti kembali).
            if (idle >= threshold)
            {
                if (idle > _longestIdle) _longestIdle = idle;
                return;
            }

            // Baru bangun dari sleep: beri waktu layar kunci muncul sebelum input dianggap kembalinya pengguna.
            if (_awaySince != null)
            {
                bool settled = _now() - _resumedAt >= ResumeSettle;
                bool userActive = idle < ActiveInputWindow;
                if (!settled || !userActive) return;
            }

            away = TakeAway();
        }

        FireIfLongEnough(away);
    }

    public void Dispose() => Stop();
}
