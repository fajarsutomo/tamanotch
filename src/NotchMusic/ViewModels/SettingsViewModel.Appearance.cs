using System.Windows.Input;
using NotchMusic.Helpers;

namespace NotchMusic.ViewModels;

public partial class SettingsViewModel
{
    public bool StartWithWindows
    {
        get => Settings.StartWithWindows;
        set
        {
            if (Settings.StartWithWindows != value)
            {
                _settingsService.SetStartWithWindows(value);
                OnPropertyChanged();
            }
        }
    }

    public bool AlwaysOnTop
    {
        get => Settings.AlwaysOnTop;
        set
        {
            if (Settings.AlwaysOnTop != value)
            {
                Settings.AlwaysOnTop = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool ExpandOnHover
    {
        get => Settings.ExpandOnHover;
        set
        {
            if (Settings.ExpandOnHover != value)
            {
                Settings.ExpandOnHover = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public string ComponentDisplayMode
    {
        get => Settings.ComponentDisplayMode ?? "Both";
        set
        {
            if (Settings.ComponentDisplayMode != value)
            {
                Settings.ComponentDisplayMode = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsComponentBoth));
                OnPropertyChanged(nameof(IsComponentMusicOnly));
                OnPropertyChanged(nameof(IsComponentTodoOnly));
            }
        }
    }

    public bool IsComponentBoth
    {
        get => ComponentDisplayMode == "Both";
        set
        {
            if (value) ComponentDisplayMode = "Both";
        }
    }

    public bool IsComponentMusicOnly
    {
        get => ComponentDisplayMode == "MusicOnly";
        set
        {
            if (value) ComponentDisplayMode = "MusicOnly";
        }
    }

    public bool IsComponentTodoOnly
    {
        get => ComponentDisplayMode == "TodoOnly";
        set
        {
            if (value) ComponentDisplayMode = "TodoOnly";
        }
    }

    public bool IsTodoDetached
    {
        get => Settings.IsTodoDetached;
        set
        {
            if (Settings.IsTodoDetached != value)
            {
                Settings.IsTodoDetached = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public double NotchHorizontalOffsetPercent
    {
        get => Settings.NotchHorizontalOffsetPercent;
        set
        {
            double clamped = Math.Clamp(Math.Round(value), -WindowHelper.MaxHorizontalOffsetPercent, WindowHelper.MaxHorizontalOffsetPercent);
            if (Math.Abs(Settings.NotchHorizontalOffsetPercent - clamped) > 0.01)
            {
                Settings.NotchHorizontalOffsetPercent = clamped;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(NotchHorizontalOffsetText));
            }
        }
    }

    public string NotchHorizontalOffsetText => Math.Abs(NotchHorizontalOffsetPercent) < 0.5
        ? "Tengah"
        : $"{Math.Abs(NotchHorizontalOffsetPercent):0}% ke {(NotchHorizontalOffsetPercent < 0 ? "kiri" : "kanan")}";

    public ICommand ResetNotchPositionCommand { get; }

    public double IdleIconSize
    {
        get => Settings.IdleIconSize;
        set
        {
            double clamped = Math.Clamp(Math.Round(value), 24, 48);
            if (Math.Abs(Settings.IdleIconSize - clamped) > 0.01)
            {
                Settings.IdleIconSize = clamped;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IdleIconSizeText));
            }
        }
    }

    public string IdleIconSizeText => $"{IdleIconSize:0} px";

    public string ToggleVisibilityHotkey
    {
        get => Settings.ToggleVisibilityHotkey;
        set
        {
            if (Settings.ToggleVisibilityHotkey != value)
            {
                Settings.ToggleVisibilityHotkey = value;
                _settingsService.Save();
                OnPropertyChanged();
                _onHotkeyUpdated?.Invoke(value);
            }
        }
    }

    public ICommand ExitApplicationCommand { get; }
}
