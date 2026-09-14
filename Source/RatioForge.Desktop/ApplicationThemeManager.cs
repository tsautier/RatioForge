namespace RatioForge.Desktop;

using Avalonia;
using Avalonia.Styling;

/// <summary>Applies the persisted appearance preference to the Avalonia application.</summary>
public static class ApplicationThemeManager
{
    public static void Apply(ApplicationThemeMode mode)
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant = Resolve(mode);
        }
    }

    public static ThemeVariant Resolve(ApplicationThemeMode mode) => mode switch
    {
        ApplicationThemeMode.Dark => ThemeVariant.Dark,
        ApplicationThemeMode.Light => ThemeVariant.Light,
        _ => ThemeVariant.Default,
    };
}
