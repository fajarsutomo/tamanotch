using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Input;
using TamaNotch.Helpers;
using TamaNotch.Models;
using TamaNotch.Services.Quran;

namespace TamaNotch.ViewModels;

public partial class MainViewModel
{
    private IQuranService _quranService = null!;

    private ICollectionView _filteredQuranSurahs = null!;

    private string _quranSearchQuery = string.Empty;

    private QuranPlaybackState _lastQuranState = QuranPlaybackState.Stopped;

    // Pemutar musik bisa masih melaporkan "sedang main" sesaat setelah dijeda; abaikan sinyal itu sebentar.
    private DateTime _ignoreMusicStartUntilUtc = DateTime.MinValue;

    public event Action? RequestOpenQuran;

    /// <summary>Al-Qur'an mulai diputar; pemutar web (YouTube Music, Spotify, Jellyfin) perlu dijeda.</summary>
    public event Action? PlaybackStartedOnQuran;

    public IQuranService QuranService => _quranService;

    public ICollectionView FilteredQuranSurahs => _filteredQuranSurahs;

    public ICommand OpenQuranCommand { get; private set; } = null!;
    public ICommand CloseQuranWindowCommand { get; private set; } = null!;
    public ICommand MinimizeQuranWindowCommand { get; private set; } = null!;
    public ICommand ExpandQuranWindowCommand { get; private set; } = null!;
    public ICommand ToggleQuranPlayPauseCommand { get; private set; } = null!;
    public ICommand StopQuranCommand { get; private set; } = null!;
    public ICommand NextQuranCommand { get; private set; } = null!;
    public ICommand PreviousQuranCommand { get; private set; } = null!;
    public ICommand RandomQuranCommand { get; private set; } = null!;
    public ICommand PlayQuranSurahCommand { get; private set; } = null!;
    public ICommand ToggleQuranMuteCommand { get; private set; } = null!;
    public ICommand CycleQuranAfterPlaybackCommand { get; private set; } = null!;

    private void InitializeQuran(IQuranService? quranService)
    {
        _quranService = quranService ?? new QuranService(_settingsService);
        _filteredQuranSurahs = CollectionViewSource.GetDefaultView(_quranService.Surahs.ToList());
        _filteredQuranSurahs.Filter = FilterQuranSurah;

        _quranService.StateChanged += OnQuranStateChanged;
        _quranService.ProgressChanged += OnQuranProgressChanged;

        OpenQuranCommand = new RelayCommand(() =>
        {
            IsQuranWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenQuran?.Invoke();
        });
        CloseQuranWindowCommand = new RelayCommand(() =>
        {
            IsQuranWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        MinimizeQuranWindowCommand = new RelayCommand(() => IsQuranWindowCompact = true);
        ExpandQuranWindowCommand = new RelayCommand(() => IsQuranWindowCompact = false);
        ToggleQuranPlayPauseCommand = new RelayCommand(() => _quranService.TogglePlayPause());
        StopQuranCommand = new RelayCommand(() => _quranService.Stop());
        NextQuranCommand = new RelayCommand(() => _quranService.Next());
        PreviousQuranCommand = new RelayCommand(() => _quranService.Previous());
        RandomQuranCommand = new RelayCommand(() => _quranService.PlayRandom());
        PlayQuranSurahCommand = new RelayCommand(obj =>
        {
            if (obj is QuranSurah surah) _quranService.PlaySurah(surah);
        });
        ToggleQuranMuteCommand = new RelayCommand(() => IsQuranMuted = !IsQuranMuted);
        CycleQuranAfterPlaybackCommand = new RelayCommand(() =>
        {
            _quranService.AfterPlayback = _quranService.AfterPlayback switch
            {
                QuranAfterPlayback.Stop => QuranAfterPlayback.Next,
                QuranAfterPlayback.Next => QuranAfterPlayback.Random,
                _ => QuranAfterPlayback.Stop
            };
        });
    }

    public bool IsQuranWindowOpen
    {
        get => Settings.IsQuranWindowOpen;
        set
        {
            if (Settings.IsQuranWindowOpen != value)
            {
                Settings.IsQuranWindowOpen = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsQuranWindowOpen));
                OnPropertyChanged(nameof(IsNotchActive));
                RequestComponentLayoutChanged?.Invoke();
            }
        }
    }

    public bool IsQuranWindowCompact
    {
        get => Settings.IsQuranWindowCompact;
        set
        {
            if (Settings.IsQuranWindowCompact != value)
            {
                Settings.IsQuranWindowCompact = value;
                _settingsService.Save();
                OnPropertyChanged(nameof(IsQuranWindowCompact));
                OnPropertyChanged(nameof(IsQuranWindowExpanded));
            }
        }
    }

    public bool IsQuranWindowExpanded => !IsQuranWindowCompact;

    public QuranSurah? CurrentQuranSurah => _quranService.CurrentSurah;

    public string CurrentQuranTitle => _quranService.CurrentSurah?.Title ?? "Pilih Surah";

    public string CurrentQuranArabic => _quranService.CurrentSurah?.NameArabic ?? string.Empty;

    public string CurrentQuranSubtitle => _quranService.CurrentSurah?.Subtitle ?? "Murottal Al-Qur'an";

    public bool IsQuranPlaying => _quranService.PlaybackState == QuranPlaybackState.Playing;

    public bool IsQuranLoading => _quranService.PlaybackState == QuranPlaybackState.Loading;

    /// <summary>Sedang memuat atau memutar: dipakai untuk saling jeda dengan pemutar lain.</summary>
    public bool IsQuranActive => IsQuranPlaying || IsQuranLoading;

    public string QuranStatusMessage => _quranService.StatusMessage;

    public string QuranStateBadgeText => _quranService.PlaybackState switch
    {
        QuranPlaybackState.Playing => "MEMUTAR",
        QuranPlaybackState.Loading => "MEMUAT",
        QuranPlaybackState.Paused => "JEDA",
        QuranPlaybackState.Error => "GAGAL",
        _ => "SIAP"
    };

    public string QuranNotchTooltip => _quranService.CurrentSurah != null
        ? $"Al-Qur'an: {_quranService.CurrentSurah.Title} ({QuranStateBadgeText})\nKlik untuk membuka daftar surah & pemutar Al-Qur'an"
        : "Buka pemutar Al-Qur'an (pilih surah atau surah acak)";

    public double QuranProgress
    {
        get
        {
            var duration = _quranService.Duration;
            return duration > TimeSpan.Zero
                ? Math.Clamp(_quranService.Position.TotalSeconds / duration.TotalSeconds, 0.0, 1.0)
                : 0.0;
        }
    }

    public string QuranPositionText => FormatQuranTime(_quranService.Position);

    public string QuranDurationText => FormatQuranTime(_quranService.Duration);

    public static string FormatQuranTime(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        return time.TotalHours >= 1
            ? time.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
            : time.ToString(@"m\:ss", CultureInfo.InvariantCulture);
    }

    public double QuranVolume
    {
        get => _quranService.Volume;
        set
        {
            _quranService.Volume = value;
            if (value > 0 && _quranService.IsMuted) _quranService.IsMuted = false;
            OnPropertyChanged(nameof(QuranVolume));
            OnPropertyChanged(nameof(QuranVolumePercentText));
            OnPropertyChanged(nameof(QuranVolumeIcon));
        }
    }

    public bool IsQuranMuted
    {
        get => _quranService.IsMuted;
        set
        {
            _quranService.IsMuted = value;
            OnPropertyChanged(nameof(IsQuranMuted));
            OnPropertyChanged(nameof(QuranVolumePercentText));
            OnPropertyChanged(nameof(QuranVolumeIcon));
        }
    }

    public string QuranVolumePercentText => IsQuranMuted ? "Mute" : $"{Math.Round(QuranVolume * 100)}%";

    public string QuranVolumeIcon => IsQuranMuted || QuranVolume <= 0.01 ? "🔇" : (QuranVolume < 0.5 ? "🔉" : "🔊");

    public string QuranAfterPlaybackText => _quranService.AfterPlayback switch
    {
        QuranAfterPlayback.Stop => "Setelah selesai: berhenti",
        QuranAfterPlayback.Random => "Setelah selesai: surah acak",
        _ => "Setelah selesai: surah berikutnya"
    };

    public string QuranSearchQuery
    {
        get => _quranSearchQuery;
        set
        {
            if (SetProperty(ref _quranSearchQuery, value ?? string.Empty))
            {
                _filteredQuranSurahs.Refresh();
            }
        }
    }

    /// <summary>Huruf kecil tanpa tanda baca dan spasi, agar "al baqarah" cocok dengan "Al-Baqarah".</summary>
    public static string NormalizeQuranText(string text)
        => new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    private bool FilterQuranSurah(object obj)
    {
        if (obj is not QuranSurah surah) return false;

        var query = _quranSearchQuery.Trim();
        if (query.Length == 0) return true;

        if (int.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            return surah.Number == number;

        var q = NormalizeQuranText(query);
        if (q.Length == 0) return true;

        return NormalizeQuranText(surah.NameSimple).Contains(q)
            || NormalizeQuranText(surah.TranslatedName).Contains(q)
            || surah.NameArabic.Contains(query, StringComparison.Ordinal);
    }

    private void OnQuranStateChanged()
    {
        Action notify = () =>
        {
            var state = _quranService.PlaybackState;
            bool wasActive = _lastQuranState is QuranPlaybackState.Loading or QuranPlaybackState.Playing;
            bool isActive = state is QuranPlaybackState.Loading or QuranPlaybackState.Playing;
            _lastQuranState = state;

            // Mulai (atau lanjut dari jeda): pemutar lain harus diam.
            if (isActive && !wasActive) PauseOthersForQuran();

            OnPropertyChanged(nameof(CurrentQuranSurah));
            OnPropertyChanged(nameof(CurrentQuranTitle));
            OnPropertyChanged(nameof(CurrentQuranArabic));
            OnPropertyChanged(nameof(CurrentQuranSubtitle));
            OnPropertyChanged(nameof(IsQuranPlaying));
            OnPropertyChanged(nameof(IsQuranLoading));
            OnPropertyChanged(nameof(IsQuranActive));
            OnPropertyChanged(nameof(IsAudioActive));
            OnPropertyChanged(nameof(IsNotchActive));
            OnPropertyChanged(nameof(QuranStatusMessage));
            OnPropertyChanged(nameof(QuranStateBadgeText));
            OnPropertyChanged(nameof(QuranNotchTooltip));
            OnPropertyChanged(nameof(QuranVolume));
            OnPropertyChanged(nameof(IsQuranMuted));
            OnPropertyChanged(nameof(QuranVolumePercentText));
            OnPropertyChanged(nameof(QuranVolumeIcon));
            OnPropertyChanged(nameof(QuranAfterPlaybackText));
        };

        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess()) d.BeginInvoke(notify);
        else notify();
    }

    private void OnQuranProgressChanged()
    {
        OnPropertyChanged(nameof(QuranProgress));
        OnPropertyChanged(nameof(QuranPositionText));
        OnPropertyChanged(nameof(QuranDurationText));
    }

    /// <summary>Geser posisi putar ke bagian tertentu (0..1) dari durasi surah.</summary>
    public void SeekQuran(double fraction) => _quranService.SeekToFraction(fraction);

    private void PauseOthersForQuran()
    {
        _ignoreMusicStartUntilUtc = DateTime.UtcNow.AddSeconds(3);

        if (IsPlaying) _ = _mediaService.PauseAsync();
        _ = _mediaService.PauseSessionByKeywordAsync("spotify");
        if (IsRadioPlaying || IsRadioBuffering) _radioService.Stop();
        if (IsLiveTvPlaying || IsLiveTvBuffering) _liveTvService.Stop();

        PlaybackStartedOnQuran?.Invoke();
    }

    /// <summary>Pemutar lain (musik, YouTube Music, Spotify, Jellyfin, radio, Live TV, adzan) mulai: Al-Qur'an berhenti.</summary>
    private void StopQuranForOtherPlayback()
    {
        if (IsQuranActive) _quranService.Stop();
    }

    private void StopQuranWhenMusicStarts()
    {
        if (DateTime.UtcNow < _ignoreMusicStartUntilUtc) return;
        StopQuranForOtherPlayback();
    }
}
