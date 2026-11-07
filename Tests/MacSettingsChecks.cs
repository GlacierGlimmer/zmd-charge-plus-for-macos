using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using EndfieldChargePlus.Settings;

internal static class MacSettingsChecks
{
    internal static void Storage(Action<bool,string> check)
    {
        string root=Path.Combine(Path.GetTempPath(),"ecp-startup-errors-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string executable=Path.Combine(root,"A & B.app","Contents","MacOS","EndfieldChargePlus");
            Directory.CreateDirectory(Path.GetDirectoryName(executable)!);File.WriteAllText(executable,"fixture");
            string plist=Path.Combine(root,"agents","ecp.plist");
            StartupManager.WriteRegistration(plist,executable);
            check(StartupManager.ReadRegistration(plist).IsEnabled,"LaunchAgent round trip checks XML and the actual executable");
            File.Delete(executable);
            check(!StartupManager.ReadRegistration(plist).IsEnabled,"A moved or missing application is not reported as enabled");
            File.WriteAllText(plist,"not XML");var broken=StartupManager.ReadRegistration(plist);
            check(!broken.Succeeded && broken.Error is not null,"Invalid LaunchAgent retains the exact read exception");
            foreach(var path in new[]{"/Volumes/ECP/A.app/Contents/MacOS/EndfieldChargePlus","/private/tmp/AppTranslocation/UUID/d/A.app/Contents/MacOS/EndfieldChargePlus","/tmp/EndfieldChargePlus"})
            {
                bool blocked=false;try{StartupManager.WriteRegistration(plist,path);}catch(InvalidOperationException ex){blocked=ex.Message.Contains("Applications");}
                check(blocked,"Uninstalled or translocated application receives an actionable reason");
            }
            File.WriteAllText(executable,"fixture");string blocker=Path.Combine(root,"blocker");File.WriteAllText(blocker,"preserve");
            Exception? failure=null;try{StartupManager.WriteRegistration(Path.Combine(blocker,"ecp.plist"),executable);}catch(Exception ex){failure=ex;}
            check(failure is not null && SaveErrorDialog.Details(failure).Contains("blocker"),"Registration write error retains its exact path and exception detail");
            check(File.ReadAllText(blocker)=="preserve","Failed startup write preserves unrelated files");
        }
        finally{Directory.Delete(root,true);}
    }

    [DllImport("/usr/lib/libobjc.A.dylib")]static extern IntPtr sel_registerName(string name);
    [DllImport("/usr/lib/libobjc.A.dylib",EntryPoint="objc_msgSend")]static extern IntPtr Send(IntPtr receiver,IntPtr selector);
    internal static async Task Resize(SettingsWindow window,Action<bool,string> check)
    {
        var native=window.TryGetPlatformHandle()!;
        var cocoa=native.HandleDescriptor=="NSView"?Send(native.Handle,sel_registerName("window")):native.Handle;
        check((Send(cocoa,sel_registerName("styleMask")).ToInt64()&8)!=0,"Cocoa window really has NSWindowStyleMaskResizable");
        check(window.ExtendClientAreaChromeHints==Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome && window.ExtendClientAreaTitleBarHeightHint==0,"Native resize keeps the custom title bar");
        var originalSize=window.ClientSize;var originalPosition=window.Position;
        foreach(var item in new[]{(new Point(3,originalSize.Height/2),new Vector(-24,0)),(new Point(originalSize.Width-3,originalSize.Height/2),new Vector(24,0)),
            (new Point(originalSize.Width/2,3),new Vector(0,-24)),(new Point(originalSize.Width/2,originalSize.Height-3),new Vector(0,24)),
            (new Point(3,3),new Vector(-24,-24)),(new Point(originalSize.Width-3,3),new Vector(24,-24)),
            (new Point(3,originalSize.Height-3),new Vector(-24,24)),(new Point(originalSize.Width-3,originalSize.Height-3),new Vector(24,24))})
        {
            var pointer=new Pointer(900,PointerType.Mouse,true);
            window.RaiseEvent(new PointerPressedEventArgs(window,pointer,window,item.Item1,0,new PointerPointProperties(RawInputModifiers.LeftMouseButton,PointerUpdateKind.LeftButtonPressed),KeyModifiers.None,1));
            window.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent,window,pointer,window,item.Item1+item.Item2,0,new PointerPointProperties(RawInputModifiers.LeftMouseButton,PointerUpdateKind.Other),KeyModifiers.None));
            pointer.Capture(null);await Task.Delay(80);
            check((item.Item2.X==0 || window.ClientSize.Width>originalSize.Width+15) && (item.Item2.Y==0 || window.ClientSize.Height>originalSize.Height+15),"Pointer drag actually resizes the native macOS window at "+item.Item1);
            window.Width=originalSize.Width;window.Height=originalSize.Height;window.Position=originalPosition;await Task.Delay(60);
        }
    }

    internal static async Task StartupSaveFailure(SettingsWindow window,Action<bool,string> check)
    {
        if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ECP_TEST_HOME")))return;
        byte[] before=File.ReadAllBytes(SettingsManager.SettingsPath);
        window.FindControl<ToggleSwitch>("StartupSwitch")!.IsChecked=true;
        var save=window.FindControl<Button>("SaveBtn")!;
        save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await Task.Delay(180);
        var dialog=window.OwnedWindows.Single(w=>w.Name=="SaveErrorDialog");
        var status=window.FindControl<TextBlock>("StartupStatusText")!;
        check(status.IsVisible && status.Text!.Contains("Applications"),"Failed startup save shows an inline reason on the current page");
        check(save.IsEnabled && File.ReadAllBytes(SettingsManager.SettingsPath).SequenceEqual(before),"Failed startup save can be retried and does not overwrite the saved configuration");
        dialog.Close();window.FindControl<ToggleSwitch>("StartupSwitch")!.IsChecked=false;
    }
}
