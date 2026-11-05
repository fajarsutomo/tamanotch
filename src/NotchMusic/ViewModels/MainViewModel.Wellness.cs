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

    // Minum = biru, gerak = pink (notch notifikasi dan banner memakai warna yang sama).
    private const string DrinkColorHex = "#4FC3F7";
    private const string MoveColorHex = "#FF7EB6";

    public string WellnessAccentColor => _activeWellnessNotification?.Kind == WellnessReminderKind.Drink ? DrinkColorHex : MoveColorHex;

    private static readonly System.Windows.Media.Brush DrinkFrameBrush = NotchMusic.Helpers.NeonPalette.Freeze(
        new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(DrinkColorHex)));

    private static readonly System.Windows.Media.Brush MoveFrameBrush = NotchMusic.Helpers.NeonPalette.Freeze(
        new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(MoveColorHex)));

    private static readonly System.Windows.Media.Effects.Effect DrinkGlow = CreateGlow(DrinkColorHex);

    private static readonly System.Windows.Media.Effects.Effect MoveGlow = CreateGlow(MoveColorHex);

    private static System.Windows.Media.Effects.Effect CreateGlow(string hex)
    {
        var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
        var effect = new System.Windows.Media.Effects.DropShadowEffect { Color = color, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.9 };
        effect.Freeze();
        return effect;
    }

    private bool IsDrinkReminder => _activeWellnessNotification?.Kind == WellnessReminderKind.Drink;

    public System.Windows.Media.Brush WellnessFrameBrush => IsDrinkReminder ? DrinkFrameBrush : MoveFrameBrush;

    public System.Windows.Media.Effects.Effect WellnessGlowEffect => IsDrinkReminder ? DrinkGlow : MoveGlow;

    // Prioritas notch: peringatan BMKG berat, sholat, lalu pengingat kesehatan.
    public bool ShowWellnessNotch => HasActiveWellnessNotification && IsCompactMode && ShowIdleEyes && !ShowAlertNotch && !ShowPrayerNotch;

    public bool ShowWellnessBanner => HasActiveWellnessNotification && !ShowWellnessNotch && !WellnessShownInNotch;

    public string WellnessNotchText
    {
        get
        {
            var n = _activeWellnessNotification;
            return n == null ? string.Empty : $"{n.Title} • {n.Message}";
        }
    }

    public string WellnessNotchTooltip
    {
        get
        {
            var n = _activeWellnessNotification;
            return n == null
                ? string.Empty
                : $"{n.Title}\n{n.Message}\nKlik: {n.PrimaryActionText} • Klik kanan: tunda 10 menit atau tutup";
        }
    }

    public WellnessNotificationEvent? ActiveWellnessNotification
    {
        get => _activeWellnessNotification;
        private set
        {
            if (!SetProperty(ref _activeWellnessNotification, value)) return;

            OnPropertyChanged(nameof(HasActiveWellnessNotification));
            OnPropertyChanged(nameof(WellnessAccentColor));
            OnPropertyChanged(nameof(WellnessFrameBrush));
            OnPropertyChanged(nameof(WellnessGlowEffect));
            OnPropertyChanged(nameof(WellnessNotchTooltip));
            // Notifikasi menahan island terbuka saat banner-nya tampil; setelah hilang, kembali ke idle bila kursor sudah pergi.
            if (value == null) ScheduleHoverCollapse();
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

    /// <summary>Tampilkan pengingat minum/gerak di notch notifikasi (atau banner island bila notch tidak tampil).</summary>
    public void ShowWellnessNotification(WellnessNotificationEvent evt) => ActiveWellnessNotification = evt;

    private void OnWellnessReminder(WellnessNotificationEvent evt)
    {
        ShowWellnessNotification(evt);
        if (Settings.ReminderSoundEnabled)
        {
            try { System.Media.SystemSounds.Asterisk.Play(); } catch { }
        }
    }

    public ICommand WellnessPrimaryActionCommand { get; }

    public ICommand WellnessSnoozeCommand { get; }

    public ICommand DismissWellnessNotificationCommand { get; }
}
