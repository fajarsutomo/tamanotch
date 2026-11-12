using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using System.Windows.Input;
using System.Windows.Media;
using TamaNotch.Helpers;
using TamaNotch.Services.ClaudeUsage;

namespace TamaNotch.ViewModels;

/// <summary>Satu baris bar kuota di jendela pemakaian Claude.</summary>
public sealed record ClaudeUsageBarItem(
    string Label,
    string PercentText,
    double Percent,
    double? Elapsed,
    string ResetText,
    ClaudeUsageLevel Level,
    Brush Brush);

public partial class MainViewModel
{
    private IClaudeUsageService _claudeUsageService = null!;

    public event Action? RequestOpenClaudeUsage;

    public IClaudeUsageService ClaudeUsageService => _claudeUsageService;

    public ICommand OpenClaudeUsageCommand { get; private set; } = null!;
    public ICommand CloseClaudeUsageWindowCommand { get; private set; } = null!;
    public ICommand EnableClaudeUsageCommand { get; private set; } = null!;
    public ICommand RefreshClaudeUsageCommand { get; private set; } = null!;

    private void InitializeClaudeUsage(IClaudeUsageService? service)
    {
        _claudeUsageService = service ?? new ClaudeUsageService(
            () => _settingsService.CurrentSettings.EnableClaudeUsage,
            () => TimeSpan.FromMinutes(_settingsService.CurrentSettings.ClaudeUsageRefreshMinutes));
        _claudeUsageService.Updated += (s, e) => OnClaudeUsageUpdated();

        OpenClaudeUsageCommand = new RelayCommand(() =>
        {
            IsClaudeUsageWindowOpen = true;
            IsComponentsMenuOpen = false;
            RequestOpenClaudeUsage?.Invoke();
        });
        CloseClaudeUsageWindowCommand = new RelayCommand(() =>
        {
            IsClaudeUsageWindowOpen = false;
            IsComponentsMenuOpen = false;
        });
        EnableClaudeUsageCommand = new RelayCommand(() => EnableClaudeUsage = true);
        RefreshClaudeUsageCommand = new RelayCommand(() => _ = _claudeUsageService.RefreshAsync());
    }

    public bool EnableClaudeUsage
    {
        get => Settings.EnableClaudeUsage;
        set
        {
            if (Settings.EnableClaudeUsage == value) return;
            Settings.EnableClaudeUsage = value;
            _settingsService.Save();
            if (value) _claudeUsageService.Start();
            else _claudeUsageService.Stop();
            RaiseClaudeUsageChanged();
        }
    }

    public bool IsClaudeUsageWindowOpen
    {
        get => Settings.IsClaudeUsageWindowOpen;
        set
        {
            if (Settings.IsClaudeUsageWindowOpen == value) return;
            Settings.IsClaudeUsageWindowOpen = value;
            _settingsService.Save();
            OnPropertyChanged(nameof(IsClaudeUsageWindowOpen));
            OnPropertyChanged(nameof(IsNotchActive));
            RequestComponentLayoutChanged?.Invoke();
        }
    }

    /// <summary>Selaraskan layanan dengan pengaturan setelah jendela Pengaturan ditutup (aktif/mati, interval baru).</summary>
    public void SyncClaudeUsageWithSettings()
    {
        if (Settings.EnableClaudeUsage)
        {
            if (_claudeUsageService.Status == ClaudeUsageStatus.Idle && _claudeUsageService.Current == null)
                _claudeUsageService.Start();
        }
        else
        {
            _claudeUsageService.Stop();
        }

        RaiseClaudeUsageChanged();
    }

    // --- Lencana island dan notifikasi ---

    private static readonly TimeSpan ClaudeBadgeMaxAge = TimeSpan.FromMinutes(30);

    private readonly Dictionary<string, (ClaudeUsageLevel Level, long ResetKey)> _claudeAlerted = new();

    private ClaudeUsageWindow? ClaudeWindow(string key)
        => _claudeUsageService.Current?.Windows.FirstOrDefault(w => w.Key == key);

    private static string ClaudeBadgeText(string prefix, ClaudeUsageWindow? w)
        => w == null ? string.Empty : FormattableString.Invariant($"{prefix} {Math.Round(w.Percent):0}%");

    private static Brush ClaudeBadgeBrush(ClaudeUsageWindow? w)
        => ClaudeUsageBrush(w == null ? ClaudeUsageLevel.Normal
            : Services.ClaudeUsage.ClaudeUsageService.Classify(w.Percent, w.ElapsedFraction(DateTimeOffset.Now)));

    /// <summary>Lencana tampil bila fitur aktif, opsi island menyala, dan ada angka yang belum basi.</summary>
    public bool ShowClaudeUsageBadge => EnableClaudeUsage && Settings.ShowClaudeUsageInIsland
        && _claudeUsageService.Current is { } c && DateTime.Now - c.FetchedAt <= ClaudeBadgeMaxAge
        && (ClaudeWindow("five_hour") != null || ClaudeWindow("seven_day") != null);

    public string ClaudeBadgeSessionText => ClaudeBadgeText("5j", ClaudeWindow("five_hour"));

    public string ClaudeBadgeWeekText => ClaudeBadgeText("7h", ClaudeWindow("seven_day"));

    public Brush ClaudeBadgeSessionBrush => ClaudeBadgeBrush(ClaudeWindow("five_hour"));

    public Brush ClaudeBadgeWeekBrush => ClaudeBadgeBrush(ClaudeWindow("seven_day"));

    public bool HasClaudeBadgeSession => ClaudeWindow("five_hour") != null;

    public bool HasClaudeBadgeWeek => ClaudeWindow("seven_day") != null;

    public string ClaudeBadgeTooltip
    {
        get
        {
            var lines = ClaudeUsageBars.Select(b => $"{b.Label}: {b.PercentText} - {b.ResetText}").ToList();
            if (lines.Count == 0) return string.Empty;
            return "Pemakaian Claude\n" + string.Join("\n", lines) + "\n(Klik untuk membuka jendela)";
        }
    }

    /// <summary>
    /// Kirim notifikasi tray bila sebuah kuota naik ke tingkat peringatan atau kritis. Satu kali per tingkat per
    /// periode reset, sehingga tidak berulang tiap penyegaran dan muncul lagi setelah kuota di-reset.
    /// </summary>
    public void CheckClaudeUsageAlerts()
    {
        if (!EnableClaudeUsage || !Settings.NotifyClaudeUsage || _claudeUsageService.Current is not { } snapshot) return;

        var now = DateTimeOffset.Now;
        foreach (var w in snapshot.Windows)
        {
            var level = Services.ClaudeUsage.ClaudeUsageService.Classify(w.Percent, w.ElapsedFraction(now));
            if (level == ClaudeUsageLevel.Normal) continue;

            var resetKey = w.ResetsAt is { } r ? r.ToUnixTimeSeconds() / 60 : 0;
            if (_claudeAlerted.TryGetValue(w.Key, out var last) && last.ResetKey == resetKey && last.Level >= level) continue;

            _claudeAlerted[w.Key] = (level, resetKey);
            var severity = level == ClaudeUsageLevel.Critical ? "hampir habis" : "mulai tinggi";
            RequestShowTrayNotification?.Invoke(
                "Kuota Claude " + severity,
                $"{w.Label}: {Math.Round(w.Percent):0}% terpakai. {ClaudeUsageFormat.Reset(w.ResetsAt, now)}.");
        }
    }

    public bool IsClaudeUsageDisabled => !EnableClaudeUsage;

    public bool HasClaudeUsageData => EnableClaudeUsage && _claudeUsageService.Current != null;

    public bool HasClaudeUsageStatus => EnableClaudeUsage && ClaudeUsageStatusText.Length > 0;

    public string ClaudeUsageStatusText => ClaudeUsageFormat.Status(_claudeUsageService.Status, _claudeUsageService.NextAllowedAt);

    public string ClaudeUsagePlanText => ClaudeUsageFormat.PlanName(_claudeUsageService.Current?.Plan);

    public string ClaudeUsageUpdatedText => ClaudeUsageFormat.Updated(_claudeUsageService.Current?.FetchedAt, DateTime.Now);

    public IReadOnlyList<ClaudeUsageBarItem> ClaudeUsageBars
    {
        get
        {
            if (_claudeUsageService.Current is not { } snapshot) return Array.Empty<ClaudeUsageBarItem>();
            var now = DateTimeOffset.Now;
            return snapshot.Windows.Select(w =>
            {
                var elapsed = w.ElapsedFraction(now);
                var level = Services.ClaudeUsage.ClaudeUsageService.Classify(w.Percent, elapsed);
                return new ClaudeUsageBarItem(
                    w.Label,
                    FormattableString.Invariant($"{Math.Round(w.Percent):0}%"),
                    w.Percent,
                    elapsed,
                    ClaudeUsageFormat.Reset(w.ResetsAt, now),
                    level,
                    ClaudeUsageBrush(level));
            }).ToList();
        }
    }

    /// <summary>Porsi pemakaian mingguan per sumber, hanya yang lebih dari nol; kosong menyembunyikan bagiannya.</summary>
    public IReadOnlyList<string> ClaudeUsageShares
        => _claudeUsageService.Current?.Shares
            .Where(s => s.Percent > 0)
            .Select(s => FormattableString.Invariant($"{s.Name} {Math.Round(s.Percent):0}%"))
            .ToList() ?? new List<string>();

    public bool HasClaudeUsageShares => ClaudeUsageShares.Count > 0;

    public static Brush ClaudeUsageBrush(ClaudeUsageLevel level) => level switch
    {
        ClaudeUsageLevel.Critical => ClaudeBrushFor("#FF453A"),
        ClaudeUsageLevel.Warning => ClaudeBrushFor("#FFB020"),
        _ => ClaudeBrushFor("#4FC3F7")
    };

    private static readonly Dictionary<string, Brush> ClaudeBrushes = new();

    private static Brush ClaudeBrushFor(string hex)
    {
        if (!ClaudeBrushes.TryGetValue(hex, out var brush))
        {
            var color = (Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            brush = NeonPalette.Freeze(new SolidColorBrush(color));
            ClaudeBrushes[hex] = brush;
        }

        return brush;
    }

    private void OnClaudeUsageUpdated()
    {
        void Apply()
        {
            RaiseClaudeUsageChanged();
            CheckClaudeUsageAlerts();
        }

        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.BeginInvoke(Apply);
        else
            Apply();
    }

    /// <summary>Segarkan semua teks dan bar (juga dipanggil tiap menit agar hitung mundur reset tetap akurat).</summary>
    public void RaiseClaudeUsageChanged()
    {
        OnPropertyChanged(nameof(EnableClaudeUsage));
        OnPropertyChanged(nameof(IsClaudeUsageDisabled));
        OnPropertyChanged(nameof(HasClaudeUsageData));
        OnPropertyChanged(nameof(HasClaudeUsageStatus));
        OnPropertyChanged(nameof(ClaudeUsageStatusText));
        OnPropertyChanged(nameof(ClaudeUsagePlanText));
        OnPropertyChanged(nameof(ClaudeUsageUpdatedText));
        OnPropertyChanged(nameof(ClaudeUsageBars));
        OnPropertyChanged(nameof(ClaudeUsageShares));
        OnPropertyChanged(nameof(HasClaudeUsageShares));
        OnPropertyChanged(nameof(ShowClaudeUsageBadge));
        OnPropertyChanged(nameof(ClaudeBadgeSessionText));
        OnPropertyChanged(nameof(ClaudeBadgeWeekText));
        OnPropertyChanged(nameof(ClaudeBadgeSessionBrush));
        OnPropertyChanged(nameof(ClaudeBadgeWeekBrush));
        OnPropertyChanged(nameof(HasClaudeBadgeSession));
        OnPropertyChanged(nameof(HasClaudeBadgeWeek));
        OnPropertyChanged(nameof(ClaudeBadgeTooltip));
    }
}
