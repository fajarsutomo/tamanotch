namespace NotchMusic.Models;

public class AppSettings
{
    public bool StartWithWindows { get; set; } = false;
    public bool AlwaysOnTop { get; set; } = true;
    public string PreferredDeviceName { get; set; } = string.Empty;
    public double PanelLeft { get; set; } = -1;
    public double PanelTop { get; set; } = -1;
    public double NotchHorizontalOffsetPercent { get; set; } = 0;
    public double IdleIconSize { get; set; } = 34;

    // Kustomisasi notch (warna = "#RRGGBB"; hover "default" = tampilan bawaan)
    public string IdleEyeColor { get; set; } = "#4FC3FF";
    public string IdleFrameColor { get; set; } = "#35D6FF";
    /// <summary>Bentuk idle notch (eksperimen): "Capsule" (bawaan) atau "Drop" (tetes air menggantung).</summary>
    public string IdleShape { get; set; } = "Capsule";
    /// <summary>Radius lengkung terbalik sisi atas (bentuk "Drop"), 0–16 px; dibatasi oleh margin jendela 16 px.</summary>
    public double IdleTopRadiusLeft { get; set; } = 10;
    public double IdleTopRadiusRight { get; set; } = 10;
    public bool IdleFrameAutoSize { get; set; } = true;
    public double IdleFrameWidth { get; set; } = 66;
    public double IdleFrameHeight { get; set; } = 30;
    public double IdleFrameRadiusLeft { get; set; } = 24;
    public double IdleFrameRadiusRight { get; set; } = 24;
    public bool HoverSameAsIdle { get; set; } = false;
    public string HoverFrameColor { get; set; } = "default";
    public string HoverRingColor { get; set; } = "default";

    // Pengingat kesehatan (opt-in): gerak dan minum selama jam kerja.
    public bool MoveReminderEnabled { get; set; } = false;
    public int MoveReminderMinutes { get; set; } = 45;
    public bool DrinkReminderEnabled { get; set; } = false;
    public int DrinkReminderMinutes { get; set; } = 60;
    public int DrinkGlassMl { get; set; } = 230;
    public int DrinkDailyGoalGlasses { get; set; } = 8;
    public string ReminderWorkStart { get; set; } = "08:00";
    public string ReminderWorkEnd { get; set; } = "17:00";
    public bool ReminderSoundEnabled { get; set; } = true;
    public int DrinkCountToday { get; set; } = 0;
    public string DrinkCountDate { get; set; } = string.Empty;
    public double LyricsLeft { get; set; } = -1;
    public double LyricsTop { get; set; } = -1;
    public double LyricsWidth { get; set; } = 480;
    public double LyricsHeight { get; set; } = 320;
    public double LyricsFontSize { get; set; } = 18;
    public double LyricsOpacity { get; set; } = 0.95;
    public bool IsPinned { get; set; } = false;
    public string PreferredSessionId { get; set; } = string.Empty;
    public bool AutoSearchLyrics { get; set; } = true;
    public bool ClickThroughInLyricsMode { get; set; } = false;
    public string ToggleVisibilityHotkey { get; set; } = "Ctrl+Alt+N";
    public string ToggleLyricsHotkey { get; set; } = "Ctrl+Alt+L";
    public Dictionary<string, double> SongOffsetsSeconds { get; set; } = new();

    // Google Drive Sync
    public string GoogleDriveClientId { get; set; } = string.Empty;
    public string GoogleDriveClientSecret { get; set; } = string.Empty;
    public bool GoogleDriveAutoSync { get; set; } = true;
    public string GoogleDriveUserEmail { get; set; } = string.Empty;
    public DateTime? GoogleDriveLastSyncTime { get; set; }

    // Auto-Update Settings
    public bool AutoCheckUpdates { get; set; } = true;
    public string CustomUpdateUrl { get; set; } = string.Empty;
    public DateTime? LastUpdateCheck { get; set; }
    public string IgnoredUpdateVersion { get; set; } = string.Empty;

    // Component Display & Detach Settings
    public string ComponentDisplayMode { get; set; } = "Both"; // "Both", "MusicOnly", "TodoOnly"
    public bool IsTodoDetached { get; set; } = false;
    public bool IsTodoWindowOpen { get; set; } = true;
    public double TodoWindowLeft { get; set; } = -1;
    public double TodoWindowTop { get; set; } = -1;
    public double TodoWindowWidth { get; set; } = 430;
    public double TodoWindowHeight { get; set; } = 520;
    public bool IsTodoWindowCompact { get; set; } = false;

    // Waktu Sholat & Pengingat (Prayer Times & Reminders)
    public bool EnablePrayerTimes { get; set; } = true;
    public bool IsPrayerDetached { get; set; } = false;
    public bool IsPrayerWindowOpen { get; set; } = true;
    public double PrayerWindowLeft { get; set; } = -1;
    public double PrayerWindowTop { get; set; } = -1;
    public double PrayerWindowWidth { get; set; } = 360;
    public double PrayerWindowHeight { get; set; } = 500;
    public bool IsPrayerWindowCompact { get; set; } = false;
    public string PrayerCity { get; set; } = "Jakarta";
    public double PrayerLatitude { get; set; } = -6.2088;
    public double PrayerLongitude { get; set; } = 106.8456;
    public double PrayerTimezone { get; set; } = 7.0;
    public bool RemindBeforePrayer { get; set; } = true;
    public int RemindBeforeMinutes { get; set; } = 10;
    public bool RemindAtPrayerTime { get; set; } = true;
    public bool RemindAfterPrayer { get; set; } = true;
    public int RemindAfterMinutes { get; set; } = 15;
    public bool PrayerSoundAlert { get; set; } = true;
    public bool PrayerAutoPauseMusic { get; set; } = true;

    // Puasa: pengingat sahur dan berbuka, pengingat minum diam selama berpuasa.
    public bool EnableFasting { get; set; } = false;
    public bool FastingMondayThursdayOnly { get; set; } = false;
    public int FastingSahurMinutes { get; set; } = 30;

    // Sapaan "selamat datang kembali" setelah idle lama.
    public string UserDisplayName { get; set; } = string.Empty;
    public bool EnableWelcomeBack { get; set; } = true;
    public int WelcomeBackIdleMinutes { get; set; } = 60;
    public bool ShowPrayerInCompactNotch { get; set; } = true;

    // Pilihan Suara Adzan & Notifikasi
    public string SelectedAdhanSound { get; set; } = "makkah"; // "makkah", "madinah", "alaqsa", "alafasy", "abdulbasit", "mansoor", "refaat", "turkey", "fajr", "chime", "custom"
    public string CustomAdhanSoundPath { get; set; } = string.Empty;
    public double AdhanVolume { get; set; } = 0.85;

    // Pengaturan Durasi Adzan Pendek / Potong Durasi (Menit & Detik Mulai s/d Selesai)
    public bool AdhanCustomDurationEnabled { get; set; } = false;
    public int AdhanStartMinutes { get; set; } = 0;
    public int AdhanStartSeconds { get; set; } = 0;
    public int AdhanEndMinutes { get; set; } = 1;
    public int AdhanEndSeconds { get; set; } = 0;

    public double GetAdhanStartTotalSeconds()
    {
        if (!AdhanCustomDurationEnabled) return 0;
        return Math.Max(0, AdhanStartMinutes) * 60 + Math.Clamp(AdhanStartSeconds, 0, 59);
    }

    public double GetAdhanEndTotalSeconds()
    {
        if (!AdhanCustomDurationEnabled) return 0;
        return Math.Max(0, AdhanEndMinutes) * 60 + Math.Clamp(AdhanEndSeconds, 0, 59);
    }

    // Browser Mengambang (Floating Browser & Video Player)
    public bool IsBrowserWindowOpen { get; set; } = false;
    public double BrowserLeft { get; set; } = -1;
    public double BrowserTop { get; set; } = -1;
    public double BrowserWidth { get; set; } = 960;
    public double BrowserHeight { get; set; } = 540;
    public bool BrowserAlwaysOnTop { get; set; } = true;
    public string BrowserLastUrl { get; set; } = "https://www.youtube.com";

    // Radio Online (Komponen ke-5)
    public bool IsRadioWindowOpen { get; set; } = false;
    public double RadioWindowLeft { get; set; } = -1;
    public double RadioWindowTop { get; set; } = -1;
    public double RadioWindowWidth { get; set; } = 410;
    public double RadioWindowHeight { get; set; } = 540;
    public bool IsRadioWindowCompact { get; set; } = false;
    public double RadioVolume { get; set; } = 0.80;
    public bool RadioMuted { get; set; } = false;
    public string LastRadioStationId { get; set; } = string.Empty;
    public List<string> FavoriteRadioStationIds { get; set; } = new();
    public List<RadioStation> CustomRadioStations { get; set; } = new();

    /// <summary>Stasiun hasil pencarian online yang dijadikan favorit; disimpan lengkap agar tetap ada setelah aplikasi dijalankan ulang.</summary>
    public List<RadioStation> SavedOnlineRadioStations { get; set; } = new();

    // Pemutar Al-Qur'an (audio per surah dari Quran.com)
    public bool IsQuranWindowOpen { get; set; } = false;
    public double QuranWindowLeft { get; set; } = -1;
    public double QuranWindowTop { get; set; } = -1;
    public bool IsQuranWindowCompact { get; set; } = false;
    public double QuranVolume { get; set; } = 0.80;
    public bool QuranMuted { get; set; } = false;
    public int LastQuranSurah { get; set; } = 0;
    /// <summary>Id qari di Quran.com (7 = Mishari Alafasy).</summary>
    public int QuranReciterId { get; set; } = 7;
    /// <summary>"Stop", "Next", atau "Random": kelanjutan setelah satu surah selesai.</summary>
    public string QuranAfterPlayback { get; set; } = "Next";

    // Pemakaian kuota Claude (sesi 5 jam dan mingguan). Mati secara bawaan: fitur ini membaca token OAuth Claude Code.
    public bool EnableClaudeUsage { get; set; } = false;
    public bool IsClaudeUsageWindowOpen { get; set; } = false;
    public double ClaudeUsageWindowLeft { get; set; } = -1;
    public double ClaudeUsageWindowTop { get; set; } = -1;
    /// <summary>Jeda penyegaran data, menit (2-30).</summary>
    public int ClaudeUsageRefreshMinutes { get; set; } = 3;
    /// <summary>Lencana 5 jam / 7 hari di baris pill island saat fitur aktif.</summary>
    public bool ShowClaudeUsageInIsland { get; set; } = true;
    /// <summary>Notifikasi tray saat kuota naik ke tingkat peringatan atau kritis.</summary>
    public bool NotifyClaudeUsage { get; set; } = true;

    public bool IsMusicWindowOpen { get; set; } = false;
    public double MusicWindowLeft { get; set; } = -1;
    public double MusicWindowTop { get; set; } = -1;
    public bool IsMusicWindowCompact { get; set; } = false;

    // YouTube Music Window (Browser YouTube Music di Music Player - Default Seukuran Handphone)
    public double YouTubeMusicLeft { get; set; } = -1;
    public double YouTubeMusicTop { get; set; } = -1;
    public double YouTubeMusicWidth { get; set; } = 380;
    public double YouTubeMusicHeight { get; set; } = 740;
    public bool YouTubeMusicAlwaysOnTop { get; set; } = false;

    // Spotify Window (Browser Spotify di Music Player - Default Seukuran Handphone)
    public double SpotifyLeft { get; set; } = -1;
    public double SpotifyTop { get; set; } = -1;
    public double SpotifyWidth { get; set; } = 380;
    public double SpotifyHeight { get; set; } = 740;
    public bool SpotifyAlwaysOnTop { get; set; } = false;

    // Jellyfin (pemutar web server media pribadi)
    public string JellyfinUrl { get; set; } = "https://myflix.tamahome.id/";
    public double JellyfinLeft { get; set; } = -1;
    public double JellyfinTop { get; set; } = -1;
    public double JellyfinWidth { get; set; } = 390;
    public double JellyfinHeight { get; set; } = 780;
    public bool JellyfinAlwaysOnTop { get; set; } = false;
    public bool JellyfinMobileMode { get; set; } = true;

    // DNS-over-HTTPS untuk jendela WebView2 (lewati DNS jaringan; berlaku setelah aplikasi dimulai ulang)
    public bool DohEnabled { get; set; } = false;
    public string DohProvider { get; set; } = "Cloudflare";

    // Live TV (Komponen ke-6)
    public bool IsLiveTvWindowOpen { get; set; } = false;
    public double LiveTvWindowLeft { get; set; } = -1;
    public double LiveTvWindowTop { get; set; } = -1;
    public double LiveTvWindowWidth { get; set; } = 480;
    public double LiveTvWindowHeight { get; set; } = 640;
    public bool IsLiveTvWindowCompact { get; set; } = false;
    public bool LiveTvAlwaysOnTop { get; set; } = true;
    public double LiveTvVolume { get; set; } = 0.85;
    public bool LiveTvMuted { get; set; } = false;
    public string LastLiveTvChannelId { get; set; } = string.Empty;
    public string LastLiveTvCountryCode { get; set; } = "id";
    public List<string> FavoriteLiveTvChannelIds { get; set; } = new();
    public List<LiveTvChannel> CustomLiveTvChannels { get; set; } = new();

    /// <summary>Saluran dari katalog/pencarian online yang dijadikan favorit; disimpan lengkap agar tetap ada setelah dijalankan ulang.</summary>
    public List<LiveTvChannel> SavedOnlineLiveTvChannels { get; set; } = new();

    // Cuaca & Gempa Bumi BMKG (Komponen ke-7)
    public bool EnableBmkg { get; set; } = true;
    public bool IsBmkgWindowOpen { get; set; } = false;
    public double BmkgWindowLeft { get; set; } = -1;
    public double BmkgWindowTop { get; set; } = -1;
    public double BmkgWindowWidth { get; set; } = 440;
    public double BmkgWindowHeight { get; set; } = 600;
    public bool IsBmkgWindowCompact { get; set; } = false;
    public bool ShowWeatherInCompactNotch { get; set; } = true;
    public bool ShowAirQualityInIsland { get; set; } = true;
    public bool BmkgFollowPrayerCity { get; set; } = true;
    public string BmkgSelectedCity { get; set; } = "Jakarta";
    public string BmkgCustomAdm4Code { get; set; } = string.Empty;
    public bool BmkgNotifyNewEarthquake { get; set; } = true;
    public bool BmkgNotifyExtremeWeather { get; set; } = true;
    public string LastNotifiedEarthquakeId { get; set; } = string.Empty;
    public string LastNotifiedWeatherAlertId { get; set; } = string.Empty;
}
