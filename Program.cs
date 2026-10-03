using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using EndfieldChargePlus.Diagnostics;
namespace EndfieldChargePlus;
internal static class Program
{
    internal static bool IsAutoStart { get; private set; }
    internal static string MacInstanceName { get; } = "ECPMac-" + Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(Interop.AppPaths.DataDirectory)))[..24];
    [STAThread]
    public static void Main(string[] args)
    {
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("This edition requires macOS.");
        IsAutoStart = args.Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase));
        using var mutex = new Mutex(true, MacInstanceName, out bool primary);
        if (!primary)
        {
            if (!IsAutoStart)
            {
                try
                {
                    using var pipe = new NamedPipeClientStream(".", MacInstanceName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
                    pipe.Connect(5000); pipe.WriteByte(1);
                }
                catch { /* The existing instance may still be initializing or shutting down. */ }
            }
            return;
        }
        AppLog.Initialize();
        AppLog.Info($"{ProductInfo.Name} started. Version={typeof(Program).Assembly.GetName().Version}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => AppLog.Fatal("Unhandled exception.", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => { AppLog.Error("Unobserved task exception.", e.Exception); e.SetObserved(); };
        try
        {
            Customization.MacVariableCatalog.Refresh();
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex) { AppLog.Fatal("Application startup/runtime failed.", ex); throw; }
        finally { mutex.ReleaseMutex(); AppLog.Info("Application ended."); }
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect()
        .With(new MacOSPlatformOptions { ShowInDock=false, DisableDefaultApplicationMenuItems=true })
        .WithInterFont().LogToTrace();
}
