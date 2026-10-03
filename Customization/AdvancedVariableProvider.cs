using System.Globalization;
using System.Net;
using System.Net.Sockets;
namespace EndfieldChargePlus.Customization;
internal sealed class AdvancedVariableProvider
{
    private static readonly HttpClient FastHttp = new() { Timeout = TimeSpan.FromSeconds(3) };
    private readonly HttpClient _fastHttp;
    internal AdvancedVariableProvider(HttpClient? http = null) => _fastHttp = http ?? FastHttp;
    private DateTime _nextPublicIpv4Read, _nextPublicIpv6Read;
    private string _publicIpv4 = "", _publicIpv6 = "";
    public async Task EnrichAsync(IDictionary<string, object?> vars, CustomHudSettings settings,
        HashSet<string>? requested, string? gpuAdapterId, CancellationToken ct)
    {
        if (requested is null || requested.Any(k => k.StartsWith("time."))) AddWorldTime(vars);
        if (requested is null || requested.Contains("network.public_ipv4") || requested.Contains("network.public_ipv6"))
            await AddPublicIpAsync(vars, requested is null || requested.Contains("network.public_ipv4"),
                requested is null || requested.Contains("network.public_ipv6"), ct);
    }
    private static void AddWorldTime(IDictionary<string, object?> v)
    {
        PutWorld(v, "time.world.nyc", "America/New_York");
        PutWorld(v, "time.world.london", "Europe/London");
        PutWorld(v, "time.world.tokyo", "Asia/Tokyo");
        PutWorld(v, "time.world.beijing", "Asia/Shanghai");
    }

    private async Task AddPublicIpAsync(IDictionary<string, object?> v, bool ipv4, bool ipv6, CancellationToken ct)
    {
        if (ipv4 && DateTime.UtcNow >= _nextPublicIpv4Read)
        {
            _nextPublicIpv4Read = DateTime.UtcNow.AddMinutes(5);
            try { _publicIpv4 = (await _fastHttp.GetStringAsync("https://api.ipify.org", ct).ConfigureAwait(false)).Trim(); } catch { _publicIpv4 = ""; }
        }
        if (ipv6 && DateTime.UtcNow >= _nextPublicIpv6Read)
        {
            _nextPublicIpv6Read = DateTime.UtcNow.AddMinutes(5);
            try { _publicIpv6 = (await _fastHttp.GetStringAsync("https://api6.ipify.org", ct).ConfigureAwait(false)).Trim(); } catch { _publicIpv6 = ""; }
        }
        if (ipv4 && IPAddress.TryParse(_publicIpv4, out var ip4) && ip4.AddressFamily == AddressFamily.InterNetwork) v["network.public_ipv4"] = _publicIpv4;
        if (ipv6 && IPAddress.TryParse(_publicIpv6, out var ip6) && ip6.AddressFamily == AddressFamily.InterNetworkV6) v["network.public_ipv6"] = _publicIpv6;
        if (OperatingSystem.IsMacOS())
        {
            if (ipv4 && !v.ContainsKey("network.public_ipv4")) v["network.public_ipv4"] = LocalizationManager.Text("IPv4 查询失败", "IPv4 lookup failed");
            if (ipv6 && !v.ContainsKey("network.public_ipv6")) v["network.public_ipv6"] = LocalizationManager.Text("IPv6 查询失败", "IPv6 lookup failed");
        }
    }

    private static void PutWorld(IDictionary<string, object?> v, string key, string zone)
    {
        try { v[key] = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zone)).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); } catch { }
    }

}
