using System.Windows.Input;
using TamaNotch.Models;
using TamaNotch.Services.Location;

namespace TamaNotch.ViewModels;

public partial class SettingsViewModel
{
    private readonly ILocationService _locationService;

    public List<CityLocation> IndonesianCities { get; } = CityLocation.GetIndonesianCities();

    // --- Pencarian lokasi presisi (kelurahan/desa/kecamatan) ---
    private string _locationSearchQuery = string.Empty;

    public string LocationSearchQuery
    {
        get => _locationSearchQuery;
        set => SetProperty(ref _locationSearchQuery, value);
    }

    public System.Collections.ObjectModel.ObservableCollection<BmkgLocationSearchResult> LocationSearchResults { get; } = new();

    public bool HasLocationSearchResults => LocationSearchResults.Count > 0;

    private bool _isSearchingLocation;

    public bool IsSearchingLocation
    {
        get => _isSearchingLocation;
        set => SetProperty(ref _isSearchingLocation, value);
    }

    public bool CanSearchLocation => _bmkgService != null;

    public string CurrentLocationText =>
        $"Lokasi saat ini: {Settings.PrayerCity} ({Settings.PrayerLatitude:F3}, {Settings.PrayerLongitude:F3})";

    public ICommand SearchLocationCommand { get; }

    public ICommand SelectLocationResultCommand { get; }

    public ICommand ClearLocationSearchCommand { get; }

    private CancellationTokenSource? _locationSearchCts;

    public async Task SearchLocationAsync()
    {
        if (_bmkgService == null) return;

        var query = LocationSearchQuery?.Trim() ?? string.Empty;
        if (query.Length < 3)
        {
            LocationDetectStatus = "Ketik minimal 3 huruf nama kelurahan, desa, kecamatan, atau kota.";
            return;
        }

        _locationSearchCts?.Cancel();
        _locationSearchCts?.Dispose();
        _locationSearchCts = new CancellationTokenSource();
        var token = _locationSearchCts.Token;

        IsSearchingLocation = true;
        LocationDetectStatus = $"Mencari '{query}'...";
        try
        {
            var results = await _bmkgService.SearchPreciseLocationsAsync(query, token);
            if (token.IsCancellationRequested) return;

            LocationSearchResults.Clear();
            foreach (var r in results) LocationSearchResults.Add(r);
            OnPropertyChanged(nameof(HasLocationSearchResults));

            LocationDetectStatus = results.Count == 0
                ? "Wilayah tidak ditemukan. Coba nama lain atau pilih kota dari daftar."
                : $"Ditemukan {results.Count} wilayah. Klik salah satu untuk dipakai.";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LocationDetectStatus = $"Pencarian gagal: {ex.Message}";
        }
        finally
        {
            IsSearchingLocation = false;
        }
    }

    public void SelectLocationResult(BmkgLocationSearchResult result)
    {
        var name = !string.IsNullOrWhiteSpace(result.VillageName)
            ? $"{result.VillageName}, {result.RegencyName}"
            : (!string.IsNullOrWhiteSpace(result.DistrictName) ? $"Kec. {result.DistrictName}, {result.RegencyName}" : result.RegencyName);

        _locationService.ApplyPrecise(name, result.Adm4Code, result.Latitude, result.Longitude, result.Timezone, LocationOrigin.Prayer);

        LocationSearchResults.Clear();
        OnPropertyChanged(nameof(HasLocationSearchResults));
        NotifyLocationChanged();
        LocationDetectStatus = $"✓ Lokasi diterapkan: {result.FullLabel}";
    }

    private void NotifyLocationChanged()
    {
        OnPropertyChanged(nameof(PrayerCity));
        OnPropertyChanged(nameof(SelectedCity));
        OnPropertyChanged(nameof(PrayerLatitude));
        OnPropertyChanged(nameof(PrayerLongitude));
        OnPropertyChanged(nameof(PrayerTimezone));
        OnPropertyChanged(nameof(TodayPrayerSchedule));
        OnPropertyChanged(nameof(CurrentLocationText));
    }

    // Null bila lokasi saat ini bukan salah satu kota di daftar (mis. kelurahan hasil pencarian).
    public CityLocation? SelectedCity
    {
        get => IndonesianCities.FirstOrDefault(c => c.Name.Equals(Settings.PrayerCity, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value != null && Settings.PrayerCity != value.Name)
            {
                _locationService.ApplyCity(value, LocationOrigin.Prayer);
                OnPropertyChanged();
                OnPropertyChanged(nameof(PrayerCity));
                OnPropertyChanged(nameof(PrayerLatitude));
                OnPropertyChanged(nameof(PrayerLongitude));
                OnPropertyChanged(nameof(PrayerTimezone));
                OnPropertyChanged(nameof(TodayPrayerSchedule));
                OnPropertyChanged(nameof(CurrentLocationText));
            }
        }
    }

    public string PrayerCity
    {
        get => Settings.PrayerCity;
        set
        {
            if (Settings.PrayerCity != value)
            {
                Settings.PrayerCity = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedCity));
                OnPropertyChanged(nameof(TodayPrayerSchedule));
            }
        }
    }

    public double PrayerLatitude
    {
        get => Settings.PrayerLatitude;
        set
        {
            if (Math.Abs(Settings.PrayerLatitude - value) > 0.0001)
            {
                Settings.PrayerLatitude = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TodayPrayerSchedule));
            }
        }
    }

    public double PrayerLongitude
    {
        get => Settings.PrayerLongitude;
        set
        {
            if (Math.Abs(Settings.PrayerLongitude - value) > 0.0001)
            {
                Settings.PrayerLongitude = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TodayPrayerSchedule));
            }
        }
    }

    public double PrayerTimezone
    {
        get => Settings.PrayerTimezone;
        set
        {
            if (Math.Abs(Settings.PrayerTimezone - value) > 0.01)
            {
                Settings.PrayerTimezone = value;
                _settingsService.Save();
                OnPropertyChanged();
                OnPropertyChanged(nameof(TodayPrayerSchedule));
            }
        }
    }

    public bool BmkgFollowPrayerCity
    {
        get => Settings.BmkgFollowPrayerCity;
        set
        {
            if (Settings.BmkgFollowPrayerCity != value)
            {
                Settings.BmkgFollowPrayerCity = value;
                _settingsService.Save();
                OnPropertyChanged();
            }
        }
    }

    private bool _isDetectingLocation;

    public bool IsDetectingLocation
    {
        get => _isDetectingLocation;
        set => SetProperty(ref _isDetectingLocation, value);
    }

    private string _locationDetectStatus = string.Empty;

    public string LocationDetectStatus
    {
        get => _locationDetectStatus;
        set => SetProperty(ref _locationDetectStatus, value);
    }

    public ICommand DetectLocationCommand { get; }
}
