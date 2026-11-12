using TamaNotch.Services.Taskbar;

namespace TamaNotch.ViewModels;

public partial class SettingsViewModel
{
    public bool LaunchTranslucentTb
    {
        get => Settings.LaunchTranslucentTb;
        set
        {
            SetSetting(Settings.LaunchTranslucentTb, value, v => Settings.LaunchTranslucentTb = v, nameof(LaunchTranslucentTb));
            if (value) TranslucentTbLauncher.EnsureRunning();
            OnPropertyChanged(nameof(TranslucentTbStatus));
        }
    }

    public bool IsTranslucentTbInstalled => TranslucentTbLauncher.IsInstalled;

    public string TranslucentTbStatus => !TranslucentTbLauncher.IsInstalled
        ? "TranslucentTB belum terpasang. Pasang dengan: winget install --id 9PF4KZ2VN4W9 (Microsoft Store) atau unduh dari github.com/TranslucentTB/TranslucentTB."
        : TranslucentTbLauncher.IsRunning()
            ? "TranslucentTB terdeteksi dan sedang berjalan."
            : "TranslucentTB terpasang tetapi belum berjalan.";
}
