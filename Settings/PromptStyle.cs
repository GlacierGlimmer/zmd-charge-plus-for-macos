using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Layout;

namespace EndfieldChargePlus.Settings;

internal static class PromptStyle
{
    internal static void Apply(Window window,Control body)
    {
        window.SystemDecorations=SystemDecorations.None;
        window.CanResize=false;window.ShowInTaskbar=false;window.Topmost=false;
        window.SizeToContent=SizeToContent.Height;window.MaxHeight=Math.Min(window.MaxHeight,720);
        window.Background=Brushes.Transparent;
        window.TransparencyLevelHint=new[]{WindowTransparencyLevel.Transparent};
        window.RequestedThemeVariant=ThemeVariant.Dark;
        var title=(body as StackPanel)?.Children.FirstOrDefault() as TextBlock;
        if(title is not null)((StackPanel)body).Children.Remove(title);
        var heading=title??Heading(window.Title??"");
        if(title is null)window.PropertyChanged+=(_,e)=>{if(e.Property==Window.TitleProperty)heading.Text=window.Title;};
        var drag=new Border{Name="PromptDragHandle",Background=Brushes.Transparent,Child=heading};
        var close=new Button{Name="PromptWindowClose",Content="×",Width=32,Height=30,Padding=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Center};
        Avalonia.Automation.AutomationProperties.SetName(close,LocalizationManager.Text("关闭窗口","Close window"));
        close.Click+=(_,_)=>window.Close();
        var header=new Grid{ColumnDefinitions=new("*,Auto"),Margin=new Thickness(0,0,0,14)};
        header.Children.Add(drag);Grid.SetColumn(close,1);header.Children.Add(close);
        PixelPoint? pressedAt=null;PixelPoint original=default;
        drag.PointerPressed+=(_,e)=>
        {
            if(!e.GetCurrentPoint(drag).Properties.IsLeftButtonPressed)return;
            pressedAt=window.PointToScreen(e.GetPosition(window));original=window.Position;
            e.Pointer.Capture(drag);e.Handled=true;
        };
        drag.PointerMoved+=(_,e)=>
        {
            if(pressedAt is not {} start || e.Pointer.Captured!=drag)return;
            var current=window.PointToScreen(e.GetPosition(window));
            window.Position=new PixelPoint(original.X+current.X-start.X,original.Y+current.Y-start.Y);e.Handled=true;
        };
        drag.PointerReleased+=(_,e)=>{pressedAt=null;e.Pointer.Capture(null);};
        drag.PointerCaptureLost+=(_,_)=>pressedAt=null;
        var panel=new Grid{RowDefinitions=new RowDefinitions("Auto,*")};
        panel.Children.Add(header);Grid.SetRow(body,1);panel.Children.Add(body);
        window.Content=new Border{Name="EcpPromptFrame",Padding=new Thickness(22),CornerRadius=new CornerRadius(8),
            BorderThickness=new Thickness(1),BorderBrush=Brush.Parse("#555555"),Background=Brush.Parse("#202020"),Child=panel};
        window.KeyDown+=(_,e)=>{if(e.Key==Key.Escape){window.Close();e.Handled=true;}};
        window.Opened+=(_,_)=>
        {
            var scale=GlobalAppearance.CurrentScales;
            GlobalAppearance.Apply(window,scale.Font,scale.Ui);
            if(window.Screens.ScreenFromWindow(window) is {} screen)
            {
                double factor=screen.Scaling;
                window.MaxHeight=Math.Min(window.MaxHeight,Math.Max(220,screen.WorkingArea.Height/factor-32));
                window.MaxWidth=Math.Min(window.MaxWidth,Math.Max(280,screen.WorkingArea.Width/factor-32));
            }
        };
    }
    internal static TextBlock Heading(string text)=>new(){Text=text,FontSize=22,FontWeight=FontWeight.SemiBold,Foreground=Brush.Parse("#FFFA00"),TextWrapping=TextWrapping.Wrap};
    internal static Window? Owner()=> (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.Windows
        .OfType<SettingsWindow>().LastOrDefault(w=>w.IsVisible);
}
