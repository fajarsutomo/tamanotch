using NotchMusic.Services.AirQuality;

namespace NotchMusic.ViewModels;

public partial class MainViewModel
{
    private readonly IAirQualityService _airQualityService;

    public bool ShowAirQualityInIsland
    {
        get => Settings.ShowAirQualityInIsland;
        set
        {
            if (Settings.ShowAirQualityInIsland != value)
            {
                Settings.ShowAirQualityInIsland = value;
                _settingsService.Save();
                RefreshAirQuality(fetch: value);
            }
        }
    }

    /// <summary>Lencana AQI di pill cuaca island: hanya bila fitur aktif dan ada pembacaan yang belum basi.</summary>
    public bool ShowAirQualityBadge => EnableBmkg && Settings.ShowAirQualityInIsland
        && _airQualityService.Current is { } r && r.IsFresh(DateTime.Now);

    public string AirQualityBadgeText => _airQualityService.Current is { } r ? $"AQI {r.UsAqi}" : string.Empty;

    public System.Windows.Media.Brush AirQualityBrush => _airQualityService.Current is { } r
        ? BrushFor(AirQualityScale.Describe(r.UsAqi).Color)
        : System.Windows.Media.Brushes.Gray;

    public string AirQualityTooltip
    {
        get
        {
            if (_airQualityService.Current is not { } r) return string.Empty;
            var (label, _) = AirQualityScale.Describe(r.UsAqi);
            var pm = new List<string>();
            if (r.Pm25 is { } p25) pm.Add(FormattableString.Invariant($"PM2.5 {p25:0.#} µg/m³"));
            if (r.Pm10 is { } p10) pm.Add(FormattableString.Invariant($"PM10 {p10:0.#} µg/m³"));
            var detail = pm.Count > 0 ? $" ({string.Join(", ", pm)})" : string.Empty;
            return $"💨 Kualitas udara: {label}, AQI {r.UsAqi}{detail}\nEstimasi model (Open-Meteo/CAMS), bukan pembacaan stasiun";
        }
    }

    private static readonly Dictionary<string, System.Windows.Media.Brush> AirQualityBrushes = new();

    private static System.Windows.Media.Brush BrushFor(string hex)
    {
        if (!AirQualityBrushes.TryGetValue(hex, out var brush))
        {
            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
            brush = NotchMusic.Helpers.NeonPalette.Freeze(new System.Windows.Media.SolidColorBrush(color));
            AirQualityBrushes[hex] = brush;
        }
        return brush;
    }

    private void OnAirQualityUpdated()
    {
        void Notify() => RaiseAirQualityChanged();

        if (System.Windows.Application.Current?.Dispatcher is { } d && !d.CheckAccess())
            d.BeginInvoke(Notify);
        else
            Notify();
    }

    private void RaiseAirQualityChanged()
    {
        OnPropertyChanged(nameof(ShowAirQualityBadge));
        OnPropertyChanged(nameof(AirQualityBadgeText));
        OnPropertyChanged(nameof(AirQualityBrush));
        OnPropertyChanged(nameof(AirQualityTooltip));
        OnPropertyChanged(nameof(BmkgNotchTooltip));
    }

    /// <summary>Selaraskan lencana dengan setelan terkini; ambil data baru bila fitur aktif (mis. setelah lokasi berganti).</summary>
    private void RefreshAirQuality(bool fetch)
    {
        RaiseAirQualityChanged();
        if (fetch && EnableBmkg && Settings.ShowAirQualityInIsland) _ = _airQualityService.RefreshAsync();
    }
}
