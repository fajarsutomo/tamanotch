using Windows.Media.Core;
using Windows.Media.Playback;

namespace NotchMusic.Services.Quran;

/// <summary>Pemutar nyata berbasis <see cref="MediaPlayer"/> Windows, sama seperti pemutar radio.</summary>
public sealed class MediaQuranAudioPlayer : IQuranAudioPlayer
{
    private readonly object _lock = new();
    private MediaPlayer? _player;
    private MediaSource? _source;
    private double _volume = 0.8;
    private bool _muted;
    private int _openVersion;

    public event Action? Opened;
    public event Action? Ended;
    public event Action<string>? Failed;

    public TimeSpan Position
    {
        get
        {
            try { lock (_lock) return _player?.PlaybackSession.Position ?? TimeSpan.Zero; }
            catch { return TimeSpan.Zero; }
        }
    }

    public TimeSpan Duration
    {
        get
        {
            try { lock (_lock) return _player?.PlaybackSession.NaturalDuration ?? TimeSpan.Zero; }
            catch { return TimeSpan.Zero; }
        }
    }

    public double Volume
    {
        get => _volume;
        set { _volume = Math.Clamp(value, 0.0, 1.0); ApplyVolume(); }
    }

    public bool IsMuted
    {
        get => _muted;
        set { _muted = value; ApplyVolume(); }
    }

    public void Open(Uri uri)
    {
        int version = Interlocked.Increment(ref _openVersion);
        lock (_lock)
        {
            DisposePlayerUnlocked();

            var player = new MediaPlayer
            {
                AudioCategory = MediaPlayerAudioCategory.Media,
                IsMuted = _muted,
                Volume = _muted ? 0.0 : _volume
            };

            try
            {
                // Jangan menimpa sesi media Windows (GSMTC) yang dipakai pemutar musik utama.
                player.CommandManager.IsEnabled = false;
            }
            catch { }

            player.MediaOpened += (s, e) => { if (version == Volatile.Read(ref _openVersion)) Opened?.Invoke(); };
            player.MediaEnded += (s, e) => { if (version == Volatile.Read(ref _openVersion)) Ended?.Invoke(); };
            player.MediaFailed += (s, e) => { if (version == Volatile.Read(ref _openVersion)) Failed?.Invoke(e.ErrorMessage ?? e.Error.ToString()); };

            var source = MediaSource.CreateFromUri(uri);
            _source = source;
            _player = player;
            player.Source = source;
            player.Play();
        }
    }

    public void Pause()
    {
        try { lock (_lock) _player?.Pause(); } catch { }
    }

    public void Resume()
    {
        try { lock (_lock) _player?.Play(); } catch { }
    }

    public void Stop()
    {
        Interlocked.Increment(ref _openVersion);
        lock (_lock) DisposePlayerUnlocked();
    }

    public void Seek(TimeSpan position)
    {
        try { lock (_lock) { if (_player != null) _player.PlaybackSession.Position = position; } }
        catch { }
    }

    private void ApplyVolume()
    {
        try
        {
            lock (_lock)
            {
                if (_player == null) return;
                _player.IsMuted = _muted;
                _player.Volume = _muted ? 0.0 : _volume;
            }
        }
        catch { }
    }

    private void DisposePlayerUnlocked()
    {
        var player = _player;
        var source = _source;
        _player = null;
        _source = null;

        try
        {
            if (player != null)
            {
                player.Pause();
                player.Source = null;
                player.Dispose();
            }
        }
        catch { }

        try { source?.Dispose(); } catch { }
    }

    public void Dispose() => Stop();
}
