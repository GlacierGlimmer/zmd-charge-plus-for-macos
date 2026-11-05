using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace EndfieldChargePlus.Settings;

public partial class SettingsWindow
{
    private static readonly IReadOnlyDictionary<WindowEdge, Cursor> ResizeCursors = new Dictionary<WindowEdge, Cursor>
    {
        [WindowEdge.North] = new(StandardCursorType.TopSide), [WindowEdge.South] = new(StandardCursorType.BottomSide),
        [WindowEdge.West] = new(StandardCursorType.LeftSide), [WindowEdge.East] = new(StandardCursorType.RightSide),
        [WindowEdge.NorthWest] = new(StandardCursorType.TopLeftCorner), [WindowEdge.NorthEast] = new(StandardCursorType.TopRightCorner),
        [WindowEdge.SouthWest] = new(StandardCursorType.BottomLeftCorner), [WindowEdge.SouthEast] = new(StandardCursorType.BottomRightCorner)
    };
    private void InitializeWindowChrome()
    {
        if (OperatingSystem.IsWindows())
        {
            // SystemDecorations.None removes WS_SYSMENU along with the native
            // caption. Keep the Shell's taskbar menu and standard window commands
            // without adding a visible title bar. Avalonia reapplies this callback
            // whenever it updates styles (including minimize/restore).
            Win32Properties.AddWndProcHookCallback(this,ResizeWindowHitTest);
            Closed+=(_,_)=>Win32Properties.RemoveWndProcHookCallback(this,ResizeWindowHitTest);
            Win32Properties.AddWindowStylesCallback(this, (style, extendedStyle) =>
                ((style & ~0x00050000u) | 0x000A0000u | (CanResize ? 0x00050000u : 0u), extendedStyle));
        }
        if (OperatingSystem.IsMacOS())
        {
            // Cocoa ignores BeginResizeDrag and removes NSWindowStyleMaskResizable
            // for SystemDecorations.None. Retain native resizing with hidden chrome.
            SystemDecorations = SystemDecorations.Full;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = 0;
            PointerCaptureLost += (_, _) => EndMacResize();
            Deactivated += (_, _) => EndMacResize();
            Closed += (_, _) => EndMacResize();
        }
        InitializeStatusBar();
        MinimizeWindowButton.Click += (_, _) => WindowState = WindowState.Minimized;
        MaximizeWindowButton.Click += (_, _) => ToggleWindowMaximize();
        CloseWindowButton.Click += (_, _) => Close();
        TitleBarDragArea.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            if (e.ClickCount == 2) ToggleWindowMaximize();
            else BeginMoveDrag(e);
            e.Handled = true;
        };
        AddHandler(PointerPressedEvent, OnWindowFramePressed, RoutingStrategies.Tunnel,handledEventsToo:true);
        LayoutUpdated += (_, _) =>
        {
            // Continue the two content surfaces through the caption, even after UI scaling.
            var edge = WorkspaceSidebar.TranslatePoint(new Point(WorkspaceSidebar.Bounds.Width, 0), SettingsTitleBar);
            if (edge is { } point)
            {
                double width = Math.Clamp(point.X, 0, SettingsTitleBar.Bounds.Width);
                if (Math.Abs(TitleBarSidebarBackground.Width - width) > .1) TitleBarSidebarBackground.Width = width;
            }
        };
        AddHandler(PointerMovedEvent,OnWindowFrameMoved,RoutingStrategies.Tunnel,handledEventsToo:true);
        AddHandler(PointerReleasedEvent,(_,e)=> { if(_macResize is not null) {EndMacResize();e.Handled=true;} },RoutingStrategies.Tunnel,handledEventsToo:true);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == WindowStateProperty) RefreshWindowChrome();
        };
        RefreshWindowChrome();
    }

    private void ToggleWindowMaximize()
    {
        if (CanResize) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private WindowEdge? ResizeEdgeAt(Point point)
    {
        if (!CanResize || WindowState != WindowState.Normal) return null;
        const double edge = 8;
        bool left = point.X <= edge, right = point.X >= Bounds.Width - edge;
        bool top = point.Y <= edge, bottom = point.Y >= Bounds.Height - edge;
        if (top) return left ? WindowEdge.NorthWest : right ? WindowEdge.NorthEast : WindowEdge.North;
        if (bottom) return left ? WindowEdge.SouthWest : right ? WindowEdge.SouthEast : WindowEdge.South;
        return left ? WindowEdge.West : right ? WindowEdge.East : null;
    }

    private IntPtr ResizeWindowHitTest(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam,ref bool handled)
    {
        if(message!=0x0084 || !IsVisible)return IntPtr.Zero; // WM_NCHITTEST
        long packed=lParam.ToInt64();
        var point=this.PointToClient(new PixelPoint(unchecked((short)(packed&0xffff)),unchecked((short)((packed>>16)&0xffff))));
        if(ResizeEdgeAt(point) is not {} edge)return IntPtr.Zero;
        handled=true;
        return (IntPtr)(edge switch {WindowEdge.West=>10,WindowEdge.East=>11,WindowEdge.North=>12,
            WindowEdge.NorthWest=>13,WindowEdge.NorthEast=>14,WindowEdge.South=>15,
            WindowEdge.SouthWest=>16,WindowEdge.SouthEast=>17,_=>1});
    }

    private void OnWindowFramePressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed && ResizeEdgeAt(e.GetPosition(this)) is { } edge)
        {
            if (OperatingSystem.IsMacOS())
            {
                _macResize=new MacResizeState(edge,this.PointToScreen(e.GetPosition(this)),Position,ClientSize,RenderScaling,e.Pointer);
                e.Pointer.Capture(this);
            }
            else BeginResizeDrag(edge, e);
            e.Handled = true;
        }
    }

    private sealed record MacResizeState(WindowEdge Edge,PixelPoint PointerStart,PixelPoint PositionStart,Size SizeStart,double Scale,IPointer Pointer);
    private MacResizeState? _macResize;

    private void EndMacResize()
    {
        var state=_macResize;_macResize=null;
        if(state?.Pointer.Captured==this)state.Pointer.Capture(null);
    }

    private void OnWindowFrameMoved(object? sender,PointerEventArgs e)
    {
        if(_macResize is {} state)
        {
            if(!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || WindowState!=WindowState.Normal) {EndMacResize();return;}
            var point=this.PointToScreen(e.GetPosition(this));
            double dx=(point.X-state.PointerStart.X)/state.Scale,dy=(point.Y-state.PointerStart.Y)/state.Scale;
            bool left=state.Edge is WindowEdge.West or WindowEdge.NorthWest or WindowEdge.SouthWest;
            bool right=state.Edge is WindowEdge.East or WindowEdge.NorthEast or WindowEdge.SouthEast;
            bool top=state.Edge is WindowEdge.North or WindowEdge.NorthWest or WindowEdge.NorthEast;
            bool bottom=state.Edge is WindowEdge.South or WindowEdge.SouthWest or WindowEdge.SouthEast;
            double width=Math.Clamp(state.SizeStart.Width+(left?-dx:right?dx:0),MinWidth,MaxWidth);
            double height=Math.Clamp(state.SizeStart.Height+(top?-dy:bottom?dy:0),MinHeight,MaxHeight);
            Width=width;Height=height;
            if(left||top)Position=new PixelPoint(state.PositionStart.X+(left?(int)Math.Round((state.SizeStart.Width-width)*state.Scale):0),
                state.PositionStart.Y+(top?(int)Math.Round((state.SizeStart.Height-height)*state.Scale):0));
            Cursor=ResizeCursors[state.Edge];e.Handled=true;
        }
        else Cursor=ResizeEdgeAt(e.GetPosition(this)) is {} edge ? ResizeCursors[edge] : null;
    }

    private void RefreshWindowChrome()
    {
        RefreshStatusLocalization();
        bool maximized = WindowState == WindowState.Maximized;
        SettingsWindowFrame.CornerRadius = new CornerRadius(maximized ? 0 : 8);
        SettingsWindowFrame.BorderThickness = new Thickness(maximized ? 0 : 1);
        MaximizeWindowGlyph.Data = Geometry.Parse(maximized ? "M1,3 H3 V1 M5,1 V3 H7 M7,5 H5 V7 M3,7 V5 H1" : "M1,3 V1 H3 M5,1 H7 V3 M7,5 V7 H5 M3,7 H1 V5");
        SetCaption(MinimizeWindowButton, LocalizationManager.Text("最小化窗口", "Minimize window"));
        SetCaption(MaximizeWindowButton, maximized ? LocalizationManager.Text("还原窗口", "Restore window") : LocalizationManager.Text("最大化窗口", "Maximize window"));
        SetCaption(CloseWindowButton, LocalizationManager.Text("关闭窗口", "Close window"));
    }

    private static void SetCaption(Button button, string text)
    {
        ToolTip.SetTip(button, text);
        AutomationProperties.SetName(button, text);
    }
}
