using System.Diagnostics;
using System.Windows.Data;
using System.Windows.Threading;
using NotchMusic.Helpers;
using NotchMusic.Models;
using NotchMusic.Services.AirQuality;
using NotchMusic.Services.Bmkg;
using NotchMusic.Services.Location;
using NotchMusic.Services.Lyrics;
using NotchMusic.Services.LiveTv;
using NotchMusic.Services.Media;
using NotchMusic.Services.Prayer;
using NotchMusic.Services.ClaudeUsage;
using NotchMusic.Services.Quran;
using NotchMusic.Services.Radio;
using NotchMusic.Services.Settings;
using NotchMusic.Services.Sync;
using NotchMusic.Services.Todo;
using NotchMusic.Services.Update;
using NotchMusic.Services.Wellness;

namespace NotchMusic.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;

    private bool _isPinned;

    public AppSettings Settings => _settingsService.CurrentSettings;

    public MainViewModel(
        IMediaSessionService mediaService,
        ILyricsService lyricsService,
        ISettingsService settingsService,
        ITodoService? todoService = null,
        IGoogleDriveSyncService? googleDriveSyncService = null,
        IUpdateService? updateService = null,
        IPrayerService? prayerService = null,
        IPrayerReminderService? prayerReminderService = null,
        IRadioService? radioService = null,
        ILiveTvService? liveTvService = null,
        IBmkgService? bmkgService = null,
        ILocationService? locationService = null,
        IWellnessReminderService? wellnessService = null,
        IAirQualityService? airQualityService = null,
        IQuranService? quranService = null,
        IClaudeUsageService? claudeUsageService = null,
        IWelcomeBackService? welcomeBackService = null)
    {
        _mediaService = mediaService;
        _lyricsService = lyricsService;
        _settingsService = settingsService;
        _isPinned = _settingsService.CurrentSettings.IsPinned;
        _todoService = todoService ?? new TodoService(settingsService);
        _googleDriveSyncService = googleDriveSyncService ?? new GoogleDriveSyncService(settingsService);
        _updateService = updateService ?? new UpdateService(settingsService);
        _prayerService = prayerService ?? new PrayerService(settingsService);
        _prayerReminderService = prayerReminderService ?? new PrayerReminderService(_prayerService, settingsService);
        _radioService = radioService ?? new RadioService(settingsService);
        _liveTvService = liveTvService ?? new LiveTvService(settingsService);
        _bmkgService = bmkgService ?? new BmkgService(settingsService);
        _locationService = locationService ?? new LocationService(settingsService, _prayerService, _prayerReminderService);
        _locationService.LocationChanged += OnLocationChanged;
        _wellnessService = wellnessService ?? new WellnessReminderService(settingsService,
            isFasting: () => FastingHelper.IsFastingNow(settingsService.CurrentSettings, _prayerReminderService.CurrentSchedule, DateTime.Now));
        _wellnessService.ReminderTriggered += OnWellnessReminder;
        _welcomeBackService = welcomeBackService ?? new WelcomeBackService(settingsService);
        _welcomeBackService.Returned += OnWelcomeBack;
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ShowComponentIcons)) RaiseCenterIconChanged();
        };

        _airQualityService = airQualityService ?? new AirQualityService(
            () => (settingsService.CurrentSettings.PrayerLatitude, settingsService.CurrentSettings.PrayerLongitude),
            () => settingsService.CurrentSettings.EnableBmkg && settingsService.CurrentSettings.ShowAirQualityInIsland);
        _airQualityService.Updated += (s, e) => OnAirQualityUpdated();
        _bmkgService.DataUpdated += (s, e) => OnBmkgDataUpdated();
        _bmkgService.NotificationTriggered += (s, evt) => OnBmkgNotificationTriggered(evt);

        foreach (var st in _radioService.Stations)
        {
            RadioStations.Add(st);
        }

        _filteredRadioStations = CollectionViewSource.GetDefaultView(RadioStations);
        _filteredRadioStations.Filter = FilterRadioStation;

        InitializeQuran(quranService);
        InitializeClaudeUsage(claudeUsageService);

        _radioService.StateChanged += OnRadioStateChanged;
        _radioService.StationChanged += _ => OnRadioStateChanged();

        foreach (var country in _liveTvService.Countries)
        {
            LiveTvCountries.Add(country);
        }

        foreach (var ch in _liveTvService.Channels)
        {
            LiveTvChannels.Add(ch);
        }

        _filteredLiveTvChannels = CollectionViewSource.GetDefaultView(LiveTvChannels);
        _filteredLiveTvChannels.Filter = FilterLiveTvChannel;

        _liveTvService.StateChanged += OnLiveTvStateChanged;
        _liveTvService.ChannelChanged += _ => OnLiveTvStateChanged();
        _liveTvService.CatalogUpdated += OnLiveTvCatalogUpdated;

        TogglePrayerPanelCommand = new RelayCommand(() => IsPrayerPanelOpen = !IsPrayerPanelOpen);
        OpenPrayerScheduleCommand = new RelayCommand(() =>
        {
            if (IsPrayerDetached)
            {
                IsPrayerWindowOpen = true;
                RequestComponentLayoutChanged?.Invoke();
            }
            else
            {
                // Jadwal sholat selalu di jendela sendiri (seperti Cuaca & Gempa); pill di notch tetap ada.
                RequestOpenPrayerSchedule?.Invoke();
            }
        });
        DismissPrayerNotificationCommand = new RelayCommand(() =>
        {
            _notificationDismissTimer?.Stop();
            ActivePrayerNotification = null;
        });
        ExitApplicationCommand = new RelayCommand(() => RequestExit?.Invoke());

        _prayerReminderService.ReminderTriggered += OnPrayerReminderTriggered;
        _prayerReminderService.ScheduleUpdated += (sch) =>
        {
            OnPropertyChanged(nameof(CurrentPrayerSchedule));
            OnPropertyChanged(nameof(ShowCompactPrayerPill));
            OnPropertyChanged(nameof(NextPrayerName));
            OnPropertyChanged(nameof(NextPrayerTimeFormatted));
            OnPropertyChanged(nameof(NextPrayerCountdownBadge));
            OnPropertyChanged(nameof(PrayerScheduleTooltip));
        };

        OpenUpdateCommand = new RelayCommand(async () => await CheckAndInstallUpdateAsync());

        _googleDriveSyncService.StatusChanged += () =>
        {
            void Notify()
            {
                OnPropertyChanged(nameof(IsGoogleDriveConnected));
                OnPropertyChanged(nameof(IsGoogleDriveSyncing));
                OnPropertyChanged(nameof(GoogleDriveUserEmail));
                OnPropertyChanged(nameof(GoogleDriveSyncStatus));
                OnPropertyChanged(nameof(GoogleDriveStatusButtonText));
                OnPropertyChanged(nameof(GoogleDriveSyncTooltip));
            }

            if (System.Windows.Application.Current?.Dispatcher != null &&
                !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.InvokeAsync(Notify);
            }
            else
            {
                Notify();
            }
        };
        _googleDriveSyncService.TodosSynced += OnTodosSyncedFromCloud;

        _filteredTodos = CollectionViewSource.GetDefaultView(Todos);
        _filteredTodos.Filter = FilterTodoItem;
        _filteredTodos.GroupDescriptions.Add(new PropertyGroupDescription(nameof(TodoItemViewModel.CategoryGroup)));

        var loadedTodos = _todoService.LoadTodos();
        foreach (var item in loadedTodos)
        {
            var vm = new TodoItemViewModel(item);
            vm.OnChanged += OnTodoItemChanged;
            vm.OnDeleteRequested += DeleteTodo;
            Todos.Add(vm);
        }

        AddTodoCommand = new RelayCommand(AddTodo);
        ToggleSetDeadlineCommand = new RelayCommand(() => IsSettingNewTodoDeadline = !IsSettingNewTodoDeadline);
        SetDeadlinePresetCommand = new RelayCommand((p) =>
        {
            if (p is string presetStr)
            {
                SetDeadlinePreset(presetStr);
            }
        });
        ApplyCustomDeadlineCommand = new RelayCommand(ApplyCustomDeadline);
        ClearNewTodoDueDateCommand = new RelayCommand(() =>
        {
            NewTodoDueDate = null;
            IsSettingNewTodoDeadline = false;
        });
        DismissTodoNotificationCommand = new RelayCommand(() =>
        {
            _todoNotificationDismissTimer?.Stop();
            ActiveTodoNotification = null;
        });
        WellnessPrimaryActionCommand = new RelayCommand(() =>
        {
            var active = ActiveWellnessNotification;
            if (active == null) return;
            if (active.Kind == WellnessReminderKind.Drink) _wellnessService.LogDrink();
            else if (active.Kind == WellnessReminderKind.Move) _wellnessService.MarkMoved();
            ActiveWellnessNotification = null;
        });
        WellnessSnoozeCommand = new RelayCommand(() =>
        {
            var active = ActiveWellnessNotification;
            if (active == null) return;
            if (active.Kind != WellnessReminderKind.Welcome) _wellnessService.Snooze(active.Kind, 10);
            ActiveWellnessNotification = null;
        });
        DismissWellnessNotificationCommand = new RelayCommand(() => ActiveWellnessNotification = null);
        CompleteTodoNotificationCommand = new RelayCommand(() =>
        {
            if (ActiveTodoNotification != null)
            {
                var target = Todos.FirstOrDefault(t => t.Id == ActiveTodoNotification.TodoId);
                if (target != null)
                {
                    target.IsCompleted = true;
                }
                _todoNotificationDismissTimer?.Stop();
                ActiveTodoNotification = null;
            }
        });
        _todoDeadlineTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _todoDeadlineTimer.Tick += (s, e) => CheckTodoDeadlines();
        try
        {
            _todoDeadlineTimer.Start();
        }
        catch { }
        ClearCompletedTodosCommand = new RelayCommand(ClearCompletedTodos);
        SetTodoFilterCommand = new RelayCommand((f) =>
        {
            if (f is string filterStr)
            {
                TodoFilter = filterStr;
            }
        });

        SetCategoryFilterCommand = new RelayCommand((catObj) =>
        {
            if (catObj is string cat)
            {
                SelectedCategoryFilter = cat;
                if (!string.Equals(cat, "All", StringComparison.OrdinalIgnoreCase))
                {
                    NewTodoCategory = cat;
                }
            }
        });

        MoveCategoryUpCommand = new RelayCommand(p => MoveCategoryUp(p));
        MoveCategoryDownCommand = new RelayCommand(p => MoveCategoryDown(p));
        ExpandAllCategoriesCommand = new RelayCommand(ExpandAllCategories);
        CollapseAllCategoriesCommand = new RelayCommand(CollapseAllCategories);
        ToggleCategoryCollapseCommand = new RelayCommand(p => ToggleCategoryCollapse(p));

        UpdateAvailableCategories();

        NextTickerCommand = new RelayCommand(NextTicker);
        PreviousTickerCommand = new RelayCommand(PreviousTicker);
        CompleteCurrentTickerCommand = new RelayCommand(CompleteCurrentTicker);
        SyncGoogleDriveCommand = new RelayCommand(async () => await HandleGoogleDriveClickAsync());

        UpdateTicker();

        TogglePlayPauseCommand = new RelayCommand(async () => await _mediaService.TogglePlayPauseAsync());
        PreviousCommand = new RelayCommand(async () => await _mediaService.PreviousAsync());
        NextCommand = new RelayCommand(async () => await _mediaService.NextAsync());
        SeekCommand = new RelayCommand(async (param) =>
        {
            if (param is double seconds)
            {
                await _mediaService.SeekAsync(TimeSpan.FromSeconds(seconds));
            }
        });

        SetModeCommand = new RelayCommand((modeObj) =>
        {
            if (modeObj is PanelMode mode)
            {
                if (mode == PanelMode.Expanded) IsPrayerPanelOpen = false;
                CurrentMode = mode;
            }
            else if (modeObj is string modeStr && Enum.TryParse<PanelMode>(modeStr, true, out var parsed))
            {
                if (parsed == PanelMode.Expanded) IsPrayerPanelOpen = false;
                CurrentMode = parsed;
            }
        });

        TogglePinCommand = new RelayCommand(() => IsPinned = !IsPinned);
        OpenSettingsCommand = new RelayCommand(() => RequestOpenSettings?.Invoke());
        OpenYouTubeMusicCommand = new RelayCommand(() => RequestOpenYouTubeMusic?.Invoke());
        OpenSpotifyCommand = new RelayCommand(() => RequestOpenSpotify?.Invoke());
        OpenJellyfinCommand = new RelayCommand(() => RequestOpenJellyfin?.Invoke());
        ToggleVocalRemoverCommand = new RelayCommand(() => IsVocalRemoverEnabled = !IsVocalRemoverEnabled);
        OpenTodoCommand = new RelayCommand(() =>
        {
            CurrentMode = PanelMode.Compact;
            IsTodoWindowCompact = false;
            IsTodoDetached = true;
            if (IsTodoDetached)
            {
                IsTodoWindowOpen = true;
                RequestComponentLayoutChanged?.Invoke();
            }
            else
            {
                CurrentMode = PanelMode.Expanded;
            }
        });
        OpenFloatingBrowserCommand = new RelayCommand(() => RequestOpenFloatingBrowser?.Invoke());
        ImportLyricsCommand = new RelayCommand(ImportLocalLyrics);
        OffsetAddCommand = new RelayCommand(() => AdjustOffset(0.5));
        OffsetSubtractCommand = new RelayCommand(() => AdjustOffset(-0.5));
        OffsetResetCommand = new RelayCommand(() => AdjustOffset(0, reset: true));
        SearchLyricsCommand = new RelayCommand(async () => await ExecuteManualSearchAsync());
        SelectSearchResultCommand = new RelayCommand((item) =>
        {
            if (item is LrcLibSearchResult res)
            {
                ApplySearchResult(res);
            }
        });
        SelectSessionCommand = new RelayCommand((sessId) =>
        {
            if (sessId is string id)
            {
                _mediaService.SelectSession(id);
                _settingsService.CurrentSettings.PreferredSessionId = id;
                _settingsService.Save();
            }
        });

        DetachTodoCommand = new RelayCommand(() =>
        {
            IsTodoDetached = true;
            IsTodoWindowOpen = true;
            IsComponentsMenuOpen = false;
        });
        AttachTodoCommand = new RelayCommand(() =>
        {
            IsTodoDetached = false;
            IsTodoWindowOpen = true;
            IsComponentsMenuOpen = false;
        });
        CloseTodoCommand = new RelayCommand(() =>
        {
            IsTodoDetached = true;
            IsTodoWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        MinimizeTodoWindowCommand = new RelayCommand(() => IsTodoWindowCompact = true);
        ExpandTodoWindowCommand = new RelayCommand(() => IsTodoWindowCompact = false);

        DetachPrayerCommand = new RelayCommand(() =>
        {
            IsPrayerDetached = true;
            IsPrayerWindowOpen = true;
            IsComponentsMenuOpen = false;
        });
        AttachPrayerCommand = new RelayCommand(() =>
        {
            IsPrayerDetached = false;
            IsPrayerWindowOpen = true;
            IsComponentsMenuOpen = false;
        });
        ClosePrayerCommand = new RelayCommand(() =>
        {
            IsPrayerDetached = true;
            IsPrayerWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        MinimizePrayerWindowCommand = new RelayCommand(() => IsPrayerWindowCompact = true);
ExpandPrayerWindowCommand = new RelayCommand(() => IsPrayerWindowCompact = false);

        PlayAdhanPreviewCommand = new RelayCommand(() =>
        {
            if (IsAdhanPlaying)
            {
                PrayerAudioHelper.StopAudio();
                IsAdhanPlaying = false;
            }
            else
            {
                IsAdhanPlaying = true;
                double startSec = Settings.GetAdhanStartTotalSeconds();
                double endSec = Settings.GetAdhanEndTotalSeconds();
                PrayerAudioHelper.PlayPreview(SelectedAdhanSound, Settings.CustomAdhanSoundPath, Settings.AdhanVolume, startSec, endSec, () =>
                {
                    IsAdhanPlaying = false;
                });
            }
        });

        StopAdhanPreviewCommand = new RelayCommand(() =>
        {
            PrayerAudioHelper.StopAudio();
            IsAdhanPlaying = false;
        });

        ToggleAdhanPlayPauseCommand = new RelayCommand(() =>
        {
            if (PrayerAudioHelper.IsPlaying && !PrayerAudioHelper.IsPaused)
            {
                PrayerAudioHelper.PauseAudio();
            }
            else if (PrayerAudioHelper.IsPlaying && PrayerAudioHelper.IsPaused)
            {
                PrayerAudioHelper.ResumeAudio();
            }
            else
            {
                IsAdhanPlaying = true;
                double startSec = Settings.GetAdhanStartTotalSeconds();
                double endSec = Settings.GetAdhanEndTotalSeconds();
                PrayerAudioHelper.PlayPreview(SelectedAdhanSound, Settings.CustomAdhanSoundPath, Settings.AdhanVolume, startSec, endSec, () =>
                {
                    IsAdhanPlaying = false;
                });
            }
        });

        PrayerAudioHelper.PlaybackProgressChanged += (current, total) =>
        {
            _adhanCurrentSec = current;
            _adhanTotalSec = total;
            OnPropertyChanged(nameof(AdhanCurrentPositionSeconds));
            OnPropertyChanged(nameof(AdhanProgressText));
            OnPropertyChanged(nameof(AdhanProgressPercent));
            OnPropertyChanged(nameof(AdhanCurrentPositionText));
            OnPropertyChanged(nameof(AdhanTotalDurationText));
        };

        PrayerAudioHelper.PlaybackStateChanged += () =>
        {
            _isAdhanPlaying = PrayerAudioHelper.IsPlaying;
            _isAdhanPaused = PrayerAudioHelper.IsPaused;
            OnPropertyChanged(nameof(IsAdhanPlaying));
            OnPropertyChanged(nameof(IsAdhanPaused));
            OnPropertyChanged(nameof(AdhanPlayButtonIcon));
            OnPropertyChanged(nameof(AdhanPlayPauseButtonIcon));
        };

        ToggleComponentsMenuCommand = new RelayCommand(() => IsComponentsMenuOpen = !IsComponentsMenuOpen);
        RestoreAllComponentsCommand = new RelayCommand(() =>
        {
            IsPrayerDetached = false;
            IsPrayerWindowOpen = true;
            IsTodoDetached = false;
            IsTodoWindowOpen = true;
            IsBrowserWindowOpen = true;
            ComponentDisplayMode = "Both";
            IsComponentsMenuOpen = false;
        });

        ToggleBrowserCommand = new RelayCommand(() =>
        {
            IsBrowserWindowOpen = !IsBrowserWindowOpen;
            IsComponentsMenuOpen = false;
        });
        OpenBrowserCommand = new RelayCommand(() =>
        {
            IsBrowserWindowOpen = true;
            IsComponentsMenuOpen = false;
        });
        CloseBrowserCommand = new RelayCommand(() =>
        {
            IsBrowserWindowOpen = false;
            IsComponentsMenuOpen = false;
        });

        OpenRadioCommand = new RelayCommand(() =>
        {
            IsRadioWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenRadio?.Invoke();
        });
        OpenMusicWindowCommand = new RelayCommand(() =>
        {
            IsMusicWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenMusic?.Invoke();
        });
        CloseMusicWindowCommand = new RelayCommand(() =>
        {
            IsMusicWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        ToggleRadioWindowCommand = new RelayCommand(() =>
        {
            IsRadioWindowOpen = !IsRadioWindowOpen;
            IsComponentsMenuOpen = false;
            if (IsRadioWindowOpen)
            {
                RequestOpenRadio?.Invoke();
            }
        });
        CloseRadioWindowCommand = new RelayCommand(() =>
        {
            IsRadioWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        MinimizeRadioWindowCommand = new RelayCommand(() => IsRadioWindowCompact = true);
        MinimizeMusicWindowCommand = new RelayCommand(() => IsMusicWindowCompact = true);
        ExpandMusicWindowCommand = new RelayCommand(() => IsMusicWindowCompact = false);
        ExpandRadioWindowCommand = new RelayCommand(() => IsRadioWindowCompact = false);

        ToggleRadioPlayPauseCommand = new RelayCommand(() => _radioService.TogglePlayPause());
        StopRadioCommand = new RelayCommand(() => _radioService.Stop());
        NextRadioStationCommand = new RelayCommand(() => _radioService.NextStation(FilteredRadioStations.Cast<RadioStation>()));
        PreviousRadioStationCommand = new RelayCommand(() => _radioService.PreviousStation(FilteredRadioStations.Cast<RadioStation>()));
        PlayRadioStationCommand = new RelayCommand((stObj) =>
        {
            if (stObj is RadioStation st)
            {
                _radioService.PlayStation(st);
            }
        });
        SetRadioCategoryCommand = new RelayCommand((catObj) =>
        {
            if (catObj is string cat)
            {
                RadioCategoryFilter = cat;
            }
        });
        ToggleCurrentRadioFavoriteCommand = new RelayCommand(() =>
        {
            if (_radioService.CurrentStation != null)
            {
                _radioService.ToggleFavorite(_radioService.CurrentStation);
                _filteredRadioStations?.Refresh();
            }
        });
        ToggleStationFavoriteCommand = new RelayCommand((stObj) =>
        {
            if (stObj is RadioStation st)
            {
                _radioService.ToggleFavorite(st);
                _filteredRadioStations?.Refresh();
            }
        });
        ToggleRadioMuteCommand = new RelayCommand(() => IsRadioMuted = !IsRadioMuted);
        SearchOnlineRadioCommand = new RelayCommand(async () => await ExecuteOnlineRadioSearchAsync());
        ToggleAddCustomRadioCommand = new RelayCommand(() => IsAddingCustomRadio = !IsAddingCustomRadio);
        SaveCustomRadioCommand = new RelayCommand(SaveCustomRadioStation);
        DeleteCustomRadioStationCommand = new RelayCommand((stObj) =>
        {
            if (stObj is RadioStation st && st.IsCustom)
            {
                _radioService.RemoveCustomStation(st);
                RadioStations.Remove(st);
                _filteredRadioStations?.Refresh();
            }
        });

        // Live TV Commands
        OpenLiveTvCommand = new RelayCommand(() =>
        {
            IsLiveTvWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenLiveTv?.Invoke();
        });
        ToggleLiveTvWindowCommand = new RelayCommand(() =>
        {
            IsLiveTvWindowOpen = !IsLiveTvWindowOpen;
            IsComponentsMenuOpen = false;
            if (IsLiveTvWindowOpen)
            {
                RequestOpenLiveTv?.Invoke();
            }
        });
        CloseLiveTvWindowCommand = new RelayCommand(() =>
        {
            _liveTvService.Stop();
            IsLiveTvWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        MinimizeLiveTvWindowCommand = new RelayCommand(() => IsLiveTvWindowCompact = true);
        ExpandLiveTvWindowCommand = new RelayCommand(() => IsLiveTvWindowCompact = false);
        ToggleLiveTvPlayPauseCommand = new RelayCommand(() => _liveTvService.TogglePlayPause());
        StopLiveTvCommand = new RelayCommand(() => _liveTvService.Stop());
        NextLiveTvChannelCommand = new RelayCommand(() => _liveTvService.NextChannel(FilteredLiveTvChannels.Cast<LiveTvChannel>()));
        PreviousLiveTvChannelCommand = new RelayCommand(() => _liveTvService.PreviousChannel(FilteredLiveTvChannels.Cast<LiveTvChannel>()));
        PlayLiveTvChannelCommand = new RelayCommand((chObj) =>
        {
            if (chObj is LiveTvChannel ch)
            {
                if (IsRadioPlaying || IsRadioBuffering)
                {
                    _radioService.Stop();
                }
                _liveTvService.PlayChannel(ch);
            }
        });
        CycleLiveTvSourceCommand = new RelayCommand(() => _liveTvService.CycleNextSource());
        SetLiveTvCategoryCommand = new RelayCommand((catObj) =>
        {
            if (catObj is string cat)
            {
                LiveTvCategoryFilter = cat;
            }
        });
        ToggleCurrentLiveTvFavoriteCommand = new RelayCommand(() =>
        {
            if (_liveTvService.CurrentChannel != null)
            {
                _liveTvService.ToggleFavorite(_liveTvService.CurrentChannel);
                _filteredLiveTvChannels?.Refresh();
                OnPropertyChanged(nameof(LiveTvCatalogCountText));
            }
        });
        ToggleLiveTvChannelFavoriteCommand = new RelayCommand((chObj) =>
        {
            if (chObj is LiveTvChannel ch)
            {
                _liveTvService.ToggleFavorite(ch);
                _filteredLiveTvChannels?.Refresh();
                OnPropertyChanged(nameof(LiveTvCatalogCountText));
            }
        });
        ToggleLiveTvMuteCommand = new RelayCommand(() => IsLiveTvMuted = !IsLiveTvMuted);
        SearchOnlineLiveTvCommand = new RelayCommand(async () => await ExecuteOnlineLiveTvSearchAsync());
        ToggleAddCustomLiveTvCommand = new RelayCommand(() => IsAddingCustomLiveTv = !IsAddingCustomLiveTv);
        SaveCustomLiveTvCommand = new RelayCommand(SaveCustomLiveTvChannel);
        DeleteCustomLiveTvChannelCommand = new RelayCommand((chObj) =>
        {
            if (chObj is LiveTvChannel ch && ch.IsCustom)
            {
                _liveTvService.RemoveCustomChannel(ch);
                LiveTvChannels.Remove(ch);
                _filteredLiveTvChannels?.Refresh();
                OnPropertyChanged(nameof(LiveTvCatalogCountText));
            }
        });

        // BMKG Cuaca & Gempa Bumi Commands
        OpenBmkgCommand = new RelayCommand(() =>
        {
            IsBmkgWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenBmkg?.Invoke();
            _ = _bmkgService.RefreshAllAsync(forceWeather: false);
        });
        OpenBmkgWeatherTabCommand = new RelayCommand(() =>
        {
            BmkgActiveTab = "Weather";
            IsBmkgWindowCompact = false;
            IsBmkgWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenBmkg?.Invoke();
            _ = _bmkgService.RefreshAllAsync(forceWeather: false);
        });
        OpenBmkgEarthquakeTabCommand = new RelayCommand(() =>
        {
            BmkgActiveTab = "Earthquake";
            IsBmkgWindowCompact = false;
            IsBmkgWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenBmkg?.Invoke();
            _ = _bmkgService.RefreshAllAsync(forceWeather: false);
        });
        ToggleBmkgWindowCommand = new RelayCommand(() =>
        {
            IsBmkgWindowOpen = !IsBmkgWindowOpen;
            IsComponentsMenuOpen = false;
            if (IsBmkgWindowOpen)
            {
                RequestOpenBmkg?.Invoke();
                _ = _bmkgService.RefreshAllAsync(forceWeather: false);
            }
        });
        CloseBmkgWindowCommand = new RelayCommand(() =>
        {
            IsBmkgWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        MinimizeBmkgWindowCommand = new RelayCommand(() => IsBmkgWindowCompact = true);
        ExpandBmkgWindowCommand = new RelayCommand(() => IsBmkgWindowCompact = false);
        SetBmkgTabCommand = new RelayCommand((tabObj) =>
        {
            if (tabObj is string tab && !string.IsNullOrWhiteSpace(tab))
            {
                BmkgActiveTab = tab;
            }
        });
        SetBmkgEarthquakeSubTabCommand = new RelayCommand((subObj) =>
        {
            if (subObj is string sub && !string.IsNullOrWhiteSpace(sub))
            {
                BmkgEarthquakeSubTab = sub;
            }
        });
        RefreshBmkgCommand = new RelayCommand(async () =>
        {
            await _bmkgService.RefreshAllAsync(forceWeather: true);
        });
        DetectBmkgLocationCommand = new RelayCommand(async () => await DetectBmkgLocationAsync());
        ToggleShakemapCommand = new RelayCommand(() => IsShowingShakemap = !IsShowingShakemap);
        OpenEarthquakeMapCommand = new RelayCommand((eqObj) =>
        {
            var eq = eqObj as BmkgEarthquakeItem ?? LatestEarthquake;
            if (eq != null && !string.IsNullOrWhiteSpace(eq.GoogleMapsUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(eq.GoogleMapsUrl) { UseShellExecute = true });
                }
                catch { }
            }
        });
        OpenShakemapBrowserCommand = new RelayCommand(() =>
        {
            if (LatestEarthquake != null && LatestEarthquake.HasShakemap)
            {
                try
                {
                    Process.Start(new ProcessStartInfo(LatestEarthquake.ShakemapUrl) { UseShellExecute = true });
                }
                catch { }
            }
        });
        OpenBmkgWebsiteCommand = new RelayCommand(() =>
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://www.bmkg.go.id") { UseShellExecute = true });
            }
            catch { }
        });
        ToggleCustomAdm4EditCommand = new RelayCommand(() => IsEditingCustomAdm4 = !IsEditingCustomAdm4);
        ApplyCustomAdm4Command = new RelayCommand(async () =>
        {
            IsEditingCustomAdm4 = false;
            OnPropertyChanged(nameof(BmkgActiveAdm4Text));
            _ = SyncRegionSelectorsFromCurrentAdm4Async();
            await _bmkgService.RefreshWeatherAsync();
        });
        TogglePreciseRegionSelectorCommand = new RelayCommand(() => IsSelectingPreciseRegion = !IsSelectingPreciseRegion);
        SearchBmkgLocationCommand = new RelayCommand(async () => await ExecuteBmkgPreciseLocationSearchAsync());
        SelectBmkgSearchResultCommand = new RelayCommand(async (resObj) =>
        {
            if (resObj is BmkgLocationSearchResult res)
            {
                await ApplyBmkgSearchResultAsync(res);
            }
        });
        ClearBmkgLocationSearchCommand = new RelayCommand(() =>
        {
            BmkgLocationSearchResults.Clear();
            OnPropertyChanged(nameof(HasBmkgLocationSearchResults));
        });
        TestBmkgAlertCommand = new RelayCommand(() => _bmkgService.TriggerTestEarthquakeAlert());
        DismissBmkgNotificationCommand = new RelayCommand(() =>
        {
            _bmkgNotificationDismissTimer?.Stop();
            ActiveBmkgNotification = null;
        });
        OpenBmkgNotificationDetailCommand = new RelayCommand(() =>
        {
            bool isEq = ActiveBmkgNotification?.IsEarthquake ?? true;
            _bmkgNotificationDismissTimer?.Stop();
            ActiveBmkgNotification = null;
            BmkgActiveTab = isEq ? "Earthquake" : "Weather";
            if (isEq) BmkgEarthquakeSubTab = "AutoGempa";
            IsBmkgWindowCompact = false;
            IsBmkgWindowOpen = true;
            RequestOpenBmkg?.Invoke();
        });

        _mediaService.TrackChanged += OnTrackChanged;
        _mediaService.TimelineUpdated += OnTimelineUpdated;
        _mediaService.SessionsChanged += (s, list) =>
        {
            if (System.Windows.Application.Current?.Dispatcher != null && !System.Windows.Application.Current.Dispatcher.CheckAccess())
            {
                System.Windows.Application.Current.Dispatcher.BeginInvoke(() => OnPropertyChanged(nameof(AvailableSessions)));
            }
            else
            {
                OnPropertyChanged(nameof(AvailableSessions));
            }
        };

        // Smooth timer for progress and lyrics interpolation (runs at ~25fps)
        _interpolationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        _interpolationTimer.Tick += InterpolationTimer_Tick;
        _interpolationTimer.Start();

        StartCloudSyncPollingTimer();
    }

    public async Task InitializeAsync()
    {
        await _mediaService.InitializeAsync();
        _prayerReminderService.Start();
        _wellnessService.Start();
        _welcomeBackService.Start();
        if (Settings.EnableBmkg)
        {
            _bmkgService.Start();
            if (Settings.ShowAirQualityInIsland) _airQualityService.Start();
        }

        if (Settings.EnableClaudeUsage) _claudeUsageService.Start();

        if (_googleDriveSyncService.IsConnected && Settings.GoogleDriveAutoSync)
        {
            _ = Task.Run(async () =>
            {
                await SyncWithGoogleDriveAsync();
            });
        }

        if (Settings.AutoCheckUpdates)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(3000);
                    var update = await _updateService.CheckForUpdatesAsync();
                    if (update != null && update.IsNewerVersion)
                    {
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            AvailableUpdateInfo = update;
                            AvailableUpdateVersion = $"v{update.Version}";
                            HasAvailableUpdate = true;
                        });
                    }
                }
                catch { }
            });
        }
    }
}
