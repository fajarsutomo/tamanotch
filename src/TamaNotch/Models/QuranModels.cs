using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TamaNotch.Models;

public enum QuranPlaybackState
{
    Stopped,
    Loading,
    Playing,
    Paused,
    Error
}

/// <summary>Apa yang terjadi saat satu surah selesai diputar.</summary>
public enum QuranAfterPlayback
{
    Stop,
    Next,
    Random
}

public class QuranSurah : INotifyPropertyChanged
{
    private bool _isCurrent;
    private bool _isPlaying;

    public int Number { get; set; }
    public string NameSimple { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string TranslatedName { get; set; } = string.Empty;
    public int VersesCount { get; set; }
    public string RevelationPlace { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsCurrent
    {
        get => _isCurrent;
        set { _isCurrent = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); }
    }

    /// <summary>Mis. "2. Al-Baqarah".</summary>
    [JsonIgnore]
    public string Title => $"{Number}. {NameSimple}";

    [JsonIgnore]
    public string PlaceText => string.Equals(RevelationPlace, "makkah", StringComparison.OrdinalIgnoreCase)
        ? "Makkiyah"
        : string.Equals(RevelationPlace, "madinah", StringComparison.OrdinalIgnoreCase) ? "Madaniyah" : string.Empty;

    [JsonIgnore]
    public string Subtitle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(TranslatedName)) parts.Add(TranslatedName);
            if (VersesCount > 0) parts.Add($"{VersesCount} ayat");
            if (PlaceText.Length > 0) parts.Add(PlaceText);
            return string.Join(" • ", parts);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
