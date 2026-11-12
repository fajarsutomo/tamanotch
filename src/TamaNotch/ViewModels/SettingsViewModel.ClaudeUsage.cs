namespace TamaNotch.ViewModels;

public partial class SettingsViewModel
{
    public IReadOnlyList<int> ClaudeUsageIntervalOptions { get; } = new[] { 2, 3, 5, 10, 15, 30 };

    public bool EnableClaudeUsage
    {
        get => Settings.EnableClaudeUsage;
        set => SetSetting(Settings.EnableClaudeUsage, value, v => Settings.EnableClaudeUsage = v, nameof(EnableClaudeUsage));
    }

    public int ClaudeUsageRefreshMinutes
    {
        get => Settings.ClaudeUsageRefreshMinutes;
        set => SetSetting(Settings.ClaudeUsageRefreshMinutes, Math.Clamp(value, 2, 30),
            v => Settings.ClaudeUsageRefreshMinutes = v, nameof(ClaudeUsageRefreshMinutes));
    }

    public bool ShowClaudeUsageInIsland
    {
        get => Settings.ShowClaudeUsageInIsland;
        set => SetSetting(Settings.ShowClaudeUsageInIsland, value, v => Settings.ShowClaudeUsageInIsland = v, nameof(ShowClaudeUsageInIsland));
    }

    public bool NotifyClaudeUsage
    {
        get => Settings.NotifyClaudeUsage;
        set => SetSetting(Settings.NotifyClaudeUsage, value, v => Settings.NotifyClaudeUsage = v, nameof(NotifyClaudeUsage));
    }
}