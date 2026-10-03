using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using EndfieldChargePlus.Interop;

namespace EndfieldChargePlus.Customization;

internal sealed class MacVariableProvider
{
    private uint[]? _ticks;
    private Dictionary<string, (ulong Rx, ulong Tx)>? _network;
    private long _networkAt;
    private static readonly object GpuGate = new();
    private static DateTime _gpuExpiry;
    private static List<MacGpu> _gpus = new();
    internal sealed record MacGpu(string Id, string Name, bool Unified, double Budget, bool LowPower, bool Removable);
    internal static IReadOnlyList<MacGpu> ReadGpus()
    {
        lock (GpuGate)
        {
            if (DateTime.UtcNow < _gpuExpiry) return _gpus;
            if (!OperatingSystem.IsMacOS()) return Array.Empty<MacGpu>();
            using var json = MacNative.Gpus();
            _gpus = json.RootElement.EnumerateArray().Select(d => new MacGpu(d.GetProperty("id").GetString()!,
                d.GetProperty("name").GetString()!, d.GetProperty("unified").GetBoolean(),
                d.GetProperty("budget").GetDouble(), d.GetProperty("lowPower").GetBoolean(), d.GetProperty("removable").GetBoolean())).ToList();
            _gpuExpiry = DateTime.UtcNow.AddSeconds(10);
            return _gpus;
        }
    }
    internal static IReadOnlyList<GpuAdapterInfo> GetGpuAdapters() => ReadGpus().Select((g,i) =>
        new GpuAdapterInfo(g.Id, g.Name, i, 0, 0, Array.Empty<string>(), Array.Empty<string>())).ToList();

    internal void Collect(IDictionary<string, object?> values, HashSet<string>? requested, string? gpuId)
    {
        if (!OperatingSystem.IsMacOS()) return;
        bool Need(string prefix) => requested is null || requested.Any(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        int mask = (Need("cpu.") || Need("system.") ? 1 : 0) | (Need("memory.") ? 2 : 0) |
            (Need("battery.") ? 4 : 0) | (Need("disk.") ? 8 : 0) | (Need("network.") ? 16 : 0);
        if (mask != 0)
        {
            using var json = MacNative.Snapshot(mask);
            Consume(json.RootElement, values, Stopwatch.GetTimestamp());
        }
        if (Need("gpu."))
        {
            var devices = ReadGpus();
            var gpu = devices.FirstOrDefault(g => g.Id == gpuId) ?? devices.FirstOrDefault();
            if (gpu is not null)
            {
                values["gpu.name"] = gpu.Name; values["gpu.count"] = devices.Count;
                values["gpu.unified_memory"] = gpu.Unified;
                values["gpu.recommended_working_set_bytes"] = gpu.Budget;
                values["gpu.low_power"] = gpu.LowPower; values["gpu.removable"] = gpu.Removable;
            }
        }
    }
    // Kept separate from the native calls so counter resets/wrap and first samples are testable.
    internal void Consume(JsonElement root, IDictionary<string, object?> values, long timestamp)
    {
        foreach (var p in root.EnumerateObject())
        {
            if (p.Name.StartsWith("__")) continue;
            values[p.Name] = p.Value.ValueKind switch
            {
                JsonValueKind.String => p.Value.GetString(), JsonValueKind.Number => p.Value.GetDouble(),
                JsonValueKind.True => true, JsonValueKind.False => false, _ => null
            };
        }
        if (root.TryGetProperty("__cpu_ticks", out var ticks))
        {
            uint[] next = ticks.EnumerateArray().Select(v => v.GetUInt32()).ToArray();
            if (_ticks is not null && next.Length == 4)
            {
                var delta = next.Select((v,i) => (double)unchecked(v-_ticks[i])).ToArray();
                double total = delta.Sum();
                if (total > 0)
                {
                    values["cpu.usage"] = (total-delta[2])/total*100;
                    values["cpu.user_usage"] = (delta[0]+delta[3])/total*100;
                    values["cpu.kernel_usage"] = delta[1]/total*100;
                    values["cpu.idle_percent"] = delta[2]/total*100;
                }
            }
            _ticks = next;
        }
        if (root.TryGetProperty("__network_samples", out var samples))
        {
            var next = samples.EnumerateObject().ToDictionary(p => p.Name, p => (Rx:p.Value[0].GetUInt64(), Tx:p.Value[1].GetUInt64()));
            double seconds = (timestamp-_networkAt)/(double)Stopwatch.Frequency;
            if (_network is not null && seconds > 0)
            {
                double rx=0,tx=0;
                foreach (var (name, sample) in next)
                    if (_network.TryGetValue(name, out var old) && sample.Rx >= old.Rx && sample.Tx >= old.Tx)
                    { rx += sample.Rx-old.Rx; tx += sample.Tx-old.Tx; }
                values["network.download_bps"] = rx/seconds; values["network.upload_bps"] = tx/seconds;
                values["network.total_bps"] = (rx+tx)/seconds;
                values["network.download_mbps"] = rx/seconds*8/1e6; values["network.upload_mbps"] = tx/seconds*8/1e6;
                values["network.total_mbps"] = (rx+tx)/seconds*8/1e6;
            }
            _network=next; _networkAt=timestamp;
        }
        if (values.TryGetValue("battery.percent", out _) && values.TryGetValue("battery.charging", out var charging))
            values["battery.status_text"] = charging is true ? LocalizationManager.Text("充电中", "Charging") :
                values.TryGetValue("battery.ac_online", out var ac) && ac is true ? LocalizationManager.Text("已接通电源", "AC power") : LocalizationManager.Text("使用电池", "On battery");
        if (values.TryGetValue("system.uptime_seconds", out var uptime))
        {
            var t=TimeSpan.FromSeconds(Convert.ToDouble(uptime, CultureInfo.InvariantCulture));
            values["system.uptime_text"]=$"{(int)t.TotalDays}d {t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}";
        }
    }
}
