namespace TamaNotch.ViewModels;

public partial class SettingsViewModel
{
    public IReadOnlyList<int> WelcomeBackIdleOptions { get; } = new[] { 15, 30, 60, 90, 120, 180 };

    public IReadOnlyList<int> FastingSahurOptions { get; } = new[] { 15, 30, 45, 60 };

    public string UserDisplayName
    {
        get => Settings.UserDisplayName;
        set => SetSetting(Settings.UserDisplayName, (value ?? string.Empty).Trim(),
            v => Settings.UserDisplayName = v, nameof(UserDisplayName));
    }

    public bool EnableWelcomeBack
    {
        get => Settings.EnableWelcomeBack;
        set => SetSetting(Settings.EnableWelcomeBack, value, v => Settings.EnableWelcomeBack = v, nameof(EnableWelcomeBack));
    }

    public int WelcomeBackIdleMinutes
    {
        get => Settings.WelcomeBackIdleMinutes;
        set => SetSetting(Settings.WelcomeBackIdleMinutes, Math.Clamp(value, 5, 600),
            v => Settings.WelcomeBackIdleMinutes = v, nameof(WelcomeBackIdleMinutes));
    }

    public bool EnableFasting
    {
        get => Settings.EnableFasting;
        set => SetSetting(Settings.EnableFasting, value, v => Settings.EnableFasting = v, nameof(EnableFasting));
    }

    public bool FastingMondayThursdayOnly
    {
        get => Settings.FastingMondayThursdayOnly;
        set => SetSetting(Settings.FastingMondayThursdayOnly, value,
            v => Settings.FastingMondayThursdayOnly = v, nameof(FastingMondayThursdayOnly));
    }

    public int FastingSahurMinutes
    {
        get => Settings.FastingSahurMinutes;
        set => SetSetting(Settings.FastingSahurMinutes, Math.Clamp(value, 5, 120),
            v => Settings.FastingSahurMinutes = v, nameof(FastingSahurMinutes));
    }
}
