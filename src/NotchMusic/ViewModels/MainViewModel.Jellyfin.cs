using System.Windows.Input;

namespace NotchMusic.ViewModels;

public partial class MainViewModel
{
    public event Action? RequestOpenJellyfin;

    /// <summary>Jellyfin mulai memutar; pemutar web lain perlu dijeda.</summary>
    public event Action? PlaybackStartedOnJellyfin;

    public ICommand OpenJellyfinCommand { get; }

    public void NotifyJellyfinPlaying()
    {
        // Spotify (aplikasi), radio, dan Live TV tidak boleh bertumpuk dengan Jellyfin.
        _ = _mediaService.PauseSessionByKeywordAsync("spotify");
        if (IsRadioPlaying || IsRadioBuffering)
        {
            _radioService.Stop();
        }
        if (IsLiveTvPlaying || IsLiveTvBuffering)
        {
            _liveTvService.Stop();
        }
        StopQuranForOtherPlayback();

        PlaybackStartedOnJellyfin?.Invoke();
    }
}
