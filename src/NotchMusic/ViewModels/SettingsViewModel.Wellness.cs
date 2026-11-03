using System.Windows.Input;
using NotchMusic.Services.Wellness;

namespace NotchMusic.ViewModels;

public partial class SettingsViewModel
{
    private readonly IWellnessReminderService _wellnessService;

    // --- Pengingat kesehatan (gerak & minum) ---
    public IReadOnlyList<int> MoveIntervalOptions { get; } = new[] { 20, 30, 45, 60, 90 };

    public IReadOnlyList<int> DrinkIntervalOptions { get; } = new[] { 30, 45, 60, 90, 120 };

    public IReadOnlyList<int> GlassSizeOptions { get; } = new[] { 150, 200, 230, 250, 300, 330, 500 };

    public IReadOnlyList<int> GlassGoalOptions { get; } = new[] { 4, 5, 6, 7, 8, 9, 10, 12 };

    public IReadOnlyList<string> WorkTimeOptions { get; } =
        Enumerable.Range(5, 19).Select(h => $"{h:00}:00").ToArray();

    private void SetSetting<T>(T current, T value, Action<T> apply, params string[] extra)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return;
        apply(value);
        _settingsService.Save();
        foreach (var name in extra) OnPropertyChanged(name);
    }

    public bool MoveReminderEnabled
    {
        get => Settings.MoveReminderEnabled;
        set { SetSetting(Settings.MoveReminderEnabled, value, v => Settings.MoveReminderEnabled = v, nameof(MoveReminderEnabled)); }
    }

    public int MoveReminderMinutes
    {
        get => Settings.MoveReminderMinutes;
        set { SetSetting(Settings.MoveReminderMinutes, value, v => Settings.MoveReminderMinutes = v, nameof(MoveReminderMinutes)); }
    }

    public bool DrinkReminderEnabled
    {
        get => Settings.DrinkReminderEnabled;
        set { SetSetting(Settings.DrinkReminderEnabled, value, v => Settings.DrinkReminderEnabled = v, nameof(DrinkReminderEnabled)); }
    }

    public int DrinkReminderMinutes
    {
        get => Settings.DrinkReminderMinutes;
        set { SetSetting(Settings.DrinkReminderMinutes, value, v => Settings.DrinkReminderMinutes = v, nameof(DrinkReminderMinutes)); }
    }

    public int DrinkGlassMl
    {
        get => Settings.DrinkGlassMl;
        set { SetSetting(Settings.DrinkGlassMl, value, v => Settings.DrinkGlassMl = v, nameof(DrinkGlassMl), nameof(DrinkProgressText)); }
    }

    public int DrinkDailyGoalGlasses
    {
        get => Settings.DrinkDailyGoalGlasses;
        set { SetSetting(Settings.DrinkDailyGoalGlasses, value, v => Settings.DrinkDailyGoalGlasses = v, nameof(DrinkDailyGoalGlasses), nameof(DrinkProgressText)); }
    }

    public string ReminderWorkStart
    {
        get => Settings.ReminderWorkStart;
        set { SetSetting(Settings.ReminderWorkStart, value, v => Settings.ReminderWorkStart = v, nameof(ReminderWorkStart)); }
    }

    public string ReminderWorkEnd
    {
        get => Settings.ReminderWorkEnd;
        set { SetSetting(Settings.ReminderWorkEnd, value, v => Settings.ReminderWorkEnd = v, nameof(ReminderWorkEnd)); }
    }

    public bool ReminderSoundEnabled
    {
        get => Settings.ReminderSoundEnabled;
        set { SetSetting(Settings.ReminderSoundEnabled, value, v => Settings.ReminderSoundEnabled = v, nameof(ReminderSoundEnabled)); }
    }

    public string DrinkProgressText
    {
        get
        {
            int count = _wellnessService.DrinkCountToday;
            return $"Hari ini: {count}/{Math.Max(1, Settings.DrinkDailyGoalGlasses)} gelas ({count * Math.Max(50, Settings.DrinkGlassMl)} ml)";
        }
    }

    public ICommand LogDrinkCommand { get; }

    public ICommand ResetDrinkCountCommand { get; }
}
