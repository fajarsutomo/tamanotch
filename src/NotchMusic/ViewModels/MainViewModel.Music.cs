using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.Media;

namespace NotchMusic.ViewModels;

public partial class MainViewModel
{
    private readonly IMediaSessionService _mediaService;

    private readonly ILyricsService _lyricsService;

    private readonly DispatcherTimer _interpolationTimer;

    private TrackInfo? _currentTrack;

    private SongLyrics? _currentLyrics;

    private int _activeLyricIndex = -1;

    private double _currentPositionSeconds;

    private double _durationSeconds;

    private string _formattedTime = "0:00 / 0:00";

    private string _previousLyric = string.Empty;

    private string _activeLyric = string.Empty;

    private string _nextLyric = string.Empty;

    private string _sessionStatus = "Menunggu pemutar...";

    private double _manualOffsetSeconds = 0;

    private string _searchQuery = string.Empty;

    private bool _isSearching;

    private bool _isVocalRemoverEnabled;

    private List<LrcLibSearchResult> _searchResults = new();

    private CancellationTokenSource? _lyricsCts;

    private bool _isLocalLyricsLoaded;

    private Stopwatch _playbackStopwatch = new();

    private TimeSpan _baseTrackPosition = TimeSpan.Zero;

    public event Action? RequestOpenYouTubeMusic;

    public event Action? RequestOpenSpotify;

    public event Action? PlaybackStartedOnSpotify;

    public event Action? PlaybackStartedOnYouTubeMusic;

    public bool IsLyricsMode => CurrentMode == PanelMode.Lyrics;

    public bool HasTrackThumbnail => CurrentTrack?.Thumbnail != null;

    public TrackInfo? CurrentTrack
    {
        get => _currentTrack;
        private set
        {
            if (SetProperty(ref _currentTrack, value))
            {
                OnPropertyChanged(nameof(StandbyLyric));
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(ShowMusicInNotch));
                OnPropertyChanged(nameof(ShowMusicPillInNotch));
                OnPropertyChanged(nameof(ShowMusicIconInNotch));
                OnPropertyChanged(nameof(IsAudioActive));
                OnPropertyChanged(nameof(IsNotchActive));
                OnPropertyChanged(nameof(HasTrack));
                OnPropertyChanged(nameof(HasTrackThumbnail));
                OnPropertyChanged(nameof(MusicNotchTooltip));
            }
        }
    }

    public bool HasTrack => CurrentTrack != null && !string.IsNullOrWhiteSpace(CurrentTrack.Title);

    public bool IsPlaying => CurrentTrack?.IsPlaying ?? false;

    public bool IsAudioActive => IsPlaying || IsRadioPlaying || IsLiveTvPlaying || IsQuranPlaying;

    public SongLyrics? CurrentLyrics
    {
        get => _currentLyrics;
        private set
        {
            if (SetProperty(ref _currentLyrics, value))
            {
                OnPropertyChanged(nameof(IsLyricsSynced));
            }
        }
    }

    public bool IsLyricsSynced => CurrentLyrics?.IsSynced == true;

    public double CurrentPositionSeconds
    {
        get => _currentPositionSeconds;
        set => SetProperty(ref _currentPositionSeconds, value);
    }

    public double DurationSeconds
    {
        get => _durationSeconds;
        set => SetProperty(ref _durationSeconds, value);
    }

    public string FormattedTime
    {
        get => _formattedTime;
        set => SetProperty(ref _formattedTime, value);
    }

    public string PreviousLyric
    {
        get => _previousLyric;
        set => SetProperty(ref _previousLyric, value);
    }

    public string ActiveLyric
    {
        get => _activeLyric;
        set
        {
            if (SetProperty(ref _activeLyric, value))
            {
                OnPropertyChanged(nameof(StandbyLyric));
            }
        }
    }

    public string StandbyLyric
    {
        get
        {
            if (CurrentTrack == null)
            {
                return "Siap memutar musik...";
            }

            if (!string.IsNullOrWhiteSpace(_activeLyric))
            {
                var firstLine = _activeLyric.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstLine))
                {
                    return firstLine.Trim();
                }
            }

            return !string.IsNullOrWhiteSpace(CurrentTrack.Artist) ? CurrentTrack.Artist : "♪ Lirik belum tersedia";
        }
    }

    public string NextLyric
    {
        get => _nextLyric;
        set => SetProperty(ref _nextLyric, value);
    }

    public string SessionStatus
    {
        get => _sessionStatus;
        set => SetProperty(ref _sessionStatus, value);
    }

    public double ManualOffsetSeconds
    {
        get => _manualOffsetSeconds;
        set => SetProperty(ref _manualOffsetSeconds, value);
    }

    public string SearchQuery
    {
        get => _searchQuery;
        set => SetProperty(ref _searchQuery, value);
    }

    public bool IsSearching
    {
        get => _isSearching;
        set => SetProperty(ref _isSearching, value);
    }

    public List<LrcLibSearchResult> SearchResults
    {
        get => _searchResults;
        set => SetProperty(ref _searchResults, value);
    }

    public IReadOnlyList<MediaSessionInfo> AvailableSessions => _mediaService.AvailableSessions;

    public bool IsVocalRemoverEnabled
    {
        get => _isVocalRemoverEnabled;
        set
        {
            if (SetProperty(ref _isVocalRemoverEnabled, value))
            {
                OnPropertyChanged(nameof(VocalRemoverButtonText));
                OnPropertyChanged(nameof(VocalRemoverTooltip));
                RequestVocalRemoverChanged?.Invoke(value);
            }
        }
    }

    public string VocalRemoverButtonText => IsVocalRemoverEnabled ? "🎤 Karaoke ON" : "🎤 Karaoke";

    public string VocalRemoverTooltip => IsVocalRemoverEnabled
        ? "Penghilang Vokal (Mode Karaoke) sedang AKTIF — Klik untuk kembali ke vokal normal"
        : "Aktifkan Penghilang Vokal (Mode Karaoke) untuk meredam suara vokal penyanyi";

    // Commands
    public ICommand TogglePlayPauseCommand { get; }

    public ICommand PreviousCommand { get; }

    public ICommand NextCommand { get; }

    public ICommand SeekCommand { get; }

    public ICommand OpenYouTubeMusicCommand { get; }

    public ICommand OpenSpotifyCommand { get; }

    public ICommand ImportLyricsCommand { get; }

    public ICommand OffsetAddCommand { get; }

    public ICommand OffsetSubtractCommand { get; }

    public ICommand OffsetResetCommand { get; }

    public ICommand SearchLyricsCommand { get; }

    public ICommand SelectSearchResultCommand { get; }

    public ICommand SelectSessionCommand { get; }

    public ICommand ToggleVocalRemoverCommand { get; }

    public event Action<bool>? RequestVocalRemoverChanged;

    public void NotifyYouTubeMusicPlaying()
    {
        _ = _mediaService.PauseSessionByKeywordAsync("spotify");
        StopQuranForOtherPlayback();
        PlaybackStartedOnYouTubeMusic?.Invoke();
    }

    public void NotifySpotifyPlaying()
    {
        StopQuranForOtherPlayback();
        PlaybackStartedOnSpotify?.Invoke();
    }

    private void OnTrackChanged(object? sender, TrackInfo? track)
    {
        Action update = () =>
        {
            var oldTrackKey = _currentTrack?.UniqueSongKey;
            CurrentTrack = track;
            if (track?.IsPlaying == true) StopQuranWhenMusicStarts();

            if (track == null || string.IsNullOrWhiteSpace(track.Title))
            {
                SessionStatus = "Tidak ada lagu yang sedang diputar";
                ActiveLyric = string.Empty;
                PreviousLyric = string.Empty;
                NextLyric = string.Empty;
                DurationSeconds = 0;
                CurrentPositionSeconds = 0;
                FormattedTime = "0:00 / 0:00";
                _playbackStopwatch.Reset();
                return;
            }

            SessionStatus = $"{track.SourceAppId}";
            DurationSeconds = track.Duration.TotalSeconds;
            _baseTrackPosition = track.Position;
            _playbackStopwatch.Restart();

            if (track.UniqueSongKey != oldTrackKey)
            {
                _isLocalLyricsLoaded = false;
                _activeLyricIndex = -1;

                // Load saved offset for song
                if (Settings.SongOffsetsSeconds.TryGetValue(track.UniqueSongKey, out var savedOffset))
                {
                    ManualOffsetSeconds = savedOffset;
                }
                else
                {
                    ManualOffsetSeconds = 0;
                }

                FetchLyricsForTrack(track);
            }

            UpdateTimelineAndLyrics();
        };

        if (App.Current?.Dispatcher != null && !App.Current.Dispatcher.CheckAccess())
        {
            App.Current.Dispatcher.Invoke(update);
        }
        else
        {
            update();
        }
    }

    private void InterpolationTimer_Tick(object? sender, EventArgs e)
    {
        if (CurrentTrack == null) return;

        UpdateTimelineAndLyrics();
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
        {
            return ts.ToString(@"h\:mm\:ss");
        }
        return ts.ToString(@"m\:ss");
    }

    private void FetchLyricsForTrack(TrackInfo track)
    {
        _lyricsCts?.Cancel();
        _lyricsCts?.Dispose();
        _lyricsCts = new CancellationTokenSource();
        var token = _lyricsCts.Token;

        ActiveLyric = "Mencari lirik di LRCLIB...";
        PreviousLyric = string.Empty;
        NextLyric = string.Empty;

        if (!Settings.AutoSearchLyrics)
        {
            CurrentLyrics = SongLyrics.Empty("Pencarian otomatis dinonaktifkan di Pengaturan");
            ActiveLyric = CurrentLyrics.PlainLyrics;
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                var result = await _lyricsService.GetLyricsAsync(track.Title, track.Artist, track.Album, track.Duration, token);
                if (!token.IsCancellationRequested)
                {
                    App.Current?.Dispatcher.Invoke(() =>
                    {
                        // Check if track is still the same and user hasn't imported local lyrics in the meantime
                        if (!_isLocalLyricsLoaded && CurrentTrack?.UniqueSongKey == track.UniqueSongKey)
                        {
                            CurrentLyrics = result;
                            _activeLyricIndex = -1;
                            UpdateTimelineAndLyrics();
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                App.Current?.Dispatcher.Invoke(() =>
                {
                    if (!_isLocalLyricsLoaded && CurrentTrack?.UniqueSongKey == track.UniqueSongKey)
                    {
                        CurrentLyrics = SongLyrics.Empty($"Gagal mengambil lirik: {ex.Message}");
                        ActiveLyric = CurrentLyrics.PlainLyrics;
                    }
                });
            }
        }, token);
    }

    private void ImportLocalLyrics()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "File Lirik (*.lrc;*.txt)|*.lrc;*.txt|LRC Files (*.lrc)|*.lrc|Text Files (*.txt)|*.txt",
            Title = "Pilih File Lirik Lokal"
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _lyricsCts?.Cancel(); // Cancel any ongoing online fetch
                var loaded = _lyricsService.LoadFromLocalFile(dialog.FileName);
                _isLocalLyricsLoaded = true;
                CurrentLyrics = loaded;
                _activeLyricIndex = -1;
                UpdateTimelineAndLyrics();
            }
            catch (Exception ex)
            {
                ActiveLyric = $"Gagal membaca file: {ex.Message}";
            }
        }
    }

    private void AdjustOffset(double deltaSeconds, bool reset = false)
    {
        if (reset)
        {
            ManualOffsetSeconds = 0;
        }
        else
        {
            ManualOffsetSeconds = Math.Round(ManualOffsetSeconds + deltaSeconds, 1);
        }

        if (CurrentTrack != null && !string.IsNullOrEmpty(CurrentTrack.UniqueSongKey))
        {
            Settings.SongOffsetsSeconds[CurrentTrack.UniqueSongKey] = ManualOffsetSeconds;
            _settingsService.Save();
        }

        _activeLyricIndex = -1; // force refresh
        UpdateTimelineAndLyrics();
    }

    private async Task ExecuteManualSearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        IsSearching = true;
        try
        {
            var results = await _lyricsService.SearchCandidatesAsync(SearchQuery);
            SearchResults = results;
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void ApplySearchResult(LrcLibSearchResult item)
    {
        _lyricsCts?.Cancel();
        _isLocalLyricsLoaded = true; // Protect from auto-fetch overrides

        if (item.Instrumental == true)
        {
            CurrentLyrics = new SongLyrics
            {
                Title = item.TrackName ?? "",
                Artist = item.ArtistName ?? "",
                IsSynced = false,
                PlainLyrics = "♪ Instrumental ♪",
                Source = "LRCLIB Manual Selection"
            };
        }
        else if (!string.IsNullOrWhiteSpace(item.SyncedLyrics))
        {
            var lrc = LrcParser.Parse(item.SyncedLyrics);
            lrc.Title = item.TrackName ?? "";
            lrc.Artist = item.ArtistName ?? "";
            lrc.Source = "LRCLIB Manual (Synced)";
            CurrentLyrics = lrc;
        }
        else if (!string.IsNullOrWhiteSpace(item.PlainLyrics))
        {
            CurrentLyrics = new SongLyrics
            {
                Title = item.TrackName ?? "",
                Artist = item.ArtistName ?? "",
                IsSynced = false,
                PlainLyrics = item.PlainLyrics,
                Source = "LRCLIB Manual (Plain)"
            };
        }

        SearchResults = new List<LrcLibSearchResult>();
        _activeLyricIndex = -1;
        UpdateTimelineAndLyrics();
    }
}
