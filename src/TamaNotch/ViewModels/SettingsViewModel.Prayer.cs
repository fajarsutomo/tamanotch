using TamaNotch.Models;
using TamaNotch.Services.Prayer;

namespace TamaNotch.ViewModels;

public partial class SettingsViewModel
{
    private readonly IPrayerService _prayerService;

    // Waktu Sholat & Pengingat Properties
    public bool EnablePrayerTimes
    {
        get => Settings.EnablePrayerTimes;
        set
        {
            if (Settings.EnablePrayerTimes != value)
            {
                Settings.EnablePrayerTimes = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TodayPrayerSchedule));
            }
        }
    }

    public bool RemindBeforePrayer
    {
        get => Settings.RemindBeforePrayer;
        set
        {
            if (Settings.RemindBeforePrayer != value)
            {
                Settings.RemindBeforePrayer = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public int RemindBeforeMinutes
    {
        get => Settings.RemindBeforeMinutes;
        set
        {
            if (Settings.RemindBeforeMinutes != value)
            {
                Settings.RemindBeforeMinutes = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public List<int> AvailableMinutesOptions { get; } = new() { 5, 10, 15, 20, 30 };

    public bool RemindAtPrayerTime
    {
        get => Settings.RemindAtPrayerTime;
        set
        {
            if (Settings.RemindAtPrayerTime != value)
            {
                Settings.RemindAtPrayerTime = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool RemindAfterPrayer
    {
        get => Settings.RemindAfterPrayer;
        set
        {
            if (Settings.RemindAfterPrayer != value)
            {
                Settings.RemindAfterPrayer = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public int RemindAfterMinutes
    {
        get => Settings.RemindAfterMinutes;
        set
        {
            if (Settings.RemindAfterMinutes != value)
            {
                Settings.RemindAfterMinutes = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool PrayerSoundAlert
    {
        get => Settings.PrayerSoundAlert;
        set
        {
            if (Settings.PrayerSoundAlert != value)
            {
                Settings.PrayerSoundAlert = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool PrayerAutoPauseMusic
    {
        get => Settings.PrayerAutoPauseMusic;
        set
        {
            if (Settings.PrayerAutoPauseMusic != value)
            {
                Settings.PrayerAutoPauseMusic = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool ShowPrayerInCompactNotch
    {
        get => Settings.ShowPrayerInCompactNotch;
        set
        {
            if (Settings.ShowPrayerInCompactNotch != value)
            {
                Settings.ShowPrayerInCompactNotch = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool IsPrayerDetached
    {
        get => Settings.IsPrayerDetached;
        set
        {
            if (Settings.IsPrayerDetached != value)
            {
                Settings.IsPrayerDetached = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public PrayerSchedule TodayPrayerSchedule => _prayerService.GetTodaySchedule();

    private string _testNotificationStatus = string.Empty;

    public string TestNotificationStatus
    {
        get => _testNotificationStatus;
        set => SetProperty(ref _testNotificationStatus, value);
    }
}
