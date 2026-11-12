using System;
using System.IO;
using System.Linq;
using TamaNotch.Helpers;
using TamaNotch.Models;
using TamaNotch.Services.LiveTv;
using TamaNotch.Services.Radio;
using TamaNotch.Services.Settings;
using Xunit;

namespace TamaNotch.Tests;

public class CatalogTests
{
    private class FakeSettings : ISettingsService
    {
        public AppSettings CurrentSettings { get; } = new();
        public void Save() { }
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => Path.Combine(Path.GetTempPath(), "tama_cat_" + Guid.NewGuid().ToString("N"));
        public string GetLyricsCachePath() => Path.GetTempPath();
        public void ClearLyricsCache() { }
    }

    [Fact]
    public void RadioStations_LoadFromJson_AreValidAndUnique()
    {
        var stations = new RadioService(new FakeSettings()).Stations;

        Assert.True(stations.Count >= 20);
        Assert.Equal(stations.Count, stations.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(stations, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Name));
            Assert.StartsWith("http", s.StreamUrl);
        });
    }

    [Fact]
    public void LiveTvChannelsAndCountries_LoadFromJson_AreValidAndUnique()
    {
        var service = new LiveTvService(new FakeSettings());

        Assert.True(service.Channels.Count >= 25);
        Assert.Equal(service.Channels.Count, service.Channels.Select(c => c.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(service.Channels, c => Assert.False(string.IsNullOrWhiteSpace(c.StreamUrl)));

        Assert.Equal("ALL", service.Countries[0].Code);
        Assert.Contains(service.Countries, c => c.Code == "id" && c.Name == "Indonesia");
    }

    [Fact]
    public void Cities_LoadFromJson_HaveCoordinatesAndAdm4Codes()
    {
        var cities = CityLocation.GetIndonesianCities();

        Assert.True(cities.Count >= 40);
        Assert.Equal(cities.Count, cities.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(cities, c =>
        {
            Assert.False(string.IsNullOrWhiteSpace(c.BmkgAdm4Code));
            Assert.InRange(c.Latitude, -12, 7);
            Assert.InRange(c.Longitude, 94, 142);
        });
        var jakarta = cities.Single(c => c.Name == "Jakarta");
        Assert.Equal("31.71.01.1001", jakarta.BmkgAdm4Code);
    }

    [Fact]
    public void GetIndonesianCities_ReturnsIndependentCopies()
    {
        var first = CityLocation.GetIndonesianCities();
        first[0].Name = "DIUBAH";
        first.RemoveAt(1);

        var second = CityLocation.GetIndonesianCities();

        Assert.NotEqual("DIUBAH", second[0].Name);
        Assert.Equal(first.Count + 1, second.Count);
    }

    [Fact]
    public void Loader_MissingOrCorruptFile_ReturnsEmptyList()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tama_loader_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Empty(CatalogLoader.LoadFile<CityLocation>(Path.Combine(dir, "tidak-ada.json")));

            var corrupt = Path.Combine(dir, "rusak.json");
            File.WriteAllText(corrupt, "[{ ini bukan json");
            Assert.Empty(CatalogLoader.LoadFile<CityLocation>(corrupt));

            var valid = Path.Combine(dir, "valid.json");
            File.WriteAllText(valid, "[{\"name\":\"Kota Uji\",\"province\":\"Prov\",\"latitude\":-1.5,\"longitude\":110,\"timezone\":7,\"bmkgAdm4Code\":\"11.11.11.1111\"}]");
            var loaded = CatalogLoader.LoadFile<CityLocation>(valid);
            Assert.Single(loaded);
            Assert.Equal("Kota Uji", loaded[0].Name);
            Assert.Equal("11.11.11.1111", loaded[0].BmkgAdm4Code);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
