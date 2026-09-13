namespace RatioForge.Desktop;

using Avalonia;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--smoke-test", StringComparer.Ordinal))
        {
            Console.WriteLine($"RatioForge {ThisAssemblyVersion()} | {ClientProfileCatalog.All.Count} profiles | {Environment.OSVersion.Platform}");
            return ClientProfileCatalog.All.Count > 0 ? 0 : 1;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();

    private static string ThisAssemblyVersion() =>
        typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "unknown";
}
