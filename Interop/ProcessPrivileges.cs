using System.Runtime.InteropServices;
using System.Security.Principal;
namespace EndfieldChargePlus.Interop;
public static class ProcessPrivileges
{
    public static bool IsElevated
    {
        get
        {
            try {
                if (OperatingSystem.IsWindows())
                    return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
                if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) return geteuid()==0;
            } catch (Exception) { }
            return false;
        }
    }
    public static string SettingsTitle(string version) =>
        LocalizationManager.Text($"Endfield Charge Plus v{version} 设置",$"Endfield Charge Plus v{version} Settings")
        + (IsElevated ? "（Endministrator Mode）" : "");
    [DllImport("libc")] private static extern uint geteuid();
}
