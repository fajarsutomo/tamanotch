using System.Diagnostics;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.Bmkg;
using NotchMusic.Services.Location;
using NotchMusic.Services.Prayer;
using NotchMusic.Services.Settings;
using NotchMusic.Services.Sync;
using NotchMusic.Services.Todo;
using NotchMusic.Services.Update;
using NotchMusic.Services.Wellness;

namespace NotchMusic.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    private readonly IGoogleDriveSyncService _syncService;

    private readonly Action<string> _onHotkeyUpdated;

    public AppSettings Settings => _settingsService.CurrentSettings;

    private string _statusMessage = string.Empty;

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public SettingsViewModel(
        ISettingsService settingsService,
        Action<string> onHotkeyUpdated,
        IGoogleDriveSyncService? syncService = null,
        IUpdateService? updateService = null,
        IPrayerService? prayerService = null,
        ILocationService? locationService = null,
        IBmkgService? bmkgService = null,
        IWellnessReminderService? wellnessService = null)
    {
        _settingsService = settingsService;
        _onHotkeyUpdated = onHotkeyUpdated;
        _syncService = syncService ?? new GoogleDriveSyncService(settingsService);
        _updateService = updateService ?? new UpdateService(settingsService);
        _prayerService = prayerService ?? new PrayerService(settingsService);
        _locationService = locationService ?? new LocationService(settingsService, _prayerService);
        _bmkgService = bmkgService;
        _wellnessService = wellnessService ?? new WellnessReminderService(settingsService);

        ResetNotchPositionCommand = new RelayCommand(() => NotchHorizontalOffsetPercent = 0);
        LogDrinkCommand = new RelayCommand(() =>
        {
            _wellnessService.LogDrink();
            OnPropertyChanged(nameof(DrinkProgressText));
        });
        ResetDrinkCountCommand = new RelayCommand(() =>
        {
            _wellnessService.ResetDrinkCount();
            OnPropertyChanged(nameof(DrinkProgressText));
        });

        SearchLocationCommand = new RelayCommand(async () => await SearchLocationAsync());
        SelectLocationResultCommand = new RelayCommand(res =>
        {
            if (res is BmkgLocationSearchResult result) SelectLocationResult(result);
        });
        ClearLocationSearchCommand = new RelayCommand(() =>
        {
            LocationSearchResults.Clear();
            OnPropertyChanged(nameof(HasLocationSearchResults));
        });

        DetectLocationCommand = new RelayCommand(async () =>
        {
            if (IsDetectingLocation) return;
            IsDetectingLocation = true;
            LocationDetectStatus = "Mendeteksi lokasi via IP...";

            try
            {
                var detected = await _prayerService.DetectLocationAsync();
                if (detected != null)
                {
                    var nearest = CityLocation.FindBestMatchOrNearest(detected.Name, detected.Latitude, detected.Longitude);
                    _locationService.ApplyCity(new CityLocation
                    {
                        Name = detected.Name,
                        Province = detected.Province,
                        Latitude = detected.Latitude,
                        Longitude = detected.Longitude,
                        Timezone = detected.Timezone,
                        BmkgAdm4Code = nearest.BmkgAdm4Code
                    }, LocationOrigin.Prayer);

                    OnPropertyChanged(nameof(PrayerCity));
                    OnPropertyChanged(nameof(SelectedCity));
                    OnPropertyChanged(nameof(PrayerLatitude));
                    OnPropertyChanged(nameof(PrayerLongitude));
                    OnPropertyChanged(nameof(PrayerTimezone));
                    OnPropertyChanged(nameof(TodayPrayerSchedule));
                    OnPropertyChanged(nameof(CurrentLocationText));

                    LocationDetectStatus = $"Lokasi terdeteksi: {detected.Name} ({detected.Latitude:F2}, {detected.Longitude:F2})";
                }
                else
                {
                    LocationDetectStatus = "Gagal mendeteksi lokasi otomatis. Silakan pilih kota manual.";
                }
            }
            catch (Exception ex)
            {
                LocationDetectStatus = $"Gagal mendeteksi lokasi: {ex.Message}";
            }
            finally
            {
                IsDetectingLocation = false;
            }
        });

        TestPrayerChimeCommand = new RelayCommand(() =>
        {
            PrayerAudioHelper.PlayChime();
            TestNotificationStatus = "Suara nada lonceng berbunyi ✓";
        });

        PlayAdhanPreviewCommand = new RelayCommand(() =>
        {
            if (IsAdhanPlaying)
            {
                PrayerAudioHelper.StopAudio();
                IsAdhanPlaying = false;
                TestNotificationStatus = "Suara dihentikan";
            }
            else
            {
                IsAdhanPlaying = true;
                TestNotificationStatus = "Memutar preview suara adzan...";
                double startSec = Settings.GetAdhanStartTotalSeconds();
                double endSec = Settings.GetAdhanEndTotalSeconds();
                PrayerAudioHelper.PlayPreview(SelectedAdhanSound, CustomAdhanSoundPath, AdhanVolume, startSec, endSec, () =>
                {
                    IsAdhanPlaying = false;
                });
            }
        });

        StopAdhanPreviewCommand = new RelayCommand(() =>
        {
            PrayerAudioHelper.StopAudio();
            IsAdhanPlaying = false;
            TestNotificationStatus = "Suara dihentikan";
        });

        ToggleAdhanPlayPauseCommand = new RelayCommand(() =>
        {
            if (PrayerAudioHelper.IsPlaying && !PrayerAudioHelper.IsPaused)
            {
                PrayerAudioHelper.PauseAudio();
                TestNotificationStatus = "Suara adzan dijeda (pause)";
            }
            else if (PrayerAudioHelper.IsPlaying && PrayerAudioHelper.IsPaused)
            {
                PrayerAudioHelper.ResumeAudio();
                TestNotificationStatus = "Melanjutkan pemutaran suara adzan...";
            }
            else
            {
                IsAdhanPlaying = true;
                TestNotificationStatus = "Memutar preview suara adzan...";
                double startSec = Settings.GetAdhanStartTotalSeconds();
                double endSec = Settings.GetAdhanEndTotalSeconds();
                PrayerAudioHelper.PlayPreview(SelectedAdhanSound, CustomAdhanSoundPath, AdhanVolume, startSec, endSec, () =>
                {
                    IsAdhanPlaying = false;
                });
            }
        });

        SetCurrentAsStartCommand = new RelayCommand(() =>
        {
            AdhanCustomDurationEnabled = true;
            int sec = (int)Math.Floor(_adhanCurrentPositionSeconds);
            if (sec < 0) sec = 0;
            AdhanStartMinutes = sec / 60;
            AdhanStartSeconds = sec % 60;
            TestNotificationStatus = $"Waktu mulai diset ke detik {AdhanStartMinutes:D2}:{AdhanStartSeconds:D2} ✓";
        });

        SetCurrentAsEndCommand = new RelayCommand(() =>
        {
            AdhanCustomDurationEnabled = true;
            int sec = (int)Math.Ceiling(_adhanCurrentPositionSeconds);
            if (sec <= 0) sec = 60;
            AdhanEndMinutes = sec / 60;
            AdhanEndSeconds = sec % 60;
            TestNotificationStatus = $"Waktu selesai diset ke detik {AdhanEndMinutes:D2}:{AdhanEndSeconds:D2} ✓";
        });

        BrowseCustomAdhanCommand = new RelayCommand(() =>
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Pilih File Audio Adzan (.mp3 / .wav)",
                Filter = "File Audio (*.mp3;*.wav)|*.mp3;*.wav|Semua File (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
            {
                CustomAdhanSoundPath = dlg.FileName;
                SelectedAdhanSound = "custom";
            }
        });

        SetAdhanPresetFullCommand = new RelayCommand(() =>
        {
            AdhanCustomDurationEnabled = false;
            TestNotificationStatus = "Durasi adzan diset penuh (full audio)";
        });

        SetAdhanPreset30sCommand = new RelayCommand(() =>
        {
            AdhanCustomDurationEnabled = true;
            AdhanStartMinutes = 0;
            AdhanStartSeconds = 0;
            AdhanEndMinutes = 0;
            AdhanEndSeconds = 30;
            TestNotificationStatus = "Durasi diset 30 detik (00:00 - 00:30)";
        });

        SetAdhanPreset1mCommand = new RelayCommand(() =>
        {
            AdhanCustomDurationEnabled = true;
            AdhanStartMinutes = 0;
            AdhanStartSeconds = 0;
            AdhanEndMinutes = 1;
            AdhanEndSeconds = 0;
            TestNotificationStatus = "Durasi diset 1 menit (00:00 - 01:00)";
        });

        SetAdhanPreset2mCommand = new RelayCommand(() =>
        {
            AdhanCustomDurationEnabled = true;
            AdhanStartMinutes = 0;
            AdhanStartSeconds = 0;
            AdhanEndMinutes = 2;
            AdhanEndSeconds = 0;
            TestNotificationStatus = "Durasi diset 2 menit (00:00 - 02:00)";
        });

        PrayerAudioHelper.PlaybackProgressChanged += OnPrayerAudioProgressChanged;
        PrayerAudioHelper.PlaybackStateChanged += OnPrayerAudioStateChanged;

        ToggleCustomCredentialsCommand = new RelayCommand(() => ShowCustomCredentials = !ShowCustomCredentials);
        ToggleCustomUpdateUrlCommand = new RelayCommand(() => ShowCustomUpdateUrl = !ShowCustomUpdateUrl);

        CheckUpdateCommand = new RelayCommand(async () =>
        {
            if (IsCheckingUpdate || IsDownloadingUpdate) return;
            IsCheckingUpdate = true;
            UpdateStatus = "Mengecek pembaruan aplikasi...";
            HasAvailableUpdate = false;
            AvailableUpdate = null;

            try
            {
                var update = await _updateService.CheckForUpdatesAsync();
                if (update != null && update.IsNewerVersion)
                {
                    AvailableUpdate = update;
                    HasAvailableUpdate = true;
                    UpdateStatus = $"Tersedia versi baru: v{update.Version} ({update.UpdateTypeLabel})";
                }
                else if (update != null)
                {
                    UpdateStatus = $"Aplikasi Anda sudah versi terbaru (v{CurrentVersion}).";
                }
                else
                {
                    UpdateStatus = "Tidak dapat memeriksa pembaruan saat ini. Periksa koneksi internet Anda.";
                }
            }
            catch (Exception ex)
            {
                UpdateStatus = $"Gagal mengecek pembaruan: {ex.Message}";
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        });

        DownloadAndInstallUpdateCommand = new RelayCommand(async () =>
        {
            if (AvailableUpdate == null) return;

            if (string.IsNullOrWhiteSpace(AvailableUpdate.DownloadUrl))
            {
                if (!string.IsNullOrWhiteSpace(AvailableUpdate.ReleasePageUrl))
                {
                    try { Process.Start(new ProcessStartInfo(AvailableUpdate.ReleasePageUrl) { UseShellExecute = true }); } catch { }
                }
                return;
            }

            IsDownloadingUpdate = true;
            DownloadProgress = 0;
            UpdateStatus = "Mengunduh file pembaruan...";

            try
            {
                var progress = new Progress<double>(pct => DownloadProgress = pct);
                var installerPath = await _updateService.DownloadUpdateAsync(AvailableUpdate.DownloadUrl, progress);
                UpdateStatus = "Download selesai. Menjalankan installer...";
                await Task.Delay(500);
                _updateService.LaunchInstallerAndExit(installerPath);
            }
            catch (Exception ex)
            {
                UpdateStatus = $"Gagal mengunduh installer: {ex.Message}";
                IsDownloadingUpdate = false;
            }
        });

        OpenReleasePageCommand = new RelayCommand(() =>
        {
            var url = AvailableUpdate?.ReleasePageUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                url = "https://github.com/fajarsutomo/tamanotch/releases";
            }
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        });

        _syncService.StatusChanged += () =>
        {
            OnPropertyChanged(nameof(IsGoogleDriveConnected));
            OnPropertyChanged(nameof(IsGoogleDriveConfigured));
            OnPropertyChanged(nameof(IsGoogleDriveSyncing));
            OnPropertyChanged(nameof(GoogleDriveUserEmail));
            OnPropertyChanged(nameof(GoogleDriveSyncStatus));
            OnPropertyChanged(nameof(GoogleDriveLastSyncFormatted));
        };

        ClearCacheCommand = new RelayCommand(() =>
        {
            _settingsService.ClearLyricsCache();
            StatusMessage = "Cache lirik berhasil dibersihkan.";
        });

        ConnectGoogleDriveCommand = new RelayCommand(async () =>
        {
            StatusMessage = "Menghubungkan akun Google...";
            var success = await _syncService.AuthenticateAsync();
            StatusMessage = success
                ? $"Berhasil terhubung ke Google Drive sebagai {_syncService.UserEmail}!"
                : $"Gagal terhubung: {_syncService.SyncStatus}";
        });

        DisconnectGoogleDriveCommand = new RelayCommand(async () =>
        {
            await _syncService.DisconnectAsync();
            StatusMessage = "Sambungan Google Drive diputuskan.";
        });

        ImportCredentialsCommand = new RelayCommand(() =>
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                Title = "Pilih file credentials.json dari Google Cloud Console"
            };

            if (dialog.ShowDialog() == true)
            {
                if (_syncService.ImportCredentialsFile(dialog.FileName))
                {
                    OnPropertyChanged(nameof(GoogleDriveClientId));
                    OnPropertyChanged(nameof(GoogleDriveClientSecret));
                    OnPropertyChanged(nameof(IsGoogleDriveConfigured));
                    StatusMessage = "Kredensial Google Drive berhasil diimpor!";
                }
                else
                {
                    StatusMessage = "Format file credentials.json tidak valid.";
                }
            }
        });

        SyncGoogleDriveNowCommand = new RelayCommand(async () =>
        {
            StatusMessage = "Menyinkronkan dengan Google Drive...";
            var todoService = new TodoService(_settingsService);
            var todos = todoService.LoadTodos();
            var deletedIds = todoService.GetDeletedIds();
            var merged = await _syncService.SyncAsync(todos, deletedIds);
            if (merged != null)
            {
                todoService.SaveTodos(merged);
                StatusMessage = "Sinkronisasi Google Drive berhasil!";
            }
            else
            {
                StatusMessage = $"Sinkronisasi gagal: {_syncService.SyncStatus}";
            }
        });

        ExitApplicationCommand = new RelayCommand(() =>
        {
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (System.Windows.Application.Current.MainWindow is MainWindow mw)
                {
                    mw.ExitApplication();
                }
                else
                {
                    System.Windows.Application.Current.Shutdown();
                }
            });
        });
    }
}
