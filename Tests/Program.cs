using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.LogicalTree;
using EndfieldChargePlus;
using EndfieldChargePlus.Customization;
using EndfieldChargePlus.Interop;
using EndfieldChargePlus.Settings;
using EndfieldChargePlus.Views;

internal static class Tests
{
    internal static int Count;
    internal static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Count++; Console.WriteLine("PASS " + message);
    }
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            UnitTests();
            if (OperatingSystem.IsMacOS())
            {
                MacVariableCatalog.Refresh();
                NativeTests().GetAwaiter().GetResult();
                AppBuilder.Configure<AuditApp>().UsePlatformDetect().WithInterFont()
                    .With(new MacOSPlatformOptions { ShowInDock=false }).StartWithClassicDesktopLifetime(args);
            }
            Console.WriteLine($"{(Environment.ExitCode == 0 ? "Passed" : "FAILED after")} {Count} assertions on {RuntimeInformation.OSDescription} / {RuntimeInformation.ProcessArchitecture}.");
            return Environment.ExitCode;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    private static void UnitTests()
    {
        var p=new MacVariableProvider(); var values=new Dictionary<string,object?>();
        using var first=JsonDocument.Parse("""{"__cpu_ticks":[4294967290,50,100,0],"__network_samples":{"en0":[1000,500]}}""");
        p.Consume(first.RootElement,values,100);
        Check(!values.ContainsKey("cpu.usage") && !values.ContainsKey("network.download_bps"),"Rates require two measured samples");
        using var second=JsonDocument.Parse("""{"__cpu_ticks":[4,60,180,0],"__network_samples":{"en0":[2000,600]}}""");
        p.Consume(second.RootElement,values,100+Stopwatch.Frequency);
        Check(Math.Abs(Convert.ToDouble(values["cpu.usage"])-20)<0.001,"CPU tick wrap and normalized utilization");
        Check(Convert.ToDouble(values["network.download_bps"])==1000,"64-bit interface byte deltas");
        using var reset=JsonDocument.Parse("""{"__network_samples":{"en0":[1,1],"en1":[999999,99999]}}""");
        p.Consume(reset.RootElement,values,100+2*Stopwatch.Frequency);
        Check(Convert.ToDouble(values["network.total_bps"])==0,"Interface reset/hotplug does not create traffic spikes");
        var plist=XDocument.Parse(StartupManager.BuildPlist("/Applications/A & B.app/Contents/MacOS/EndfieldChargePlus"));
        Check(plist.Descendants("array").Single().Elements("string").First().Value.Contains("A & B.app"),"LaunchAgent XML preserves spaces and special characters");
        using var releases=JsonDocument.Parse("""
        [
        {"tag_name":"v9.0.0","draft":false,"prerelease":true,"assets":[{"name":"EndfieldChargePlusForMacOS-v9.0.0-osx-arm64.dmg"}]},
        {"tag_name":"v8.0.0","draft":true,"prerelease":false,"assets":[{"name":"EndfieldChargePlusForMacOS-v8.0.0-osx-arm64.dmg"}]},
        {"tag_name":"v2.0.0","draft":false,"prerelease":false,"assets":[{"name":"EndfieldChargePlusForMacOS-v2.0.0-osx-x64.dmg"}]},
        {"tag_name":"v1.0.0","draft":false,"prerelease":false,"assets":[{"name":"EndfieldChargePlusForMacOS-v1.0.0-osx-arm64.dmg"}]}
        ]
        """);
        Check(MacUpdateRelease.SelectTag(releases.RootElement,Architecture.Arm64)=="v1.0.0","ARM update ignores Intel-only, draft and prerelease assets");
        Check(MacUpdateRelease.SelectTag(releases.RootElement,Architecture.X64)=="v2.0.0","Intel update selects Intel DMG");
        foreach (string key in new[]{"system.defender_status","system.bitlocker_status","cpu.dpc_time","gpu.usage","gpu.memory_used_bytes","battery.remaining_mwh","security.tpm.present"})
            Check(!MacVariableCatalog.Supports(key),"Unsupported metric removed: "+key);
        var imported = MacVariableCatalog.RemoveUnsupportedReferences(new HudProfile { PrimaryTemplate="{gpu.usage|0} {system.time}", ProgressVariable="gpu.usage", ColorRules=new(){new(){Variable="gpu.usage"}} });
        Check(imported.PrimaryTemplate==" {system.time}" && imported.ProgressVariable=="" && imported.ColorRules.Count==0,"Unsupported imported expressions and color rules are removed");
        Check(SecretStore.Unprotect("windows-dpapi-blob")=="","Foreign encrypted key is not treated as plaintext");
    }
    private static async Task NativeTests()
    {
        using var hub=new VariableHub();
        var keys=new[]{"cpu.usage","memory.total_bytes","disk.system.total_bytes","network.download_bps","system.os_version"};
        var v=await hub.SnapshotAsync(CustomHudSettings.CreateDefault(),keys);
        foreach(var key in keys) Check(v.ContainsKey(key),"Native collector: "+key);
        Check(Convert.ToDouble(v["memory.total_bytes"])>0,"Physical memory is measurable");
        Check(Convert.ToDouble(v["cpu.usage"]) is >=0 and <=100,"CPU percent within bounds");
        Check(Convert.ToDouble(v["disk.system.used_bytes"])+Convert.ToDouble(v["disk.system.free_bytes"])==Convert.ToDouble(v["disk.system.total_bytes"]),"APFS capacity invariant");
        Check(!v.ContainsKey("gpu.usage"),"No synthetic GPU utilization");
        string testKey="ecp-ci-"+Guid.NewGuid().ToString("N");
        string protectedKey=SecretStore.Protect(testKey);
        Check(!protectedKey.Contains(testKey) && SecretStore.Unprotect(protectedKey)==testKey,"Native login Keychain secret round trip");
        using var http=new HttpClient(new FixtureHttp());
        using var integrations=new VariableHub(http);
        var configured=CustomHudSettings.CreateDefault() with
        {
            DeepSeekApiKeyProtected=protectedKey,
            HttpSources=new(){new(){Name="audit",Url="https://fixture.invalid/data",Enabled=true,
                Fields=new(){new(){Variable="value",JsonPath="metrics[0].value"}}}}
        };
        var onlineKeys=VariableCatalog.AllBuiltIns.Where(d=>d.Key.StartsWith("deepseek.") || d.Key.StartsWith("network.public_")).Select(d=>d.Key).Append("custom.audit.value").ToList();
        var online=await integrations.SnapshotAsync(configured,onlineKeys);
        foreach(var key in onlineKeys) Check(online.TryGetValue(key,out var value) && value is not null,"Online integration collector (fixture): "+key);
        Check(Convert.ToDouble(online["deepseek.balance"])==12.5 && Convert.ToDouble(online["custom.audit.value"])==42,"HTTP/JSON and DeepSeek values are parsed from responses");
        // Loopback probe is independent of Internet connectivity.
        var listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
        try
        {
            int port=((IPEndPoint)listener.LocalEndpoint).Port;
            var pending=listener.AcceptTcpClientAsync();
            v=await hub.SnapshotAsync(CustomHudSettings.CreateDefault(),new[]{"probe.online","probe.latency_ms"},pingTarget:"127.0.0.1",probeProtocol:"TCP",probePort:port);
            using var connection=await pending.WaitAsync(TimeSpan.FromSeconds(5));
            Check(v["probe.online"] is true && Convert.ToDouble(v["probe.latency_ms"])>=0,"TCP probe actually connects on macOS");
        }
        finally { listener.Stop(); }
    }
}

internal sealed class AuditApp : Application
{
    public override void Initialize() { Styles.Add(new FluentTheme()); RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Dark; }
    public override void OnFrameworkInitializationCompleted()
    {
        var desktop=(IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        desktop.ShutdownMode=ShutdownMode.OnExplicitShutdown;
        Dispatcher.UIThread.Post(async () =>
        {
            try { await Audit(desktop); }
            catch(Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode=1; }
            finally { desktop.Shutdown(Environment.ExitCode); }
        });
        base.OnFrameworkInitializationCompleted();
    }
    private static async Task Audit(IClassicDesktopStyleApplicationLifetime desktop)
    {
        Directory.CreateDirectory("artifacts/audit");
        var settings=SettingsManager.CreateDefaults();
        var hud=new HudWindow(); hud.ApplySettings(settings);
        using var runtime=new CustomHudRuntime(hud); runtime.ApplySettings(settings);
        var window=new SettingsWindow(settings,hud,runtime); desktop.MainWindow=window; window.Show();
        await Task.Delay(600);
        await window.Clipboard!.SetTextAsync("ECP macOS variable audit");
        using var hub=new VariableHub();
        var catalog=VariableCatalog.AllBuiltIns;
        // Conditional online integrations are audited separately, never mistaken for hardware support.
        var local=catalog.Where(d => !d.Key.StartsWith("deepseek.") && !d.Key.StartsWith("probe.") && !d.Key.StartsWith("ping.") && !d.Key.StartsWith("network.public_")).ToList();
        var values=await hub.SnapshotAsync(settings.CustomHud,local.Select(d=>d.Key));
        Tests.Check(Convert.ToInt32(values["clipboard.text_length"])=="ECP macOS variable audit".Length && (string?)values["clipboard.preview"]=="ECP macOS variable audit","Clipboard values reflect actual macOS pasteboard text");
        var allProfile=new HudProfile { PrimaryTemplate=string.Join(" ",local.Select(d=>d.TemplateToken)) };
        var effective=HudProfileRenderer.BuildEffectiveVariables(allProfile,values);
        foreach(var d in local) Tests.Check(effective.TryGetValue(d.Key,out var value) && value is not null,"Advertised variable has a value: "+d.Key);
        File.WriteAllText("artifacts/audit/variables.json",JsonSerializer.Serialize(new { architecture=RuntimeInformation.ProcessArchitecture.ToString(), definitions=catalog, values=effective },new JsonSerializerOptions{WriteIndented=true}));
        foreach(var profile in settings.CustomHud.Profiles)
        {
            var vars=await hub.SnapshotAsync(settings.CustomHud,HudProfileRenderer.GetRequiredVariables(profile));
            var rendered=HudProfileRenderer.Render(profile,vars);
            Tests.Check(!new[]{rendered.PrimaryText,rendered.SecondaryText,rendered.RightText}.Any(s=>s.Contains("--")),"Built-in renders without placeholders: "+profile.BuiltInKey);
        }
        foreach(var language in new[]{AppLanguage.SimplifiedChinese,AppLanguage.English})
        {
            LocalizationManager.Initialize(LocalizationManager.PreferenceFor(language));
            settings=settings with { UiLanguage=LocalizationManager.PreferenceFor(language) };
            window.Close(); window=new SettingsWindow(settings,hud,runtime); desktop.MainWindow=window; window.Show();
            await Task.Delay(400);
            Tests.Check(LocalizationManager.Current==language,"Settings language: "+language);
            using var bitmap=new RenderTargetBitmap(new PixelSize((int)window.Width,(int)window.Height),new Vector(96,96));
            bitmap.Render(window); bitmap.Save($"artifacts/audit/settings-{language}.png");
            var tabs=window.GetLogicalDescendants().OfType<TabControl>().First();
            tabs.SelectedIndex=2;
            await Task.Delay(250);
            using var about=new RenderTargetBitmap(new PixelSize((int)window.Width,(int)window.Height),new Vector(96,96));
            about.Render(window); about.Save($"artifacts/audit/about-{language}.png");
            tabs.SelectedIndex=1;
            await Task.Delay(250);
            var library=window.FindControl<HudCustomizerView>("Customizer")!;
            library.GetLogicalDescendants().OfType<TabControl>().First().SelectedIndex=1;
            await Task.Delay(250);
            using var variables=new RenderTargetBitmap(new PixelSize((int)window.Width,(int)window.Height),new Vector(96,96));
            variables.Render(window); variables.Save($"artifacts/audit/library-{language}.png");
            var memory=settings.CustomHud.Profiles.First(p=>p.BuiltInKey=="system.memory");
            var measurement=await hub.SnapshotAsync(settings.CustomHud,HudProfileRenderer.GetRequiredVariables(memory));
            await hud.ShowPersistentAsync(HudProfileRenderer.Render(memory,measurement));
            using var hudImage=new RenderTargetBitmap(new PixelSize((int)hud.Width,(int)hud.Height),new Vector(96,96));
            hudImage.Render(hud); hudImage.Save($"artifacts/audit/hud-{language}.png");
            await hud.HidePersistentAsync();
        }
        hud.Show(); await Task.Delay(300);
        var handle=hud.TryGetPlatformHandle();
        Tests.Check(handle is not null,"HUD exposes a native handle");
        // Exercise the application's actual window configuration path, then only read native state.
        hud.ApplySettings(settings);
        Tests.Check(MacNative.ecp_hud_flags(handle!.Handle)==7,"Application applies click-through, all-Spaces and native transparency");
        Tests.Check(MacSystemProbe.TryGetCursorPosition(out _),"Global mouse position available without Accessibility permission");
        window.Close(); hud.Close();
    }
}

internal sealed class FixtureHttp : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        string content=request.RequestUri!.Host switch
        {
            "api.deepseek.com" => """{"is_available":true,"balance_infos":[{"currency":"CNY","total_balance":"12.5","granted_balance":"2.5","topped_up_balance":"10"}]}""",
            "api.ipify.org" => "203.0.113.1",
            "api6.ipify.org" => "2001:db8::1",
            "fixture.invalid" => """{"metrics":[{"value":42}]}""",
            _ => throw new InvalidOperationException("Unexpected test endpoint")
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(content)});
    }
}
