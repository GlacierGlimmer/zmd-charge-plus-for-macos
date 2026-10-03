using Avalonia;
namespace EndfieldChargePlus.Interop;
internal static class MacSystemProbe
{
    private static DateTime _nextPowerRead;
    private static bool? _ac;
    public static bool TryGetCursorPosition(out PixelPoint point)
    {
        point = default;
        if (!OperatingSystem.IsMacOS() || MacNative.ecp_pointer(out var x, out var y) == 0) return false;
        point = new PixelPoint((int)Math.Round(x), (int)Math.Round(y));
        return true;
    }
    public static bool TryGetAcOnline(out bool online)
    {
        if (DateTime.UtcNow >= _nextPowerRead)
        {
            _nextPowerRead = DateTime.UtcNow.AddSeconds(1);
            using var json = MacNative.Snapshot(4);
            _ac = json.RootElement.TryGetProperty("battery.ac_online", out var value) ? value.GetBoolean() : null;
        }
        online = _ac ?? false;
        return _ac.HasValue;
    }
}
