using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;

namespace EndfieldChargePlus.Settings;

internal static class PromptStyle
{
    internal static void Apply(Window window,Control body)
    {
        window.SystemDecorations=SystemDecorations.None;
        window.CanResize=false;window.ShowInTaskbar=false;
        window.SizeToContent=SizeToContent.Height;window.MaxHeight=Math.Min(window.MaxHeight,720);
        window.Background=Brushes.Transparent;
        window.TransparencyLevelHint=new[]{WindowTransparencyLevel.Transparent};
        window.RequestedThemeVariant=ThemeVariant.Dark;
        window.Content=new Border{Name="EcpPromptFrame",Padding=new Thickness(22),CornerRadius=new CornerRadius(8),
            BorderThickness=new Thickness(1),BorderBrush=Brush.Parse("#555555"),Background=Brush.Parse("#202020"),Child=body};
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
