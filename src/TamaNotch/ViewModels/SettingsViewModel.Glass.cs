using TamaNotch.Helpers;
using TamaNotch.Services.Theme;

namespace TamaNotch.ViewModels;

/// <summary>Pengaturan lanjutan tema Liquid Glass (blur island, kepekatan kaca, blur jendela lain).</summary>
public partial class SettingsViewModel
{
    public bool IsGlassTheme => AppTheme == ThemeService.Glass;

    public bool GlassIslandBlur
    {
        get => Settings.GlassIslandBlur;
        set => SetGlass(Settings.GlassIslandBlur, value, v => Settings.GlassIslandBlur = v, nameof(GlassIslandBlur));
    }

    public int GlassBlurStrength
    {
        get => Math.Clamp(Settings.GlassBlurStrength, GlassOptions.MinBlur, GlassOptions.MaxBlur);
        set => SetGlass(GlassBlurStrength, Math.Clamp(value, GlassOptions.MinBlur, GlassOptions.MaxBlur),
            v => Settings.GlassBlurStrength = v, nameof(GlassBlurStrength), nameof(GlassBlurStrengthText));
    }

    public string GlassBlurStrengthText => GlassBlurStrength switch
    {
        <= 2 => $"{GlassBlurStrength} (tipis)",
        <= 6 => $"{GlassBlurStrength} (sedang)",
        _ => $"{GlassBlurStrength} (kuat)"
    };

    public int GlassTintPercent
    {
        get => Math.Clamp(Settings.GlassTintPercent, GlassOptions.MinTint, GlassOptions.MaxTint);
        set => SetGlass(GlassTintPercent, Math.Clamp(value, GlassOptions.MinTint, GlassOptions.MaxTint),
            v => Settings.GlassTintPercent = v, nameof(GlassTintPercent), nameof(GlassTintPercentText));
    }

    public string GlassTintPercentText => GlassTintPercent == 0 ? "0% (paling bening)" : $"{GlassTintPercent}%";

    public bool GlassAdaptiveTint
    {
        get => Settings.GlassAdaptiveTint;
        set => SetGlass(Settings.GlassAdaptiveTint, value, v => Settings.GlassAdaptiveTint = v, nameof(GlassAdaptiveTint));
    }

    public int GlassRefreshMs
    {
        get => Math.Clamp(Settings.GlassRefreshMs, GlassOptions.MinRefreshMs, GlassOptions.MaxRefreshMs);
        set => SetGlass(GlassRefreshMs, Math.Clamp(value, GlassOptions.MinRefreshMs, GlassOptions.MaxRefreshMs),
            v => Settings.GlassRefreshMs = v, nameof(GlassRefreshMs), nameof(GlassRefreshText));
    }

    public string GlassRefreshText => $"{GlassRefreshMs} ms ({1000.0 / GlassRefreshMs:0.#}x per detik)";

    public bool GlassWindowBlur
    {
        get => Settings.GlassWindowBlur;
        set => SetGlass(Settings.GlassWindowBlur, value, v => Settings.GlassWindowBlur = v, nameof(GlassWindowBlur));
    }

    public RelayCommand ResetGlassCommand => _resetGlassCommand ??= new RelayCommand(_ =>
    {
        Settings.GlassIslandBlur = true;
        Settings.GlassBlurStrength = GlassOptions.DefaultBlur;
        Settings.GlassTintPercent = GlassOptions.DefaultTint;
        Settings.GlassAdaptiveTint = true;
        Settings.GlassRefreshMs = GlassOptions.DefaultRefreshMs;
        Settings.GlassWindowBlur = true;
        _settingsService.Save();
        GlassOptions.Load(Settings);
        foreach (var name in new[]
                 {
                     nameof(GlassIslandBlur), nameof(GlassBlurStrength), nameof(GlassBlurStrengthText), nameof(GlassTintPercent),
                     nameof(GlassTintPercentText), nameof(GlassAdaptiveTint), nameof(GlassRefreshMs), nameof(GlassRefreshText),
                     nameof(GlassWindowBlur)
                 })
            OnPropertyChanged(name);
    });

    private RelayCommand? _resetGlassCommand;

    private void SetGlass<T>(T current, T value, Action<T> apply, params string[] names)
    {
        SetSetting(current, value, apply, names);
        GlassOptions.Load(Settings); // terapkan langsung ke island dan jendela yang terbuka
    }
}
