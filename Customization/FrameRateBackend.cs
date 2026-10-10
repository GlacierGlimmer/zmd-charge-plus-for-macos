using System.Runtime.InteropServices;
using System.Text.Json;

namespace EndfieldChargePlus.Customization;

/// <summary>Counts ScreenCaptureKit complete frames, excluding idle/blank/suspended frames.</summary>
public static class FrameRateBackend
{
    [DllImport("ecpmac")] private static extern IntPtr ecp_frame_targets(int windows);
    [DllImport("ecpmac")] private static extern IntPtr ecp_frame_read(int window, [MarshalAs(UnmanagedType.LPUTF8Str)]string id);
    [DllImport("ecpmac")] private static extern void ecp_frames_stop();
    [DllImport("ecpmac")] private static extern void ecp_frames_keepalive(int window,[MarshalAs(UnmanagedType.LPUTF8Str)] string id);
    [DllImport("ecpmac")] private static extern void ecp_free(IntPtr pointer);
    private static bool IsCaptureFailure(Exception ex) => ex is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or JsonException or InvalidOperationException;
    private static JsonDocument ReadJson(IntPtr pointer)
    {
        try { return JsonDocument.Parse(pointer==IntPtr.Zero ? "{}" : Marshal.PtrToStringUTF8(pointer) ?? "{}"); }
        finally { if(pointer!=IntPtr.Zero) ecp_free(pointer); }
    }
    public static IReadOnlyList<FrameTarget> Windows()=>Targets(true);
    public static IReadOnlyList<FrameTarget> Displays()=>Targets(false);
    private static IReadOnlyList<FrameTarget> Targets(bool windows)
    {
        if (!OperatingSystem.IsMacOS()) return Array.Empty<FrameTarget>();
        try {
            using var json=ReadJson(ecp_frame_targets(windows ? 1 : 0));
            return json.RootElement.ValueKind==JsonValueKind.Array ? json.RootElement.EnumerateArray().Select(v=>new FrameTarget(v.GetProperty("id").GetString()!,v.GetProperty("name").GetString()!)).ToArray() : Array.Empty<FrameTarget>();
        } catch(Exception ex) when(IsCaptureFailure(ex)) { return Array.Empty<FrameTarget>(); }
    }
    public static FrameReading Read(string kind,string target)
    {
        if(!OperatingSystem.IsMacOS()) return new(null,target,"Requires macOS");
        try {
        using var json=ReadJson(ecp_frame_read(kind=="window" ? 1 : 0,target));
        var value=json.RootElement;
        return new(value.TryGetProperty("fps",out var fps) && fps.TryGetDouble(out var number) ? number : null,
            value.TryGetProperty("name",out var name) ? name.GetString() ?? target : target,
            value.TryGetProperty("status",out var status) ? status.GetString() ?? "Unavailable" : "Unavailable");
        } catch(Exception ex) when(IsCaptureFailure(ex)) { return new(null,target,"帧率采集不可用，请检查应用组件与屏幕录制权限 / Frame capture unavailable; check app components and Screen Recording permission"); }
    }
    public static void KeepAlive(string kind,string target) { try { if(OperatingSystem.IsMacOS()) ecp_frames_keepalive(kind=="window" ? 1 : 0,target); } catch(Exception ex) when(IsCaptureFailure(ex)) { } }
    public static void Stop() { try { if(OperatingSystem.IsMacOS()) ecp_frames_stop(); } catch(Exception ex) when(IsCaptureFailure(ex)) { } }
}
