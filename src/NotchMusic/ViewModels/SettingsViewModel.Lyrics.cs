using System.Windows.Input;

namespace NotchMusic.ViewModels;

public partial class SettingsViewModel
{
    public bool AutoSearchLyrics
    {
        get => Settings.AutoSearchLyrics;
        set
        {
            if (Settings.AutoSearchLyrics != value)
            {
                Settings.AutoSearchLyrics = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public double LyricsFontSize
    {
        get => Settings.LyricsFontSize;
        set
        {
            if (Math.Abs(Settings.LyricsFontSize - value) > 0.1)
            {
                Settings.LyricsFontSize = Math.Clamp(value, 12, 36);
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public double LyricsOpacity
    {
        get => Settings.LyricsOpacity;
        set
        {
            if (Math.Abs(Settings.LyricsOpacity - value) > 0.01)
            {
                Settings.LyricsOpacity = Math.Clamp(value, 0.2, 1.0);
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public string ToggleLyricsHotkey
    {
        get => Settings.ToggleLyricsHotkey;
        set
        {
            if (Settings.ToggleLyricsHotkey != value)
            {
                Settings.ToggleLyricsHotkey = value;
                _settingsService.Save();
                OnPropertyChanged();
                _onHotkeyUpdated?.Invoke(value);
            }
        }
    }

    public ICommand ClearCacheCommand { get; }
}
