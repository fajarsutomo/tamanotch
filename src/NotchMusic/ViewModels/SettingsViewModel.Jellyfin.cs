using System.Net.Http;
using System.Windows.Input;
using NotchMusic.Helpers;

namespace NotchMusic.ViewModels;

// Alamat server Jellyfin untuk jendela pemutar Jellyfin.
public partial class SettingsViewModel
{
    private string _jellyfinStatus = string.Empty;
    private bool _isCheckingJellyfin;

    public string JellyfinUrl
    {
        get => Settings.JellyfinUrl;
        set
        {
            SetSetting(Settings.JellyfinUrl, value?.Trim() ?? string.Empty, v => Settings.JellyfinUrl = v, nameof(JellyfinUrl), nameof(JellyfinResolvedUrl));
            JellyfinStatus = string.Empty;
        }
    }

    /// <summary>Alamat yang benar-benar dipakai setelah dirapikan (kosong/tidak valid kembali ke bawaan).</summary>
    public string JellyfinResolvedUrl => JellyfinUrl_Normalized.ToString();

    private Uri JellyfinUrl_Normalized => JellyfinAddress.Normalize(Settings.JellyfinUrl);

    public string JellyfinStatus
    {
        get => _jellyfinStatus;
        set => SetProperty(ref _jellyfinStatus, value);
    }

    public bool IsCheckingJellyfin
    {
        get => _isCheckingJellyfin;
        set => SetProperty(ref _isCheckingJellyfin, value);
    }

    public ICommand CheckJellyfinCommand { get; }
    public ICommand ResetJellyfinUrlCommand { get; }

    public async Task CheckJellyfinAsync(HttpClient? http = null)
    {
        if (IsCheckingJellyfin) return;
        IsCheckingJellyfin = true;
        JellyfinStatus = "Menghubungi server...";

        var ownsClient = http == null;
        http ??= new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        try
        {
            var result = await JellyfinServer.CheckAsync(JellyfinUrl_Normalized, http);
            JellyfinStatus = result.IsJellyfin
                ? $"✓ Terhubung: {result.ServerName} (Jellyfin {result.Version})"
                : $"✗ Gagal: {result.Error}";
        }
        finally
        {
            if (ownsClient) http.Dispose();
            IsCheckingJellyfin = false;
        }
    }
}
