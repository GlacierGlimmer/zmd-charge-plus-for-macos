using Avalonia.Controls;
namespace EndfieldChargePlus.Customization;
public partial class HudCustomizerView
{
    private string _frameDisplaySelected="", _frameWindowSelected="";
    private bool _frameNeedsDisplay, _frameNeedsWindow;
    private void LoadFrameOptions(HudProfile profile)
    {
        var keys=HudProfileRenderer.GetRequiredVariables(profile);
        _frameNeedsDisplay=keys.Any(k=>k.StartsWith("frame.display."));
        _frameNeedsWindow=keys.Any(k=>k.StartsWith("frame.window."));
        FrameOptionsPanel.IsVisible=_frameNeedsDisplay || _frameNeedsWindow;
        if(!FrameOptionsPanel.IsVisible) return;
        FrameDisplayOptions.IsVisible=_frameNeedsDisplay; FrameWindowOptions.IsVisible=_frameNeedsWindow;
        _frameDisplaySelected=profile.FrameDisplayId; _frameWindowSelected=profile.FrameWindowId;
        FrameFullScaleBox.Value=(decimal)profile.FrameFullScaleFps;
        FrameDisplayTargetCombo.SelectedItem=null; FrameWindowTargetCombo.SelectedItem=null;
        RefreshFrameTargets();
        FrameCaptureHint.Text=LocalizationManager.Text("统计选定画面的实际更新帧率，排除静止帧；需要屏幕录制权限。不会保存画面。这与应用内部渲染／提交帧率可能不同，静止画面可以是 0 FPS。","Counts actual content-update frames, excluding idle frames. Requires Screen Recording permission; no images are saved. This may differ from internal application rendering/presentation FPS; static content can be 0 FPS.");
    }
    private void RefreshFrameTargets()
    {
        if(_frameNeedsDisplay) _frameDisplaySelected=FillFrameTargets(FrameDisplayTargetCombo,"display",_frameDisplaySelected);
        if(_frameNeedsWindow) _frameWindowSelected=FillFrameTargets(FrameWindowTargetCombo,"window",_frameWindowSelected);
    }
    private static string FillFrameTargets(ComboBox combo,string kind,string previous)
    {
        string selected=(combo.SelectedItem as FrameTarget)?.Id ?? previous;
        var targets=kind=="window" ? FrameRateBackend.Windows() : FrameRateBackend.Displays();
        var items=new[] { new FrameTarget("",kind=="display" ? LocalizationManager.Text("主显示器（自动）","Primary display (auto)") : LocalizationManager.Text("请选择窗口","Select a window")) }
            .Concat(targets.GroupBy(t=>t.Id).Select(g=>g.First())).ToArray();
        if (!items.Any(t=>t.Id==selected)) selected="";
        combo.ItemsSource=items;
        combo.SelectedItem=items.First(t=>t.Id==selected);
        return selected;
    }
}
