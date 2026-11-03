using System.Windows.Input;
using NotchMusic.Helpers;
using NotchMusic.Models;

namespace NotchMusic.ViewModels;

public partial class SettingsViewModel
{
    // Pilihan Suara Adzan & Kontrol Pemutar
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
                OnPropertyChanged(nameof(SelectedAdhanDescription));
                OnPropertyChanged(nameof(IsCustomAdhanSelected));
            }
        }
    }

    public string SelectedAdhanDescription
    {
        get
        {
            var opt = AvailableAdhanSounds.FirstOrDefault(s => string.Equals(s.Id, SelectedAdhanSound, StringComparison.OrdinalIgnoreCase));
            return opt?.Description ?? string.Empty;
        }
    }

    public bool IsCustomAdhanSelected => string.Equals(SelectedAdhanSound, "custom", StringComparison.OrdinalIgnoreCase);

    public string CustomAdhanSoundPath
    {
        get => Settings.CustomAdhanSoundPath;
        set
        {
            if (Settings.CustomAdhanSoundPath != value)
            {
                Settings.CustomAdhanSoundPath = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public double AdhanVolume
    {
        get => Settings.AdhanVolume;
        set
        {
            if (Math.Abs(Settings.AdhanVolume - value) > 0.001)
            {
                Settings.AdhanVolume = Math.Clamp(value, 0.0, 1.0);
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdhanVolumePercentText));
            }
        }
    }

    public string AdhanVolumePercentText => $"{(int)Math.Round(AdhanVolume * 100)}%";

    private bool _isAdhanPlaying;

    public bool IsAdhanPlaying
    {
        get => _isAdhanPlaying;
        set
        {
            if (SetProperty(ref _isAdhanPlaying, value))
            {
                OnPropertyChanged(nameof(AdhanPlayButtonText));
                OnPropertyChanged(nameof(AdhanPlayButtonIcon));
                OnPropertyChanged(nameof(AdhanPlayPauseButtonIcon));
                OnPropertyChanged(nameof(AdhanPlaybackStatusDetail));
            }
        }
    }

    public string AdhanPlayButtonText => IsAdhanPlaying ? "Berhenti" : "Tes Suara Adzan";

    public string AdhanPlayButtonIcon => IsAdhanPlaying ? "⏹" : "▶";

    private bool _isAdhanPaused;

    public bool IsAdhanPaused
    {
        get => _isAdhanPaused;
        set
        {
            if (SetProperty(ref _isAdhanPaused, value))
            {
                OnPropertyChanged(nameof(AdhanPlayPauseButtonIcon));
                OnPropertyChanged(nameof(AdhanPlayPauseButtonTooltip));
                OnPropertyChanged(nameof(AdhanPlaybackStatusDetail));
            }
        }
    }

    public string AdhanPlayPauseButtonIcon => (!IsAdhanPlaying || IsAdhanPaused) ? "▶" : "⏸";

    public string AdhanPlayPauseButtonTooltip => IsAdhanPlaying && !IsAdhanPaused ? "Jeda (Pause)" : "Putar / Lanjutkan";

    private double _adhanCurrentPositionSeconds;

    public double AdhanCurrentPositionSeconds
    {
        get => _adhanCurrentPositionSeconds;
        set
        {
            if (Math.Abs(_adhanCurrentPositionSeconds - value) > 0.5)
            {
                _adhanCurrentPositionSeconds = value;
                PrayerAudioHelper.SeekTo(value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdhanCurrentPositionText));
                OnPropertyChanged(nameof(AdhanPlaybackStatusDetail));
            }
        }
    }

    private double _adhanTotalDurationSeconds;

    public double AdhanTotalDurationSeconds
    {
        get => _adhanTotalDurationSeconds > 0 ? _adhanTotalDurationSeconds : 180;
        set
        {
            if (SetProperty(ref _adhanTotalDurationSeconds, value))
            {
                OnPropertyChanged(nameof(AdhanTotalDurationText));
                OnPropertyChanged(nameof(AdhanPlaybackStatusDetail));
            }
        }
    }

    public string AdhanCurrentPositionText => TimeSpan.FromSeconds(Math.Max(0, _adhanCurrentPositionSeconds)).ToString(@"mm\:ss");

    public string AdhanTotalDurationText => TimeSpan.FromSeconds(Math.Max(0, _adhanTotalDurationSeconds)).ToString(@"mm\:ss");

    public string AdhanActiveRangeText
    {
        get
        {
            if (!AdhanCustomDurationEnabled)
            {
                return "Rentang Suara: Penuh (00:00 s/d selesai)";
            }
            return $"Rentang Suara: Dari {Settings.AdhanStartMinutes:D2}:{Settings.AdhanStartSeconds:D2} s/d {Settings.AdhanEndMinutes:D2}:{Settings.AdhanEndSeconds:D2}";
        }
    }

    public string AdhanPlaybackStatusDetail
    {
        get
        {
            if (!IsAdhanPlaying && !IsAdhanPaused)
            {
                return "Tekan tombol ▶ Tes Putar di bawah untuk memantau posisi detik audio secara langsung.";
            }

            string pos = AdhanCurrentPositionText;
            string total = AdhanTotalDurationText;

            if (IsAdhanPaused)
            {
                return $"⏸ Suara dijeda pada detik {pos} (Total file: {total}).";
            }

            if (AdhanCustomDurationEnabled)
            {
                return $"🔊 Suara keluar: {pos} | Batas selesai otomatis di {Settings.AdhanEndMinutes:D2}:{Settings.AdhanEndSeconds:D2} (Total: {total})";
            }

            return $"🔊 Suara keluar: {pos} / {total} (Mode Penuh)";
        }
    }

    public bool IsAdhanControlVisible => true;

    // Pengaturan Durasi Adzan Pendek
    public bool AdhanCustomDurationEnabled
    {
        get => Settings.AdhanCustomDurationEnabled;
        set
        {
            if (Settings.AdhanCustomDurationEnabled != value)
            {
                Settings.AdhanCustomDurationEnabled = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdhanDurationSummaryText));
                OnPropertyChanged(nameof(AdhanActiveRangeText));
                OnPropertyChanged(nameof(AdhanPlaybackStatusDetail));
            }
        }
    }

    public int AdhanStartMinutes
    {
        get => Settings.AdhanStartMinutes;
        set
        {
            int val = Math.Clamp(value, 0, 59);
            if (Settings.AdhanStartMinutes != val)
            {
                Settings.AdhanStartMinutes = val;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdhanDurationSummaryText));
                OnPropertyChanged(nameof(AdhanActiveRangeText));
                OnPropertyChanged(nameof(AdhanPlaybackStatusDetail));
            }
        }
    }

    public int AdhanStartSeconds
    {
        get => Settings.AdhanStartSeconds;
        set
        {
            int val = Math.Clamp(value, 0, 59);
            if (Settings.AdhanStartSeconds != val)
            {
                Settings.AdhanStartSeconds = val;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdhanDurationSummaryText));
                OnPropertyChanged(nameof(AdhanActiveRangeText));
                OnPropertyChanged(nameof(AdhanPlaybackStatusDetail));
            }
        }
    }

    public int AdhanEndMinutes
    {
        get => Settings.AdhanEndMinutes;
        set
        {
            int val = Math.Clamp(value, 0, 59);
            if (Settings.AdhanEndMinutes != val)
            {
                Settings.AdhanEndMinutes = val;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdhanDurationSummaryText));
                OnPropertyChanged(nameof(AdhanActiveRangeText));
                OnPropertyChanged(nameof(AdhanPlaybackStatusDetail));
            }
        }
    }

    public int AdhanEndSeconds
    {
        get => Settings.AdhanEndSeconds;
        set
        {
            int val = Math.Clamp(value, 0, 59);
            if (Settings.AdhanEndSeconds != val)
            {
                Settings.AdhanEndSeconds = val;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(AdhanDurationSummaryText));
                OnPropertyChanged(nameof(AdhanActiveRangeText));
                OnPropertyChanged(nameof(AdhanPlaybackStatusDetail));
            }
        }
    }

    public string AdhanDurationSummaryText
    {
        get
        {
            if (!AdhanCustomDurationEnabled)
            {
                return "Lantunan adzan diputar penuh dari awal hingga selesai.";
            }

            int startSec = Settings.AdhanStartMinutes * 60 + Settings.AdhanStartSeconds;
            int endSec = Settings.AdhanEndMinutes * 60 + Settings.AdhanEndSeconds;
            int diffSec = Math.Max(0, endSec - startSec);

            int diffMin = diffSec / 60;
            int diffRemainingSec = diffSec % 60;
            string durationStr = diffMin > 0
                ? $"{diffMin} menit {diffRemainingSec} detik"
                : $"{diffRemainingSec} detik";

            return $"Diputar pendek dari {Settings.AdhanStartMinutes:D2}:{Settings.AdhanStartSeconds:D2} sampai {Settings.AdhanEndMinutes:D2}:{Settings.AdhanEndSeconds:D2} (durasi: {durationStr}).";
        }
    }

    public ICommand TestPrayerChimeCommand { get; }

    public ICommand PlayAdhanPreviewCommand { get; }

    public ICommand StopAdhanPreviewCommand { get; }

    public ICommand ToggleAdhanPlayPauseCommand { get; }

    public ICommand SetCurrentAsStartCommand { get; }

    public ICommand SetCurrentAsEndCommand { get; }

    public ICommand BrowseCustomAdhanCommand { get; }

    public ICommand SetAdhanPresetFullCommand { get; }

    public ICommand SetAdhanPreset30sCommand { get; }

    public ICommand SetAdhanPreset1mCommand { get; }

    public ICommand SetAdhanPreset2mCommand { get; }

    private void OnPrayerAudioProgressChanged(double currentSec, double totalSec)
    {
        _adhanCurrentPositionSeconds = currentSec;
        _adhanTotalDurationSeconds = totalSec;
        OnPropertyChanged(nameof(AdhanCurrentPositionSeconds));
        OnPropertyChanged(nameof(AdhanTotalDurationSeconds));
        OnPropertyChanged(nameof(AdhanCurrentPositionText));
        OnPropertyChanged(nameof(AdhanTotalDurationText));
        OnPropertyChanged(nameof(AdhanPlaybackStatusDetail));
    }

    private void OnPrayerAudioStateChanged()
    {
        _isAdhanPlaying = PrayerAudioHelper.IsPlaying;
        _isAdhanPaused = PrayerAudioHelper.IsPaused;
        OnPropertyChanged(nameof(IsAdhanPlaying));
        OnPropertyChanged(nameof(IsAdhanPaused));
        OnPropertyChanged(nameof(AdhanPlayButtonText));
        OnPropertyChanged(nameof(AdhanPlayButtonIcon));
        OnPropertyChanged(nameof(AdhanPlayPauseButtonIcon));
        OnPropertyChanged(nameof(AdhanPlayPauseButtonTooltip));
        OnPropertyChanged(nameof(AdhanPlaybackStatusDetail));
    }
}
