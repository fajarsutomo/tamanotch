namespace NotchMusic.Services.Quran;

/// <summary>Pemutar audio di balik <see cref="QuranService"/>; dipisah agar logika pemutaran dapat diuji tanpa audio nyata.</summary>
public interface IQuranAudioPlayer : IDisposable
{
    /// <summary>Audio sudah terbuka dan mulai diputar.</summary>
    event Action? Opened;

    /// <summary>Audio habis diputar.</summary>
    event Action? Ended;

    /// <summary>Audio gagal dibuka atau diputar; argumen berisi pesan galat.</summary>
    event Action<string>? Failed;

    TimeSpan Position { get; }

    TimeSpan Duration { get; }

    double Volume { get; set; }

    bool IsMuted { get; set; }

    /// <summary>Buka alamat audio dan langsung putar, menggantikan yang sedang berjalan.</summary>
    void Open(Uri uri);

    void Pause();

    void Resume();

    void Stop();

    void Seek(TimeSpan position);
}
