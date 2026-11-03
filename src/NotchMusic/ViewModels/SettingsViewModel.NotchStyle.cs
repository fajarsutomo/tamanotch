using NotchMusic.Helpers;

namespace NotchMusic.ViewModels;

// Kustomisasi tampilan notch: warna mata/frame saat idle, bentuk frame idle, dan warna frame/ikon saat hover.
public partial class SettingsViewModel
{
    public IReadOnlyList<ColorChoice> IdlePalette => NeonPalette.Presets;
    public IReadOnlyList<ColorChoice> HoverPalette => NeonPalette.HoverChoices;

    // --- Idle ---
    public string IdleEyeColor
    {
        get => Settings.IdleEyeColor;
        set => SetSetting(Settings.IdleEyeColor, value, v => Settings.IdleEyeColor = v, nameof(IdleEyeColor));
    }

    public string IdleFrameColor
    {
        get => Settings.IdleFrameColor;
        set => SetSetting(Settings.IdleFrameColor, value, v => Settings.IdleFrameColor = v, nameof(IdleFrameColor));
    }

    public bool IdleFrameAutoSize
    {
        get => Settings.IdleFrameAutoSize;
        set => SetSetting(Settings.IdleFrameAutoSize, value, v => Settings.IdleFrameAutoSize = v, nameof(IdleFrameAutoSize), nameof(IsIdleFrameSizeEditable));
    }

    public bool IsIdleFrameSizeEditable => !Settings.IdleFrameAutoSize;

    public double IdleFrameWidth
    {
        get => Settings.IdleFrameWidth;
        set => SetSetting(Settings.IdleFrameWidth, Math.Clamp(Math.Round(value), 40, 240), v => Settings.IdleFrameWidth = v, nameof(IdleFrameWidth), nameof(IdleFrameWidthText));
    }

    public double IdleFrameHeight
    {
        get => Settings.IdleFrameHeight;
        set => SetSetting(Settings.IdleFrameHeight, Math.Clamp(Math.Round(value), 20, 90), v => Settings.IdleFrameHeight = v, nameof(IdleFrameHeight), nameof(IdleFrameHeightText));
    }

    public double IdleFrameRadiusLeft
    {
        get => Settings.IdleFrameRadiusLeft;
        set => SetSetting(Settings.IdleFrameRadiusLeft, Math.Clamp(Math.Round(value), 0, 60), v => Settings.IdleFrameRadiusLeft = v, nameof(IdleFrameRadiusLeft), nameof(IdleFrameRadiusLeftText));
    }

    public double IdleFrameRadiusRight
    {
        get => Settings.IdleFrameRadiusRight;
        set => SetSetting(Settings.IdleFrameRadiusRight, Math.Clamp(Math.Round(value), 0, 60), v => Settings.IdleFrameRadiusRight = v, nameof(IdleFrameRadiusRight), nameof(IdleFrameRadiusRightText));
    }

    public string IdleFrameWidthText => $"{IdleFrameWidth:0} px";
    public string IdleFrameHeightText => $"{IdleFrameHeight:0} px";
    public string IdleFrameRadiusLeftText => $"{IdleFrameRadiusLeft:0} px";
    public string IdleFrameRadiusRightText => $"{IdleFrameRadiusRight:0} px";

    // --- Hover (island) ---
    public bool HoverSameAsIdle
    {
        get => Settings.HoverSameAsIdle;
        set => SetSetting(Settings.HoverSameAsIdle, value, v => Settings.HoverSameAsIdle = v, nameof(HoverSameAsIdle), nameof(IsHoverColorEditable));
    }

    public bool IsHoverColorEditable => !Settings.HoverSameAsIdle;

    public string HoverFrameColor
    {
        get => Settings.HoverFrameColor;
        set => SetSetting(Settings.HoverFrameColor, value, v => Settings.HoverFrameColor = v, nameof(HoverFrameColor));
    }

    public string HoverRingColor
    {
        get => Settings.HoverRingColor;
        set => SetSetting(Settings.HoverRingColor, value, v => Settings.HoverRingColor = v, nameof(HoverRingColor));
    }
}
