using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
namespace EndfieldChargePlus.Customization;

public static class ProfileDataWarnings
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, (long Since, string? Reported)> Incidents = new();
    private static readonly Queue<(string Name, string[] Keys)> Pending = new();
    private static Window? _dialog;
    public static void Check(HudProfile profile, IReadOnlyDictionary<string,object?> values,string scope="")
    {
        string key=scope+":"+profile.Id;
        var missing=HudProfileRenderer.GetUnavailableVariables(profile,values).OrderBy(k => k).ToArray();
        lock (Gate)
        {
            if (missing.Length==0) { Incidents.Remove(key); return; }
            if (!Incidents.TryGetValue(key,out var incident))
                incident=(Environment.TickCount64,null);
            var signature=string.Join("|",missing);
            if (Environment.TickCount64-incident.Since<5000 || incident.Reported==signature)
            { Incidents[key]=incident; return; }
            Incidents[key]=(incident.Since,signature);
            Pending.Enqueue((BuiltInProfileLocalization.DisplayName(profile),missing));
        }
        Dispatcher.UIThread.Post(ShowNext);
    }
    private static void ShowNext()
    {
        if (_dialog is not null) return;
        (string Name,string[] Keys) item;
        lock (Gate) { if (Pending.Count==0) return; item=Pending.Dequeue(); }
        var close=new Button { Content=LocalizationManager.Text("知道了", "OK"), HorizontalAlignment=HorizontalAlignment.Right };
        var panel=new StackPanel { Margin=new Avalonia.Thickness(22), Spacing=16 };
        panel.Children.Add(new TextBlock { Text=LocalizationManager.Text($"方案“{item.Name}”的数据不可用或无效。", $"Data for '{item.Name}' is unavailable or invalid."), TextWrapping=Avalonia.Media.TextWrapping.Wrap, FontSize=16 });
        panel.Children.Add(new TextBlock { Text=string.Join("\n",item.Keys), TextWrapping=Avalonia.Media.TextWrapping.Wrap });
        panel.Children.Add(new TextBlock { Text=LocalizationManager.Text("请检查设备支持、权限、所选窗口或数据源设置。同一故障不会重复提醒；数据恢复后可再次提醒。", "Check device support, permissions, the selected window and data-source settings. The same failure is reported once and rearmed after recovery."), TextWrapping=Avalonia.Media.TextWrapping.Wrap });
        panel.Children.Add(close);
        var dialog=new Window { Title=LocalizationManager.Text("HUD 数据提示", "HUD data warning"), Width=520, SizeToContent=SizeToContent.Height, Content=panel, CanResize=false, Topmost=true, WindowStartupLocation=WindowStartupLocation.CenterScreen };
        _dialog=dialog; close.Click+=(_,_) => dialog.Close();
        dialog.Closed+=(_,_) => { _dialog=null; ShowNext(); }; dialog.Show();
    }
}
