using System.Xml.Linq;
namespace EndfieldChargePlus.Settings;
public sealed record StartupApplyResult(bool Succeeded,bool IsEnabled,bool IsPackaged=false,string State="Native",Exception? Error=null);
public static class StartupManager
{
    public static bool IsPackaged=>false;
    public static Task<StartupApplyResult> ReadStatusAsync()=>Task.FromResult(ReadRegistration(AutostartPath));
    public static Task<StartupApplyResult> ApplyAsync(bool enabled)
    {
        Apply(enabled);
        var state=ReadRegistration(AutostartPath);
        if(!state.Succeeded)throw state.Error ?? new IOException(LocalizationManager.Text("保存失败","Save failed"));
        return Task.FromResult(state with {Succeeded=state.IsEnabled==enabled});
    }

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
        if (!enabled) { RemoveRegistration(AutostartPath); return; }
        WriteRegistration(AutostartPath,Environment.ProcessPath ?? "");
    }

    internal static void WriteRegistration(string path,string executable)
    {
        if(string.IsNullOrWhiteSpace(executable) || executable.StartsWith("/Volumes/",StringComparison.Ordinal) ||
            executable.Contains("/AppTranslocation/",StringComparison.Ordinal) ||
            !executable.Replace('\\','/').Contains(".app/Contents/MacOS/",StringComparison.Ordinal))
            throw new InvalidOperationException(LocalizationManager.Text("请先把应用拖到 Applications 文件夹，再开启登录启动。", "Move the app into Applications before enabling launch at login."));
        if(!File.Exists(executable))throw new FileNotFoundException(LocalizationManager.Text("请先把应用拖到 Applications 文件夹，再开启登录启动。", "Move the app into Applications before enabling launch at login."),executable);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temp,BuildPlist(executable));
            if(OperatingSystem.IsMacOS())File.SetUnixFileMode(temp,UnixFileMode.UserRead|UnixFileMode.UserWrite);
            File.Move(temp,path,overwrite:true);
        }
        finally {if(File.Exists(temp))File.Delete(temp);}
    }

    internal static StartupApplyResult ReadRegistration(string path)
    {
        if(!File.Exists(path))return new(true,false);
        try
        {
            using var reader=System.Xml.XmlReader.Create(path,new System.Xml.XmlReaderSettings{DtdProcessing=System.Xml.DtdProcessing.Ignore,XmlResolver=null});
            var document=XDocument.Load(reader);var dict=document.Root?.Element("dict")??throw new IOException("Invalid LaunchAgent plist: "+path);
            XElement? Value(string key)=>dict.Elements("key").FirstOrDefault(x=>x.Value==key)?.ElementsAfterSelf().FirstOrDefault();
            bool valid=Value("Label")?.Value==Label && Value("RunAtLoad")?.Name.LocalName=="true";
            var args=Value("ProgramArguments")?.Elements("string").Select(x=>x.Value).ToArray()??Array.Empty<string>();
            valid &= args.Length==2 && args[1]=="--autostart";
            if(!valid)throw new IOException("Invalid LaunchAgent plist: "+path);
            return new(true,File.Exists(args[0]),State:File.Exists(args[0])?"Native":"MissingExecutable");
        }
        catch(Exception ex){return new(false,false,State:"ReadError",Error:ex);}
    }

    internal static void RemoveRegistration(string path)
    {
        try { File.Delete(path); }
        catch (DirectoryNotFoundException)
        {
            // A fresh macOS account may not have ~/Library/LaunchAgents yet.
            // No parent directory means there is no registration left to remove.
        }
    }
}
