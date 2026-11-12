using TamaNotch.Services.Dns;

namespace TamaNotch.ViewModels;

// DNS-over-HTTPS untuk jendela WebView2 (melewati DNS jaringan). Berlaku setelah aplikasi dimulai ulang.
public partial class SettingsViewModel
{
    public bool DohEnabled
    {
        get => Settings.DohEnabled;
        set => SetSetting(Settings.DohEnabled, value, v => Settings.DohEnabled = v, nameof(DohEnabled));
    }

    public IReadOnlyList<string> DohProviders { get; } = new[] { DohResolver.CloudflareProvider, DohResolver.GoogleProvider };

    public string DohProvider
    {
        get => Settings.DohProvider;
        set => SetSetting(Settings.DohProvider, value, v => Settings.DohProvider = v, nameof(DohProvider));
    }
}
