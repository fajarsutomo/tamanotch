using System.Windows.Threading;
using TamaNotch.Helpers;
using TamaNotch.Models;
using TamaNotch.Services.Settings;

namespace TamaNotch.Services.Wellness;

public interface IWellnessReminderService : IDisposable
{
    event Action<WellnessNotificationEvent>? ReminderTriggered;

    /// <summary>Dipicu saat jumlah gelas hari ini berubah.</summary>
    event Action? StateChanged;

    int DrinkCountToday { get; }
    int DrinkGoalGlasses { get; }
    int DrinkGlassMl { get; }

    void Start();
    void Stop();

    /// <summary>Satu putaran penjadwalan (dipanggil timer; dibuka agar bisa dites).</summary>
    void Tick();

    void LogDrink();
    void ResetDrinkCount();
    void MarkMoved();
    void Snooze(WellnessReminderKind kind, int minutes);
}

/// <summary>
/// Pengingat gerak dan minum. Hanya berbunyi pada jam kerja, tidak saat pengguna sedang pergi
/// (tanpa input >= IdleBreakThreshold; untuk gerak itu sudah dihitung sebagai jeda), dan pengingat minum
/// berhenti setelah target harian tercapai.
/// </summary>
public class WellnessReminderService : IWellnessReminderService
{
    public static readonly TimeSpan IdleBreakThreshold = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(15);

    private readonly ISettingsService _settingsService;
    private readonly Func<DateTime> _now;
    private readonly Func<TimeSpan> _idleTime;
    private DispatcherTimer? _timer;

    private DateTime? _moveDue;
    private DateTime? _drinkDue;
    private int _moveIntervalScheduled;
    private int _drinkIntervalScheduled;

    public event Action<WellnessNotificationEvent>? ReminderTriggered;
    public event Action? StateChanged;

    public WellnessReminderService(ISettingsService settingsService, Func<DateTime>? now = null, Func<TimeSpan>? idleTime = null,
        Func<bool>? isFasting = null)
    {
        _settingsService = settingsService;
        _now = now ?? (() => DateTime.Now);
        _idleTime = idleTime ?? UserIdleHelper.GetIdleTime;
        _isFasting = isFasting ?? (() => false);
    }

    private readonly Func<bool> _isFasting;

    private AppSettings Settings => _settingsService.CurrentSettings;

    public int DrinkCountToday
    {
        get
        {
            ResetCounterIfNewDay(notify: false);
            return Settings.DrinkCountToday;
        }
    }

    public int DrinkGoalGlasses => Math.Max(1, Settings.DrinkDailyGoalGlasses);
    public int DrinkGlassMl => Math.Max(50, Settings.DrinkGlassMl);

    public void Start()
    {
        if (_timer != null) return;
        _timer = new DispatcherTimer { Interval = TickInterval };
        _timer.Tick += (s, e) => Tick();
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _timer = null;
    }

    public void Tick()
    {
        var now = _now();
        ResetCounterIfNewDay();

        bool inWorkHours = IsWithinWorkHours(now);
        bool away = _idleTime() >= IdleBreakThreshold;

        TickMove(now, inWorkHours, away);
        TickDrink(now, inWorkHours, away);
    }

    private void TickMove(DateTime now, bool inWorkHours, bool away)
    {
        int interval = Math.Max(1, Settings.MoveReminderMinutes);
        if (!Settings.MoveReminderEnabled || !inWorkHours)
        {
            _moveDue = null;
            return;
        }

        // Pergi dari komputer dihitung sebagai jeda gerak: mulai hitung dari awal lagi.
        if (_moveDue == null || _moveIntervalScheduled != interval || away)
        {
            _moveDue = now.AddMinutes(interval);
            _moveIntervalScheduled = interval;
            return;
        }

        if (now >= _moveDue)
        {
            _moveDue = now.AddMinutes(interval);
            Raise(new WellnessNotificationEvent
            {
                Kind = WellnessReminderKind.Move,
                Title = "Waktunya bergerak",
                Message = $"Sudah sekitar {interval} menit duduk. Berdiri, jalan, dan regangkan badan 2-5 menit.",
                PrimaryActionText = "Sudah bergerak"
            });
        }
    }

    private void TickDrink(DateTime now, bool inWorkHours, bool away)
    {
        int interval = Math.Max(1, Settings.DrinkReminderMinutes);
        if (!Settings.DrinkReminderEnabled || !inWorkHours)
        {
            _drinkDue = null;
            return;
        }

        // Sedang berpuasa (Imsak sampai Maghrib): tidak mengingatkan minum; hitungan mulai lagi setelah berbuka.
        if (_isFasting())
        {
            _drinkDue = null;
            return;
        }

        if (_drinkDue == null || _drinkIntervalScheduled != interval)
        {
            _drinkDue = now.AddMinutes(interval);
            _drinkIntervalScheduled = interval;
            return;
        }

        // Saat pengguna pergi, jatuh tempo tetap menunggu dan muncul begitu ia kembali.
        if (away || now < _drinkDue) return;

        _drinkDue = now.AddMinutes(interval);
        if (Settings.DrinkCountToday >= DrinkGoalGlasses) return;

        Raise(new WellnessNotificationEvent
        {
            Kind = WellnessReminderKind.Drink,
            Title = "Waktunya minum",
            Message = $"Segelas air ({DrinkGlassMl} ml). Hari ini {Settings.DrinkCountToday}/{DrinkGoalGlasses} gelas.",
            PrimaryActionText = "Sudah minum"
        });
    }

    public void LogDrink()
    {
        ResetCounterIfNewDay();
        Settings.DrinkCountToday++;
        _settingsService.Save();
        _drinkDue = _now().AddMinutes(Math.Max(1, Settings.DrinkReminderMinutes));
        StateChanged?.Invoke();
    }

    public void ResetDrinkCount()
    {
        Settings.DrinkCountToday = 0;
        Settings.DrinkCountDate = _now().ToString("yyyy-MM-dd");
        _settingsService.Save();
        StateChanged?.Invoke();
    }

    public void MarkMoved()
    {
        _moveDue = _now().AddMinutes(Math.Max(1, Settings.MoveReminderMinutes));
    }

    public void Snooze(WellnessReminderKind kind, int minutes)
    {
        var due = _now().AddMinutes(Math.Max(1, minutes));
        if (kind == WellnessReminderKind.Move) _moveDue = due; else _drinkDue = due;
    }

    private void ResetCounterIfNewDay(bool notify = true)
    {
        var today = _now().ToString("yyyy-MM-dd");
        if (Settings.DrinkCountDate == today) return;

        Settings.DrinkCountDate = today;
        Settings.DrinkCountToday = 0;
        _settingsService.Save();
        if (notify) StateChanged?.Invoke();
    }

    public bool IsWithinWorkHours(DateTime now)
    {
        var start = ParseTime(Settings.ReminderWorkStart, new TimeSpan(8, 0, 0));
        var end = ParseTime(Settings.ReminderWorkEnd, new TimeSpan(17, 0, 0));
        var t = now.TimeOfDay;
        return start <= end ? t >= start && t < end : t >= start || t < end;
    }

    private static TimeSpan ParseTime(string? text, TimeSpan fallback)
        => TimeSpan.TryParse(text, out var value) ? value : fallback;

    private void Raise(WellnessNotificationEvent evt) => ReminderTriggered?.Invoke(evt);

    public void Dispose() => Stop();
}
