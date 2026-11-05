using EndfieldChargePlus.Interop;
namespace EndfieldChargePlus.Views;
public partial class HudWindow
{
    private bool _applicationExitPrepared;
    internal void PrepareForApplicationExit()
    {
        if(_applicationExitPrepared)return;
        _applicationExitPrepared=true;
        _progressTimer.Stop();
        DisposeRadialInput();CancelPresentation();
        if(_macInputOwner!=IntPtr.Zero)
        {
            MacNative.ecp_hud_input_close(_macInputOwner);_macInputOwner=IntPtr.Zero;
        }
    }
}
