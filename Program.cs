using Avalonia;
using PdfSharp;

namespace PdfTool;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        PdfSharpLicense.SetAgplLicense();

        if (WantConsole(args))
        {
            ConsoleMenu.Run();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();

    static bool WantConsole(string[] args)
    {
        if (args.Any(a => a == "--console"))
            return true;

        if (!OperatingSystem.IsLinux())
            return false;

        return string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DISPLAY"))
            && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY"));
    }
}
