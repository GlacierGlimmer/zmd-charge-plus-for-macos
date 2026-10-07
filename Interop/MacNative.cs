using System.Runtime.InteropServices;
using System.Text.Json;

namespace EndfieldChargePlus.Interop;

internal static class MacNative
{
    private const string Library = "ecpmac";
    [DllImport(Library)] private static extern IntPtr ecp_snapshot(int mask);
    [DllImport(Library)] private static extern IntPtr ecp_gpus();
    [DllImport(Library)] private static extern IntPtr ecp_display();
    [DllImport(Library)] private static extern IntPtr ecp_clipboard();
    [DllImport(Library)] private static extern void ecp_free(IntPtr pointer);
    [DllImport(Library)] private static extern int ecp_hud_window(IntPtr handle, int topmost);
    [DllImport(Library)] private static extern int ecp_hud_vertical_offset(IntPtr handle, double offset);
    [DllImport(Library)] internal static extern int ecp_hud_flags(IntPtr handle);
    [DllImport(Library)] internal static extern int ecp_pointer(out double x, out double y);
    [DllImport(Library)] internal static extern int ecp_keychain_set([MarshalAs(UnmanagedType.LPUTF8Str)] string account, [MarshalAs(UnmanagedType.LPUTF8Str)] string secret);
    [DllImport(Library)] private static extern IntPtr ecp_keychain_get([MarshalAs(UnmanagedType.LPUTF8Str)] string account);
    private static string? Consume(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero) return null;
        try { return Marshal.PtrToStringUTF8(pointer); }
        finally { ecp_free(pointer); }
    }
    internal static JsonDocument Snapshot(int mask) => JsonDocument.Parse(Consume(ecp_snapshot(mask)) ?? "{}");
    internal static JsonDocument Gpus() => JsonDocument.Parse(Consume(ecp_gpus()) ?? "[]");
    internal static JsonDocument Display() => JsonDocument.Parse(Consume(ecp_display()) ?? "{}");
    internal static JsonDocument Clipboard() => JsonDocument.Parse(Consume(ecp_clipboard()) ?? "{}");
    internal static string? ReadKeychain(string account) => Consume(ecp_keychain_get(account));
    internal static bool SetHudWindow(IntPtr handle, bool topmost) => ecp_hud_window(handle, topmost ? 1 : 0) == 1;
    internal static void SetHudVerticalOffset(IntPtr handle, double offset) => ecp_hud_vertical_offset(handle, offset);
}
