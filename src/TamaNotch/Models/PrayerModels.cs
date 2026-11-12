using TamaNotch.Helpers;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TamaNotch.Models;

public enum PrayerReminderType
{
    Before,
    AtPrayerTime,
    After,

    /// <summary>Pengingat sahur sebelum Imsak (hari puasa).</summary>
    Sahur
}

public class CityLocation
{
    public string Name { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Timezone { get; set; } = 7.0;
    public string BmkgAdm4Code { get; set; } = "31.71.01.1001";

    [JsonIgnore]
    public string DisplayName => $"{Name} ({Province})";

    public override string ToString() => DisplayName;

    public static CityLocation FindBestMatchOrNearest(string? cityName, double latitude, double longitude)
    {
        var cities = GetIndonesianCities();
        if (!string.IsNullOrWhiteSpace(cityName))
        {
            var exact = cities.FirstOrDefault(c => c.Name.Equals(cityName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            var partial = cities.FirstOrDefault(c =>
                c.Name.Contains(cityName.Trim(), StringComparison.OrdinalIgnoreCase) ||
                cityName.Trim().Contains(c.Name, StringComparison.OrdinalIgnoreCase));
            if (partial != null) return partial;
        }

        // Find nearest by Haversine / Euclidean distance
        CityLocation? nearest = null;
        double minDistanceSq = double.MaxValue;
        foreach (var city in cities)
        {
            double dLat = city.Latitude - latitude;
            double dLon = city.Longitude - longitude;
            double distSq = dLat * dLat + dLon * dLon;
            if (distSq < minDistanceSq)
            {
                minDistanceSq = distSq;
                nearest = city;
            }
        }

        return nearest ?? cities[0];
    }

    private static readonly Lazy<List<CityLocation>> BuiltInCities = new(LoadBuiltInCities);

    private static List<CityLocation> LoadBuiltInCities()
    {
        var cities = CatalogLoader.Load<CityLocation>("cities-id.json");
        if (cities.Count == 0)
        {
            // Berkas katalog hilang/rusak: tetap sediakan satu kota agar pencarian kota terdekat tidak gagal.
            cities.Add(new CityLocation { Name = "Jakarta", Province = "DKI Jakarta", Latitude = -6.2088, Longitude = 106.8456, Timezone = 7.0, BmkgAdm4Code = "31.71.01.1001" });
        }
        return cities;
    }

    /// <summary>Daftar kota bawaan (dari Assets/Data/cities-id.json). Mengembalikan salinan baru tiap pemanggilan.</summary>
    public static List<CityLocation> GetIndonesianCities()
    {
        return BuiltInCities.Value
            .Select(c => new CityLocation
            {
                Name = c.Name,
                Province = c.Province,
                Latitude = c.Latitude,
                Longitude = c.Longitude,
                Timezone = c.Timezone,
                BmkgAdm4Code = c.BmkgAdm4Code
            })
            .ToList();
    }
}

public class PrayerTimeItem
{
    public string Name { get; set; } = string.Empty;
    public string ArabicName { get; set; } = string.Empty;
    public TimeSpan Time { get; set; }
    public string FormattedTime { get; set; } = string.Empty;
    public bool IsMandatoryPrayer { get; set; } = true;
    public bool IsNext { get; set; }
    public bool IsCurrent { get; set; }
    public string CountdownText { get; set; } = string.Empty;
    public string StatusBadge { get; set; } = string.Empty;
}

public class PrayerSchedule
{
    public DateTime Date { get; set; }
    public string HijriDate { get; set; } = string.Empty;
    public string CityName { get; set; } = string.Empty;
    public List<PrayerTimeItem> Items { get; set; } = new();
    public PrayerTimeItem? NextPrayer { get; set; }
    public PrayerTimeItem? CurrentPrayer { get; set; }
    public TimeSpan TimeUntilNextPrayer { get; set; }
    public string CountdownSummary => NextPrayer != null ? $"{NextPrayer.Name} {NextPrayer.FormattedTime} ({FormatTimeSpan(TimeUntilNextPrayer)})" : "-";

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalMinutes < 1) return "< 1 mnt";
        if (ts.TotalHours < 1) return $"-{Math.Max(1, (int)ts.TotalMinutes)}m";
        return $"-{(int)ts.TotalHours}j {ts.Minutes}m";
    }
}

public class PrayerNotificationEvent
{
    public string PrayerName { get; set; } = string.Empty;
    public string PrayerTimeFormatted { get; set; } = string.Empty;
    public PrayerReminderType Type { get; set; }
    public int Minutes { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool AutoPauseRequested { get; set; }
}
