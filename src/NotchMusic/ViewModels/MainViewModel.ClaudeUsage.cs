using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using System.Windows.Input;
using System.Windows.Media;
using NotchMusic.Helpers;
using NotchMusic.Services.ClaudeUsage;

namespace NotchMusic.ViewModels;

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
        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.BeginInvoke(RaiseClaudeUsageChanged);
        else
            RaiseClaudeUsageChanged();
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
    }
}
