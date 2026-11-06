using System.Windows.Input;

namespace NotchMusic.ViewModels;

public partial class SettingsViewModel
{
    // Google Drive Sync Properties
    public string GoogleDriveClientId
    {
        get => Settings.GoogleDriveClientId;
        set
        {
            if (Settings.GoogleDriveClientId != value)
            {
                Settings.GoogleDriveClientId = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGoogleDriveConfigured));
            }
        }
    }

    public string GoogleDriveClientSecret
    {
        get => Settings.GoogleDriveClientSecret;
        set
        {
            if (Settings.GoogleDriveClientSecret != value)
            {
                Settings.GoogleDriveClientSecret = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGoogleDriveConfigured));
            }
        }
    }

    public bool GoogleDriveAutoSync
    {
        get => Settings.GoogleDriveAutoSync;
        set
        {
            if (Settings.GoogleDriveAutoSync != value)
            {
                Settings.GoogleDriveAutoSync = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public bool IsGoogleDriveConnected => _syncService.IsConnected;

    public bool IsGoogleDriveConfigured => _syncService.IsConfigured;

    public bool IsGoogleDriveSyncing => _syncService.IsSyncing;

    public string GoogleDriveUserEmail => _syncService.UserEmail;

    public string GoogleDriveSyncStatus => _syncService.SyncStatus;

    public string GoogleDriveLastSyncFormatted =>
        Settings.GoogleDriveLastSyncTime.HasValue
            ? Settings.GoogleDriveLastSyncTime.Value.ToString("dd MMM yyyy, HH:mm")
            : "Belum pernah";

    // Tanpa kredensial bawaan: bagian kredensial langsung terbuka selama Google Drive belum dikonfigurasi.
    private bool _showCustomCredentials;

    public bool ShowCustomCredentials
    {
        get => _showCustomCredentials;
        set => SetProperty(ref _showCustomCredentials, value);
    }

    public ICommand ConnectGoogleDriveCommand { get; }

    public ICommand DisconnectGoogleDriveCommand { get; }

    public ICommand ImportCredentialsCommand { get; }

    public ICommand SyncGoogleDriveNowCommand { get; }

    public ICommand ToggleCustomCredentialsCommand { get; }
}
