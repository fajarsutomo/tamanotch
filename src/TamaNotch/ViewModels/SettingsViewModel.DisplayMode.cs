namespace TamaNotch.ViewModels;

public sealed record DisplayModeChoice(string Id, string Label);

/// <summary>Pilihan "tampil di jendela sendiri" atau "tertanam di island" untuk Radio, Al-Qur'an, Sholat, Claude.</summary>
public partial class SettingsViewModel
{
    public IReadOnlyList<DisplayModeChoice> DisplayModeChoices { get; } = new[]
    {
        new DisplayModeChoice(MainViewModel.DisplayWindow, "Jendela sendiri"),
        new DisplayModeChoice(MainViewModel.DisplayIsland, "Tertanam di island")
    };

    private static string NormalizeDisplayMode(string? mode)
        => MainViewModel.IsIslandMode(mode) ? MainViewModel.DisplayIsland : MainViewModel.DisplayWindow;

    public string RadioDisplayMode
    {
        get => NormalizeDisplayMode(Settings.RadioDisplayMode);
        set => SetSetting(NormalizeDisplayMode(Settings.RadioDisplayMode), NormalizeDisplayMode(value), v => Settings.RadioDisplayMode = v, nameof(RadioDisplayMode));
    }

    public string QuranDisplayMode
    {
        get => NormalizeDisplayMode(Settings.QuranDisplayMode);
        set => SetSetting(NormalizeDisplayMode(Settings.QuranDisplayMode), NormalizeDisplayMode(value), v => Settings.QuranDisplayMode = v, nameof(QuranDisplayMode));
    }

    public string PrayerDisplayMode
    {
        get => NormalizeDisplayMode(Settings.PrayerDisplayMode);
        set => SetSetting(NormalizeDisplayMode(Settings.PrayerDisplayMode), NormalizeDisplayMode(value), v => Settings.PrayerDisplayMode = v, nameof(PrayerDisplayMode));
    }

    public string ClaudeUsageDisplayMode
    {
        get => NormalizeDisplayMode(Settings.ClaudeUsageDisplayMode);
        set => SetSetting(NormalizeDisplayMode(Settings.ClaudeUsageDisplayMode), NormalizeDisplayMode(value), v => Settings.ClaudeUsageDisplayMode = v, nameof(ClaudeUsageDisplayMode));
    }
}
