using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System.Text.RegularExpressions;

namespace EndfieldChargePlus.Settings;

internal static class SaveErrorDialog
{
    internal static string Details(Exception error) => Regex.Replace(Regex.Replace(error.ToString(), @"(?i)bearer\s+[^\s,""}]+", "Bearer [redacted]"),
        @"(?i)(bearer\s+|(?:api[_-]?key|authorization|access[_-]?token)\s*[:=]\s*)[^\s,""}]+", "$1[redacted]");

    internal static Task Show(Window owner,Exception error,bool startup=false)
    {
        if(!owner.IsVisible)return Task.CompletedTask;
        string T(string zh,string en)=>LocalizationManager.Text(zh,en);
        string details=Details(error);
        var reason=new TextBlock{Name="SaveErrorReason",Text=error.Message,TextWrapping=TextWrapping.Wrap,FontSize=14};
        var body=new TextBox{Name="SaveErrorDetails",Text=details,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=140,MaxHeight=260};
        var close=new Button{Name="CloseSaveError",Content=T("关闭窗口","Close window"),HorizontalContentAlignment=HorizontalAlignment.Center};
        var copy=new Button{Name="CopySaveError",Content=T("复制异常详情","Copy error details"),HorizontalContentAlignment=HorizontalAlignment.Center};
        var logs=new Button{Content=T("日志文件夹","Logs Folder"),HorizontalContentAlignment=HorizontalAlignment.Center};
        foreach(var button in new[]{close,copy,logs}){button.MinHeight=36;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Content=new TextBlock{Text=button.Content?.ToString(),TextWrapping=TextWrapping.Wrap,TextAlignment=TextAlignment.Center};}
        logs.Margin=new Thickness(8,0);
        var actions=new Grid{ColumnDefinitions=new("*,*,*")};actions.Children.Add(copy);Grid.SetColumn(logs,1);actions.Children.Add(logs);Grid.SetColumn(close,2);actions.Children.Add(close);
        var panel=new StackPanel{Spacing=14,Children={new TextBlock{Text=T("保存失败","Save failed"),FontSize=22,FontWeight=FontWeight.SemiBold,Foreground=Brush.Parse("#FFFA00")},reason,body}};
        if(startup && error.Message==T("请先把应用拖到 Applications 文件夹，再开启登录启动。","Move the app into Applications before enabling launch at login."))
        {
            var applications=new Button{Content="Applications ↗"};applications.Click+=(_,_)=>Open("file:///Applications");panel.Children.Add(applications);
        }
        panel.Children.Add(actions);
        var dialog=new Window{Name="SaveErrorDialog",Title=T("保存失败","Save failed"),Width=620,SizeToContent=SizeToContent.Height,MaxHeight=660,CanResize=false,ShowInTaskbar=false,SystemDecorations=SystemDecorations.None,Background=Brushes.Transparent,TransparencyLevelHint=new[]{WindowTransparencyLevel.Transparent},WindowStartupLocation=WindowStartupLocation.CenterOwner,
            Content=new Border{Padding=new(22),CornerRadius=new(8),BorderThickness=new(1),BorderBrush=Brush.Parse("#555555"),Background=Brush.Parse("#202020"),Child=panel}};
        close.Click+=(_,_)=>dialog.Close();dialog.KeyDown+=(_,e)=>{if(e.Key==Key.Escape){dialog.Close();e.Handled=true;}};
        copy.Click+=async(_,_)=>{try{if(dialog.Clipboard is {} clipboard)await clipboard.SetTextAsync(details);}catch(Exception ex){reason.Text=ex.Message;}};
        logs.Click+=(_,_)=>{try{System.IO.Directory.CreateDirectory(Diagnostics.AppLog.LogsDirectory);Open(Diagnostics.AppLog.LogsDirectory);}catch(Exception ex){reason.Text=ex.Message;}};
        return dialog.ShowDialog(owner);
    }
    private static void Open(string path)=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true});
}
