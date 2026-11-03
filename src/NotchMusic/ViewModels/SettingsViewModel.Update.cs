using System.Windows.Input;
using NotchMusic.Models;
using NotchMusic.Services.Update;

namespace NotchMusic.ViewModels;

public partial class SettingsViewModel
{
    private readonly IUpdateService _updateService;

    public string CurrentVersion => _updateService.CurrentVersion;

    public bool AutoCheckUpdates
    {
        get => Settings.AutoCheckUpdates;
        set
        {
            if (Settings.AutoCheckUpdates != value)
            {
                Settings.AutoCheckUpdates = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    public string CustomUpdateUrl
    {
        get => Settings.CustomUpdateUrl;
        set
        {
            if (Settings.CustomUpdateUrl != value)
            {
                Settings.CustomUpdateUrl = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    private bool _isCheckingUpdate;

    public bool IsCheckingUpdate
    {
        get => _isCheckingUpdate;
        set => SetProperty(ref _isCheckingUpdate, value);
    }

    private bool _isDownloadingUpdate;

    public bool IsDownloadingUpdate
    {
        get => _isDownloadingUpdate;
        set => SetProperty(ref _isDownloadingUpdate, value);
    }

    private double _downloadProgress;

    public double DownloadProgress
    {
        get => _downloadProgress;
        set => SetProperty(ref _downloadProgress, value);
    }

    private string _updateStatus = string.Empty;

    public string UpdateStatus
    {
        get => _updateStatus;
        set => SetProperty(ref _updateStatus, value);
    }

    private bool _hasAvailableUpdate;

    public bool HasAvailableUpdate
    {
        get => _hasAvailableUpdate;
        set => SetProperty(ref _hasAvailableUpdate, value);
    }

    private UpdateInfo? _availableUpdate;

    public UpdateInfo? AvailableUpdate
    {
        get => _availableUpdate;
        set => SetProperty(ref _availableUpdate, value);
    }

    private bool _showCustomUpdateUrl;

    public bool ShowCustomUpdateUrl
    {
        get => _showCustomUpdateUrl;
        set => SetProperty(ref _showCustomUpdateUrl, value);
    }

    public ICommand ToggleCustomUpdateUrlCommand { get; }

    public ICommand CheckUpdateCommand { get; }

    public ICommand DownloadAndInstallUpdateCommand { get; }

    public ICommand OpenReleasePageCommand { get; }
}
