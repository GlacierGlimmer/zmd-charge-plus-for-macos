using System.Xml.Linq;
namespace EndfieldChargePlus.Settings;
public static class StartupManager
{
    internal const string Label = "com.glacierglimmer.endfieldchargeplus.macos";
    internal static string AutostartPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", Label + ".plist");
    internal static string BuildPlist(string executable) => new XDocument(
        new XDeclaration("1.0", "UTF-8", null),
        new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null),
        new XElement("plist", new XAttribute("version", "1.0"), new XElement("dict",
            new XElement("key", "Label"), new XElement("string", Label),
            new XElement("key", "ProgramArguments"), new XElement("array", new XElement("string", executable), new XElement("string", "--autostart")),
            new XElement("key", "RunAtLoad"), new XElement("true"),
            new XElement("key", "LimitLoadToSessionType"), new XElement("string", "Aqua")))).ToString();
    public static void Apply(bool enabled)
    {
        if (!OperatingSystem.IsMacOS()) return;
        if (!enabled) { File.Delete(AutostartPath); return; }
        string executable = Environment.ProcessPath ?? throw new InvalidOperationException("Application executable path is unavailable.");
        if (executable.StartsWith("/Volumes/", StringComparison.Ordinal) || executable.Contains("/AppTranslocation/", StringComparison.Ordinal))
            throw new InvalidOperationException(LocalizationManager.Text("请先把应用拖到 Applications 文件夹，再开启登录启动。", "Move the app into Applications before enabling launch at login."));
        if (!executable.Contains(".app/Contents/MacOS/", StringComparison.Ordinal))
            throw new InvalidOperationException("Launch at login requires the installed .app bundle.");
        Directory.CreateDirectory(Path.GetDirectoryName(AutostartPath)!);
        File.WriteAllText(AutostartPath + ".tmp", BuildPlist(executable));
        File.SetUnixFileMode(AutostartPath + ".tmp", UnixFileMode.UserRead | UnixFileMode.UserWrite);
        File.Move(AutostartPath + ".tmp", AutostartPath, overwrite:true);
    }
}
