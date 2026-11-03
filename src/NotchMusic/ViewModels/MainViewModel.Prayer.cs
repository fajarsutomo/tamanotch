using System.Windows.Input;
using System.Windows.Threading;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Prayer;

namespace NotchMusic.ViewModels;

public partial class MainViewModel
{
    private readonly IPrayerService _prayerService;

    private readonly IPrayerReminderService _prayerReminderService;

    private PrayerNotificationEvent? _activePrayerNotification;

    private bool _isPrayerPanelOpen;

    public bool IsPrayerDetached
    {
        get => Settings.IsPrayerDetached;
        set
        {
            if (Settings.IsPrayerDetached != value)
            {
                Settings.IsPrayerDetached = value;
                _settingsService.Save();
                RefreshComponentLayout();
            }
        }
    }

    public bool IsPrayerWindowOpen
    {
        get => Settings.IsPrayerWindowOpen;
        set
        {
            if (Settings.IsPrayerWindowOpen != value)
            {
                Settings.IsPrayerWindowOpen = value;
                _settingsService.Save();
                RefreshComponentLayout();
            }
        }
    }

    public bool IsPrayerClosed => IsPrayerDetached && !IsPrayerWindowOpen;

    public bool IsPrayerWindowCompact
    {
        get => Settings.IsPrayerWindowCompact;
        set
        {
            if (Settings.IsPrayerWindowCompact != value)
            {
                Settings.IsPrayerWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsPrayerWindowCompact));
                OnPropertyChanged(nameof(IsPrayerWindowExpanded));
            }
        }
    }

    public bool IsPrayerWindowExpanded => !IsPrayerWindowCompact;

    public bool ShowPrayerDivider1 => ShowMusicSection && ShowCompactPrayerPill;

    public bool ShowPrayerDivider2 => ShowTodoTicker && (ShowMusicSection || ShowCompactPrayerPill);

    public bool ShowPrayerSectionInMainWindow => EnablePrayerTimes && !IsPrayerDetached && IsPrayerWindowOpen;

    public bool ShowExpandedPrayerDivider1 => ShowMusicSection && ShowPrayerSectionInMainWindow;

    public bool ShowExpandedPrayerDivider2 => ShowTodoSectionInMainWindow && (ShowMusicSection || ShowPrayerSectionInMainWindow);

    public System.Windows.GridLength PrayerColumnWidth => ShowPrayerSectionInMainWindow ? new System.Windows.GridLength(285) : new System.Windows.GridLength(0);

    public bool IsPrayerPanelOpen
    {
        get => _isPrayerPanelOpen;
        set
        {
            if (SetProperty(ref _isPrayerPanelOpen, value))
            {
                OnPropertyChanged(nameof(ShowNormalExpandedContent));
                OnPropertyChanged(nameof(ShowPrayerExpandedContent));
                OnPropertyChanged(nameof(ExpandedPanelWidth));
            }
        }
    }

    public bool ShowPrayerExpandedContent => IsPrayerPanelOpen;

    // Prayer Times & Reminders Properties
    public IPrayerService PrayerService => _prayerService;

    public IPrayerReminderService PrayerReminderService => _prayerReminderService;

    public PrayerSchedule CurrentPrayerSchedule => _prayerReminderService.CurrentSchedule;

    public bool EnablePrayerTimes => Settings.EnablePrayerTimes;

    public bool ShowCompactPrayerPill => Settings.EnablePrayerTimes && Settings.ShowPrayerInCompactNotch && !IsPrayerDetached && IsPrayerWindowOpen && CurrentPrayerSchedule?.NextPrayer != null;

    public string NextPrayerName => CurrentPrayerSchedule?.NextPrayer?.Name ?? string.Empty;

    public string NextPrayerTimeFormatted => CurrentPrayerSchedule?.NextPrayer?.FormattedTime ?? string.Empty;

    public string NextPrayerCountdownBadge => CurrentPrayerSchedule?.NextPrayer?.CountdownText ?? string.Empty;

    public string PrayerScheduleTooltip => CurrentPrayerSchedule != null
        ? $"Jadwal Sholat ({CurrentPrayerSchedule.CityName}): {CurrentPrayerSchedule.CountdownSummary}\nKlik untuk melihat jadwal lengkap hari ini"
        : "Waktu Sholat";

    public IReadOnlyList<AdhanSoundOption> AvailableAdhanSounds => PrayerAudioHelper.AvailableSounds;

    public string SelectedAdhanSound
    {
        get => Settings.SelectedAdhanSound;
        set
        {
            if (Settings.SelectedAdhanSound != value)
            {
                Settings.SelectedAdhanSound = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedAdhanSoundName));
            }
        }
    }

    public string SelectedAdhanSoundName
    {
        get
        {
            string name;
            if (string.Equals(SelectedAdhanSound, "custom", StringComparison.OrdinalIgnoreCase))
            {
                string customName = !string.IsNullOrWhiteSpace(Settings.CustomAdhanSoundPath)
                    ? System.IO.Path.GetFileName(Settings.CustomAdhanSoundPath)
                    : "File Audio Kustom";
                name = $"Kustom: {customName}";
            }
            else
            {
                var opt = AvailableAdhanSounds.FirstOrDefault(s => string.Equals(s.Id, SelectedAdhanSound, StringComparison.OrdinalIgnoreCase));
                name = opt?.Name ?? "Adzan Mekkah (Masjidil Haram)";
            }

            if (Settings.AdhanCustomDurationEnabled)
            {
                name += $" (⏱ {Settings.AdhanStartMinutes:D2}:{Settings.AdhanStartSeconds:D2} - {Settings.AdhanEndMinutes:D2}:{Settings.AdhanEndSeconds:D2})";
            }
            return name;
        }
    }

    private bool _isAdhanPlaying;

    public bool IsAdhanPlaying
    {
        get => _isAdhanPlaying;
        set
        {
            if (_isAdhanPlaying != value)
            {
                _isAdhanPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdhanPlayButtonIcon));
                OnPropertyChanged(nameof(AdhanPlayButtonTooltip));
                OnPropertyChanged(nameof(AdhanPlayPauseButtonIcon));
            }
        }
    }

    private bool _isAdhanPaused;

    public bool IsAdhanPaused
    {
        get => _isAdhanPaused;
        set
        {
            if (SetProperty(ref _isAdhanPaused, value))
            {
                OnPropertyChanged(nameof(AdhanPlayPauseButtonIcon));
            }
        }
    }

    public string AdhanPlayButtonIcon => IsAdhanPlaying ? "⏹" : "▶";

    public string AdhanPlayButtonTooltip => IsAdhanPlaying ? "Berhenti putar suara" : "Tes putar suara adzan";

    public string AdhanPlayPauseButtonIcon => (!IsAdhanPlaying || IsAdhanPaused) ? "▶" : "⏸";

    private double _adhanCurrentSec;

    private double _adhanTotalSec;

    public double AdhanCurrentPositionSeconds
    {
        get => _adhanCurrentSec;
        set
        {
            if (Math.Abs(_adhanCurrentSec - value) > 0.5)
            {
                _adhanCurrentSec = value;
                PrayerAudioHelper.SeekTo(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdhanProgressText));
                OnPropertyChanged(nameof(AdhanProgressPercent));
            }
        }
    }

    public double AdhanTotalDurationSeconds => _adhanTotalSec > 0 ? _adhanTotalSec : 180;

    public string AdhanCurrentPositionText => TimeSpan.FromSeconds(Math.Max(0, _adhanCurrentSec)).ToString(@"mm\:ss");

    public string AdhanTotalDurationText => TimeSpan.FromSeconds(Math.Max(0, _adhanTotalSec)).ToString(@"mm\:ss");

    public string AdhanProgressText
    {
        get
        {
            string cur = TimeSpan.FromSeconds(Math.Max(0, _adhanCurrentSec)).ToString(@"mm\:ss");
            if (Settings.AdhanCustomDurationEnabled && Settings.GetAdhanEndTotalSeconds() > 0)
            {
                string end = $"{Settings.AdhanEndMinutes:D2}:{Settings.AdhanEndSeconds:D2}";
                return $"{cur} / {end}";
            }
            string tot = TimeSpan.FromSeconds(Math.Max(0, _adhanTotalSec)).ToString(@"mm\:ss");
            return $"{cur} / {tot}";
        }
    }

    public double AdhanProgressPercent
    {
        get
        {
            double target = (Settings.AdhanCustomDurationEnabled && Settings.GetAdhanEndTotalSeconds() > 0)
                ? Settings.GetAdhanEndTotalSeconds()
                : _adhanTotalSec;
            return target > 0 ? Math.Clamp((_adhanCurrentSec / target) * 100.0, 0, 100) : 0;
        }
        set { /* Read-only calculated property; setter provided to prevent WPF TwoWay binding crash */ }
    }

    public bool HasActivePrayerNotification => EnablePrayerTimes && _activePrayerNotification != null;

    public PrayerNotificationEvent? ActivePrayerNotification
    {
        get => _activePrayerNotification;
        private set
        {
            if (SetProperty(ref _activePrayerNotification, value))
            {
                OnPropertyChanged(nameof(HasActivePrayerNotification));
            }
        }
    }

    public ICommand DetachPrayerCommand { get; }

    public ICommand AttachPrayerCommand { get; }

    public ICommand ClosePrayerCommand { get; }

    public ICommand MinimizePrayerWindowCommand { get; }

    public ICommand ExpandPrayerWindowCommand { get; }

public ICommand TogglePrayerPanelCommand { get; }

    public ICommand PlayAdhanPreviewCommand { get; }

    public ICommand StopAdhanPreviewCommand { get; }

    public ICommand ToggleAdhanPlayPauseCommand { get; }

    public ICommand OpenPrayerScheduleCommand { get; }

    public ICommand DismissPrayerNotificationCommand { get; }

    public void RefreshAdhanSettings()
    {
        OnPropertyChanged(nameof(SelectedAdhanSound));
        OnPropertyChanged(nameof(SelectedAdhanSoundName));
        OnPropertyChanged(nameof(AdhanPlayButtonIcon));
        OnPropertyChanged(nameof(AdhanPlayButtonTooltip));
        OnPropertyChanged(nameof(AdhanPlayPauseButtonIcon));
    }

    private void OnPrayerReminderTriggered(PrayerNotificationEvent evt)
    {
        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
        {
            ActivePrayerNotification = evt;
            RequestShowTrayNotification?.Invoke(evt.Title, evt.Message);

            if (evt.AutoPauseRequested)
            {
                if (IsPlaying)
                {
                    _ = _mediaService.PauseAsync();
                }
                if (IsRadioPlaying || IsRadioBuffering)
                {
                    _radioService.Stop();
                }
                if (IsLiveTvPlaying || IsLiveTvBuffering)
                {
                    _liveTvService.Stop();
                }
            }

            // Auto dismiss notification banner after 20 seconds
            _notificationDismissTimer?.Stop();
            _notificationDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _notificationDismissTimer.Tick += (s, e) =>
            {
                _notificationDismissTimer?.Stop();
                ActivePrayerNotification = null;
            };
            _notificationDismissTimer.Start();
        });
    }
}
