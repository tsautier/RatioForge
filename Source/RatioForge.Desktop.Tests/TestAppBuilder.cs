namespace RatioForge.Desktop.Tests;

using Avalonia;
using Avalonia.Headless;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
