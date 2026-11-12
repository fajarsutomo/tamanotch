namespace TamaNotch.ViewModels;

public partial class SettingsViewModel
{
    public bool EnableSystemMonitor
    {
        get => Settings.EnableSystemMonitor;
        set => SetSetting(Settings.EnableSystemMonitor, value, v => Settings.EnableSystemMonitor = v, nameof(EnableSystemMonitor));
    }

    public bool ShowUptimeInIsland
    {
        get => Settings.ShowUptimeInIsland;
        set => SetSetting(Settings.ShowUptimeInIsland, value, v => Settings.ShowUptimeInIsland = v, nameof(ShowUptimeInIsland));
    }
}
