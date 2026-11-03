using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Sync;
using NotchMusic.Services.Update;

namespace NotchMusic.ViewModels;

public partial class MainViewModel
{
    private readonly IGoogleDriveSyncService _googleDriveSyncService;

    private readonly IUpdateService _updateService;

    // Google Drive Sync Properties
    public IGoogleDriveSyncService GoogleDriveSyncService => _googleDriveSyncService;

    public IUpdateService UpdateService => _updateService;

    private bool _isCheckingOrInstallingUpdate;

    private string _updateActionStatusText = string.Empty;

    public string UpdateActionStatusText
    {
        get => _updateActionStatusText;
        private set
        {
            if (SetProperty(ref _updateActionStatusText, value))
            {
                OnPropertyChanged(nameof(VersionButtonText));
                OnPropertyChanged(nameof(VersionButtonTooltip));
            }
        }
    }

    public string CurrentVersionText => $"v{_updateService.CurrentVersion}";

    public string VersionButtonText => !string.IsNullOrWhiteSpace(_updateActionStatusText)
        ? _updateActionStatusText
        : (HasAvailableUpdate && !string.IsNullOrWhiteSpace(AvailableUpdateVersion)
            ? $"Click to Update {(AvailableUpdateVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? AvailableUpdateVersion : "v" + AvailableUpdateVersion)}"
            : CurrentVersionText);

    public string VersionButtonTooltip => HasAvailableUpdate && !string.IsNullOrWhiteSpace(AvailableUpdateVersion)
        ? $"Pembaruan v{AvailableUpdateVersion} tersedia! (Saat ini: {CurrentVersionText}) — Klik untuk langsung download & install"
        : $"Versi Aplikasi TAMANOTCH {CurrentVersionText} — Klik untuk langsung cek, download, dan install pembaruan";

    private bool _hasAvailableUpdate;

    public bool HasAvailableUpdate
    {
        get => _hasAvailableUpdate;
        set
        {
            if (SetProperty(ref _hasAvailableUpdate, value))
            {
                OnPropertyChanged(nameof(VersionButtonText));
                OnPropertyChanged(nameof(VersionButtonTooltip));
            }
        }
    }

    private string _availableUpdateVersion = string.Empty;

    public string AvailableUpdateVersion
    {
        get => _availableUpdateVersion;
        set
        {
            if (SetProperty(ref _availableUpdateVersion, value))
            {
                OnPropertyChanged(nameof(VersionButtonText));
                OnPropertyChanged(nameof(VersionButtonTooltip));
            }
        }
    }

    private UpdateInfo? _availableUpdateInfo;

    public UpdateInfo? AvailableUpdateInfo
    {
        get => _availableUpdateInfo;
        set => SetProperty(ref _availableUpdateInfo, value);
    }

    public ICommand OpenUpdateCommand { get; }

    public bool IsGoogleDriveConnected => _googleDriveSyncService.IsConnected;

    public bool IsGoogleDriveSyncing => _googleDriveSyncService.IsSyncing;

    public string GoogleDriveUserEmail => _googleDriveSyncService.UserEmail;

    public string GoogleDriveSyncStatus => _googleDriveSyncService.SyncStatus;

    public string GoogleDriveStatusButtonText
    {
        get
        {
            if (IsGoogleDriveSyncing) return "Menyinkronkan...";
            if (IsGoogleDriveConnected) return "Drive";
            return "Drive: Belum Konek";
        }
    }

    public string GoogleDriveSyncTooltip => _googleDriveSyncService.IsConnected
        ? $"Google Drive ({_googleDriveSyncService.UserEmail}): {_googleDriveSyncService.SyncStatus}\nKlik untuk sinkronkan sekarang"
        : "Google Drive belum terhubung.\nKlik untuk menghubungkan dan login akun Google";

    public ICommand SyncGoogleDriveCommand { get; }

    private DispatcherTimer? _cloudSyncPollingTimer;

    private void StartCloudSyncPollingTimer()
    {
        if (System.Windows.Application.Current?.Dispatcher == null) return;

        _cloudSyncPollingTimer?.Stop();
        _cloudSyncPollingTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _cloudSyncPollingTimer.Tick += async (s, e) =>
        {
            try
            {
                if (_googleDriveSyncService.IsConnected && Settings.GoogleDriveAutoSync && !_googleDriveSyncService.IsSyncing)
                {
                    await _googleDriveSyncService.CheckForRemoteChangesAsync();
                }
            }
            catch { }
        };
        _cloudSyncPollingTimer.Start();
    }

    public async Task CheckAndInstallUpdateAsync()
    {
        if (_isCheckingOrInstallingUpdate) return;
        _isCheckingOrInstallingUpdate = true;

        try
        {
            if (!HasAvailableUpdate || AvailableUpdateInfo == null)
            {
                UpdateActionStatusText = "Mengecek...";
                var update = await _updateService.CheckForUpdatesAsync();
                if (update != null && update.IsNewerVersion)
                {
                    AvailableUpdateInfo = update;
                    AvailableUpdateVersion = $"v{update.Version.TrimStart('v', 'V')}";
                    HasAvailableUpdate = true;
                }
                else if (update != null)
                {
                    UpdateActionStatusText = "Sudah terbaru ✓";
                    RequestShowTrayNotification?.Invoke("Pembaruan TAMANOTCH", $"Aplikasi sudah menggunakan versi terbaru ({CurrentVersionText}).");
                    await Task.Delay(2200);
                    UpdateActionStatusText = string.Empty;
                    return;
                }
                else
                {
                    UpdateActionStatusText = "Gagal cek";
                    await Task.Delay(2200);
                    UpdateActionStatusText = string.Empty;
                    return;
                }
            }

            if (AvailableUpdateInfo != null)
            {
                if (!string.IsNullOrWhiteSpace(AvailableUpdateInfo.DownloadUrl))
                {
                    UpdateActionStatusText = "Unduh 0%";
                    var progress = new Progress<double>(pct =>
                    {
                        UpdateActionStatusText = $"Unduh {pct:0}%";
                    });
                    var installerPath = await _updateService.DownloadUpdateAsync(AvailableUpdateInfo.DownloadUrl, progress);
                    UpdateActionStatusText = "Menginstal...";
                    await Task.Delay(400);
                    _updateService.LaunchInstallerAndExit(installerPath);
                }
                else if (!string.IsNullOrWhiteSpace(AvailableUpdateInfo.ReleasePageUrl))
                {
                    UpdateActionStatusText = string.Empty;
                    try
                    {
                        Process.Start(new ProcessStartInfo(AvailableUpdateInfo.ReleasePageUrl) { UseShellExecute = true });
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            UpdateActionStatusText = "Gagal update";
            RequestShowTrayNotification?.Invoke("Pembaruan TAMANOTCH", $"Gagal mengunduh pembaruan: {ex.Message}");
            await Task.Delay(2200);
            UpdateActionStatusText = string.Empty;
        }
        finally
        {
            _isCheckingOrInstallingUpdate = false;
        }
    }

    private void OnTimelineUpdated(object? sender, TimeSpan position)
    {
        App.Current?.Dispatcher.Invoke(() =>
        {
            _baseTrackPosition = position;
            _playbackStopwatch.Restart();
            UpdateTimelineAndLyrics();
        });
    }

    private void UpdateTimelineAndLyrics()
    {
        if (CurrentTrack == null) return;

        TimeSpan currentPos;
        if (CurrentTrack.IsPlaying)
        {
            var elapsed = _playbackStopwatch.Elapsed * CurrentTrack.PlaybackRate;
            currentPos = _baseTrackPosition + elapsed;
            if (CurrentTrack.Duration > TimeSpan.Zero && currentPos > CurrentTrack.Duration)
            {
                currentPos = CurrentTrack.Duration;
            }
        }
        else
        {
            currentPos = _baseTrackPosition;
        }

        CurrentPositionSeconds = currentPos.TotalSeconds;
        DurationSeconds = CurrentTrack.Duration.TotalSeconds;

        var posStr = FormatTimeSpan(currentPos);
        var durStr = FormatTimeSpan(CurrentTrack.Duration);
        FormattedTime = $"{posStr} / {durStr}";

        // Update Lyrics
        if (CurrentLyrics == null)
        {
            ActiveLyric = "Memuat lirik...";
            PreviousLyric = string.Empty;
            NextLyric = string.Empty;
            return;
        }

        if (!CurrentLyrics.IsSynced)
        {
            ActiveLyric = CurrentLyrics.PlainLyrics;
            PreviousLyric = string.Empty;
            NextLyric = string.Empty;
            return;
        }

        var effectivePos = currentPos + TimeSpan.FromSeconds(ManualOffsetSeconds);
        var idx = LrcParser.GetActiveLineIndex(CurrentLyrics.Lines, effectivePos);

        if (idx != _activeLyricIndex)
        {
            _activeLyricIndex = idx;
            if (idx >= 0 && idx < CurrentLyrics.Lines.Count)
            {
                ActiveLyric = CurrentLyrics.Lines[idx].Text;
                PreviousLyric = idx > 0 ? CurrentLyrics.Lines[idx - 1].Text : string.Empty;
                NextLyric = idx < CurrentLyrics.Lines.Count - 1 ? CurrentLyrics.Lines[idx + 1].Text : string.Empty;
            }
            else
            {
                ActiveLyric = "♪ ♪ ♪";
                PreviousLyric = string.Empty;
                NextLyric = CurrentLyrics.Lines.Count > 0 ? CurrentLyrics.Lines[0].Text : string.Empty;
            }
        }
    }

    private readonly object _autoSyncLock = new();

    private bool _isAutoSyncRunning = false;

    private bool _hasPendingAutoSync = false;

    private void ScheduleAutoSync()
    {
        if (!_googleDriveSyncService.IsConnected || !Settings.GoogleDriveAutoSync) return;

        lock (_autoSyncLock)
        {
            if (_isAutoSyncRunning)
            {
                _hasPendingAutoSync = true;
                return;
            }
            _isAutoSyncRunning = true;
            _hasPendingAutoSync = false;
        }

        _ = Task.Run(async () =>
        {
            while (true)
            {
                List<TodoItem> snapshot;
                if (System.Windows.Application.Current?.Dispatcher != null &&
                    !System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    snapshot = await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                        () => Todos.Select(t => t.ToModel()).ToList());
                }
                else
                {
                    snapshot = Todos.Select(t => t.ToModel()).ToList();
                }

                try
                {
                    if (_googleDriveSyncService.IsConnected && Settings.GoogleDriveAutoSync)
                    {
                        await _googleDriveSyncService.UploadTodosAsync(snapshot);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"AutoSync error: {ex.Message}");
                }

                lock (_autoSyncLock)
                {
                    if (_hasPendingAutoSync)
                    {
                        _hasPendingAutoSync = false;
                        continue;
                    }
                    _isAutoSyncRunning = false;
                    break;
                }
            }
        });
    }

    public async Task HandleGoogleDriveClickAsync()
    {
        if (!_googleDriveSyncService.IsConnected)
        {
            try
            {
                RequestShowTrayNotification?.Invoke("Google Drive", "Membuka browser untuk login akun Google...");
                var success = await _googleDriveSyncService.AuthenticateAsync();
                if (success)
                {
                    RequestShowTrayNotification?.Invoke("Google Drive", $"Berhasil terhubung sebagai {_googleDriveSyncService.UserEmail}!");
                    await SyncWithGoogleDriveAsync();
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        $"Tidak dapat login ke Google Drive:\n{_googleDriveSyncService.SyncStatus}",
                        "TAMANOTCH - Google Drive",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Gagal otentikasi Google Drive:\n{ex.Message}",
                    "TAMANOTCH - Google Drive Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        else
        {
            await SyncWithGoogleDriveAsync();
        }
    }

    public async Task SyncWithGoogleDriveAsync()
    {
        if (!_googleDriveSyncService.IsConnected) return;

        var deletedIds = _todoService.GetDeletedIds();
        var merged = await _googleDriveSyncService.SyncAsync(Todos.Select(t => t.ToModel()), deletedIds);
        if (merged != null)
        {
            OnTodosSyncedFromCloud(merged);
        }
    }
}
