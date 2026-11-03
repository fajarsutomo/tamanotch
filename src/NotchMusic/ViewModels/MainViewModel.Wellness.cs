using System.Windows.Input;
using System.Windows.Threading;
using NotchMusic.Models;
using NotchMusic.Services.Wellness;

namespace NotchMusic.ViewModels;

public partial class MainViewModel
{
    private readonly IWellnessReminderService _wellnessService;

    private WellnessNotificationEvent? _activeWellnessNotification;

    private DispatcherTimer? _wellnessDismissTimer;

    public IWellnessReminderService WellnessService => _wellnessService;

    public bool HasActiveWellnessNotification => _activeWellnessNotification != null;

    public string WellnessAccentColor => _activeWellnessNotification?.Kind == WellnessReminderKind.Drink ? "#4FC3F7" : "#7BE495";

    public WellnessNotificationEvent? ActiveWellnessNotification
    {
        get => _activeWellnessNotification;
        private set
        {
            if (!SetProperty(ref _activeWellnessNotification, value)) return;

            OnPropertyChanged(nameof(HasActiveWellnessNotification));
            OnPropertyChanged(nameof(WellnessAccentColor));
            _wellnessDismissTimer?.Stop();
            if (value == null) return;

            _wellnessDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(40) };
            _wellnessDismissTimer.Tick += (s, e) =>
            {
                _wellnessDismissTimer?.Stop();
                ActiveWellnessNotification = null;
            };
            _wellnessDismissTimer.Start();
        }
    }

    private void OnWellnessReminder(WellnessNotificationEvent evt)
    {
        ActiveWellnessNotification = evt;
        if (Settings.ReminderSoundEnabled)
        {
            try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
        }
    }

    public ICommand WellnessPrimaryActionCommand { get; }

    public ICommand WellnessSnoozeCommand { get; }

    public ICommand DismissWellnessNotificationCommand { get; }
}
