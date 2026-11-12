using TamaNotch.Services.Theme;

namespace TamaNotch.ViewModels;

public partial class SettingsViewModel
{
    public IReadOnlyList<ThemeChoice> ThemeChoices => ThemeService.Choices;

    public string AppTheme
    {
        get => ThemeService.Normalize(Settings.AppTheme);
        set
        {
            var theme = ThemeService.Normalize(value);
            SetSetting(ThemeService.Normalize(Settings.AppTheme), theme, v => Settings.AppTheme = v, nameof(AppTheme), nameof(IsGlassTheme));
            ThemeService.Apply(theme);
        }
    }
}
