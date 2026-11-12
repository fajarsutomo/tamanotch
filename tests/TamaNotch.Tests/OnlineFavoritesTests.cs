using System.Text.Json;
using TamaNotch.Models;
using TamaNotch.Services.LiveTv;
using TamaNotch.Services.Radio;
using TamaNotch.Services.Settings;
using Xunit;

namespace TamaNotch.Tests;

/// <summary>Favorit dari pencarian/katalog online harus bertahan setelah aplikasi dijalankan ulang.</summary>
public class OnlineFavoritesTests
{
    private sealed class MemSettings : ISettingsService
    {
        public AppSettings CurrentSettings { get; private set; } = new();
        public void Save() { }
        public void SetStartWithWindows(bool enable) { }
        public string GetAppDataPath() => Path.GetTempPath();
        public string GetLyricsCachePath() => Path.GetTempPath();
        public void ClearLyricsCache() { }

        /// <summary>Meniru tutup-buka aplikasi: pengaturan ditulis ke JSON lalu dibaca lagi.</summary>
        public MemSettings Restarted() => new()
        {
            CurrentSettings = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(CurrentSettings))!
        };
    }

    private static RadioStation OnlineStation() => new()
    {
        Id = "rb-abc-123",
        Name = "Radio Hasil Cari",
        StreamUrl = "https://stream.example/radio",
        CityOrCountry = "Surabaya, Indonesia",
        Genre = "pop",
        Bitrate = 96,
        Category = "Indonesia"
    };

    [Fact]
    public void Radio_OnlineFavorite_SurvivesRestart()
    {
        var settings = new MemSettings();
        using (var radio = new RadioService(settings))
        {
            radio.ToggleFavorite(OnlineStation());
        }

        var restarted = settings.Restarted();
        using var radio2 = new RadioService(restarted);

        var back = Assert.Single(radio2.Stations, s => s.Id == "rb-abc-123");
        Assert.True(back.IsFavorite);
        Assert.Equal("Radio Hasil Cari", back.Name);
        Assert.Equal("https://stream.example/radio", back.StreamUrl);
        Assert.False(back.IsCustom);
    }

    [Fact]
    public void Radio_UnfavoritingOnlineStation_RemovesItFromSavedData()
    {
        var settings = new MemSettings();
        var st = OnlineStation();
        using (var radio = new RadioService(settings))
        {
            radio.ToggleFavorite(st);   // favorit
            radio.ToggleFavorite(st);   // lepas
        }

        Assert.Empty(settings.CurrentSettings.SavedOnlineRadioStations);
        using var radio2 = new RadioService(settings.Restarted());
        Assert.DoesNotContain(radio2.Stations, s => s.Id == "rb-abc-123");
    }

    [Fact]
    public void Radio_BuiltInFavorite_IsNotDuplicatedIntoSavedData()
    {
        var settings = new MemSettings();
        using var radio = new RadioService(settings);

        radio.ToggleFavorite(radio.Stations[0]);

        Assert.Empty(settings.CurrentSettings.SavedOnlineRadioStations);
        Assert.Single(settings.CurrentSettings.FavoriteRadioStationIds);
    }

    [Fact]
    public void LiveTv_OnlineFavorite_SurvivesRestart()
    {
        var settings = new MemSettings();
        var channel = new LiveTvChannel
        {
            Id = "nano-xyz-789",
            Name = "TV Hasil Katalog",
            StreamUrl = "https://stream.example/tv.m3u8",
            CountryCode = "id",
            CountryName = "Indonesia",
            Category = "Berita"
        };
        using (var tv = new LiveTvService(settings))
        {
            tv.ToggleFavorite(channel);
        }

        using var tv2 = new LiveTvService(settings.Restarted());

        var back = Assert.Single(tv2.Channels, c => c.Id == "nano-xyz-789");
        Assert.True(back.IsFavorite);
        Assert.Equal("TV Hasil Katalog", back.Name);
        Assert.Equal("https://stream.example/tv.m3u8", back.StreamUrl);
    }

    [Fact]
    public void LiveTv_UnfavoritingOnlineChannel_RemovesItFromSavedData()
    {
        var settings = new MemSettings();
        var channel = new LiveTvChannel { Id = "nano-xyz-789", Name = "TV", StreamUrl = "https://s.example/a.m3u8" };
        using (var tv = new LiveTvService(settings))
        {
            tv.ToggleFavorite(channel);
            tv.ToggleFavorite(channel);
        }

        Assert.Empty(settings.CurrentSettings.SavedOnlineLiveTvChannels);
    }
}
