using System.Text.RegularExpressions;
namespace EndfieldChargePlus.Customization;

/// <summary>Shared by the library, built-ins and imported profiles.</summary>
internal static class MacVariableCatalog
{
    private static readonly object Gate = new();
    private static HashSet<string>? _detected;
    private static readonly HashSet<string> UiKeys = new((
        "frame.display.fps frame.window.fps frame.display.percent frame.window.percent frame.display.name frame.window.name frame.display.status frame.window.status " +
        "display.primary_width_px display.primary_height_px display.virtual_x_points display.virtual_y_points " +
        "display.virtual_width_points display.virtual_height_points display.monitor_count display.system_dpi display.scale_percent " +
        "clipboard.has_text clipboard.text_length clipboard.preview clipboard.has_image clipboard.last_updated " +
        "app.theme app.preset_name app.active_profile " +
        "time.world.nyc time.world.london time.world.tokyo time.world.beijing " +
        "time.target.value time.target.remaining_seconds time.target.remaining_percent " +
        "time.target.progress time.target.remaining_text time.display.progress time.display.status_text " +
        "network.public_ipv4 network.public_ipv6 network.display_download network.display_upload " +
        "network.profile_percent network.profile_percent_text network.profile_percent_bps network.profile_percent_mode")
        .Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
    internal static IReadOnlySet<string> Detected
    {
        get { lock (Gate) { if (_detected is null) Refresh(); return _detected!; } }
    }
    internal static void Refresh()
    {
        var values = new Dictionary<string, object?>();
        if (OperatingSystem.IsMacOS())
        {
            var provider = new MacVariableProvider();
            provider.Collect(values, null, null);
            for (int attempt=0; attempt<6 && !values.ContainsKey("cpu.usage"); attempt++)
            {
                Thread.Sleep(200);
                provider.Collect(values, null, null);
            }
        }
        lock (Gate) _detected = values.Where(p => p.Value is not null).Select(p => p.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
    internal static bool Supports(string key)
    {
        if(key.StartsWith("sensor.",StringComparison.OrdinalIgnoreCase) || key.StartsWith("hardware.",StringComparison.OrdinalIgnoreCase))return true;
        if (key.StartsWith("custom.", StringComparison.OrdinalIgnoreCase)) return true;
        if (MacSharedKeys.Keys.Contains(key) || UiKeys.Contains(key) || Detected.Contains(key)) return true;
        if (!VariableCatalog.PlatformIndependentDefinitions.Any(d => d.Key.Equals(key, StringComparison.OrdinalIgnoreCase))) return false;
        if (key.StartsWith("deepseek.", StringComparison.OrdinalIgnoreCase)) return true;
        return (key.StartsWith("probe.", StringComparison.OrdinalIgnoreCase) || key.StartsWith("ping.", StringComparison.OrdinalIgnoreCase))
            && key is not ("probe.ttl" or "ping.ttl");
    }
    internal static IReadOnlyList<VariableDefinition> Build(IEnumerable<VariableDefinition> original, string? gpuId = null)
    {
        var result = original.Where(d => Supports(d.Key)).Select(d => d with { Description = Describe(d) }).ToList();
        foreach (var (key,name,category,unit,type,description) in new[]
        {
            ("memory.free_bytes", "空闲物理内存", "内存", "Byte", "数值", "Mach free_count × 系统页大小。"),
            ("memory.active_bytes", "活跃内存", "内存", "Byte", "数值", "Mach active_count × 系统页大小。"),
            ("memory.inactive_bytes", "非活跃内存", "内存", "Byte", "数值", "Mach inactive_count × 系统页大小。"),
            ("memory.wired_bytes", "锁定内存", "内存", "Byte", "数值", "Mach wire_count × 系统页大小。"),
            ("memory.swap_total_bytes", "交换空间总量", "内存", "Byte", "数值", "sysctl vm.swapusage，随系统动态分配。"),
            ("memory.swap_used_bytes", "已用交换空间", "内存", "Byte", "数值", "sysctl vm.swapusage 实际已用值。"),
            ("memory.swap_available_bytes", "可用交换空间", "内存", "Byte", "数值", "sysctl vm.swapusage 实际可用值。"),
            ("disk.system.available_bytes", "用户可用磁盘空间", "磁盘", "Byte", "数值", "APFS Data 卷 statfs f_bavail，不含系统保留空间。"),
            ("battery.time_to_full_seconds", "充满剩余时间", "电池", "秒", "数值", "IOPowerSources 充电时间估计，仅系统提供估计时显示。"),
            ("gpu.unified_memory", "统一内存架构", "GPU", "", "布尔", "Metal hasUnifiedMemory，不将统一内存当作独立显存。"),
            ("gpu.recommended_working_set_bytes", "Metal 建议工作集上限", "GPU", "Byte", "数值", "Metal recommendedMaxWorkingSetSize；是预算，不是显存容量或 GPU 已用内存。"),
            ("gpu.low_power", "低功耗 GPU", "GPU", "", "布尔", "Metal isLowPower。"),
            ("gpu.removable", "可移除 GPU", "GPU", "", "布尔", "Metal isRemovable。"),
            ("display.virtual_x_points", "桌面左边界", "显示器", "pt", "数值", "CoreGraphics 全局桌面坐标，单位为逻辑点。"),
            ("display.virtual_y_points", "桌面上边界", "显示器", "pt", "数值", "CoreGraphics 全局桌面坐标，单位为逻辑点。"),
            ("display.virtual_width_points", "桌面总宽度", "显示器", "pt", "数值", "CoreGraphics 所有活动屏幕的边界合并，单位为逻辑点。"),
            ("display.virtual_height_points", "桌面总高度", "显示器", "pt", "数值", "CoreGraphics 所有活动屏幕的边界合并，单位为逻辑点。"),
        })
            if ((Detected.Contains(key) || UiKeys.Contains(key)) && result.All(d => d.Key != key))
                result.Add(new(key,name,category,description,type,unit,"左侧信息",unit=="Byte" ? "gb:1 / auto:1" : "0"));
        return result.GroupBy(d => d.Key, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
    }
    private static string Describe(VariableDefinition d)
    {
        string source = d.Key.Split('.')[0] switch
        {
            "cpu" => "Mach CPU tick 差分或 sysctl",
            "memory" => "Mach VM / sysctl；已用 = active + wired + compressed，可用 = 总量减已用，并非内存压力",
            "disk" => "APFS Data 卷 statfs；容量包含同一 APFS 容器共享空间",
            "battery" => "IOPowerSources；无内置电池时移除，仅在系统提供数据时列出",
            "gpu" => "Metal 设备信息；不提供系统未公开的全局 GPU 使用率、温度和显存占用",
            "network" => "macOS en* 物理接口 64 位计数器差分（排除 VPN/loopback 重复流量），或已实现的公网查询/方案计算",
            "display" => "CoreGraphics 原生像素分辨率 / AppKit Retina backingScaleFactor；桌面坐标使用逻辑点，DPI 为 96 × 缩放比例",
            "clipboard" => "macOS 剪贴板；更新时间为 ECP 最近观察到内容变化的时刻",
            _ => "macOS / .NET 或本方案实际配置与查询结果",
        };
        return d.Key.StartsWith("time.") || d.Key.StartsWith("deepseek.")
            ? d.Description.Replace("Windows", "macOS").Replace("Eastern Standard Time", "America/New_York").Replace("China Standard Time", "Asia/Shanghai")
            : $"{d.Name}。数据来源：{source}。";
    }
    internal static string LocalizedName(VariableDefinition item,string fallback)=>item.Key switch {
        "memory.free_bytes"=>LocalizationManager.Text("空闲物理内存","Free Physical Memory"),
        "memory.active_bytes"=>LocalizationManager.Text("活跃内存","Active Memory"),
        "memory.inactive_bytes"=>LocalizationManager.Text("非活跃内存","Inactive Memory"),
        "memory.wired_bytes"=>LocalizationManager.Text("锁定内存","Wired Memory"),
        "memory.swap_total_bytes"=>LocalizationManager.Text("交换空间总量","Total Swap Space"),
        "memory.swap_used_bytes"=>LocalizationManager.Text("已用交换空间","Used Swap Space"),
        "memory.swap_available_bytes"=>LocalizationManager.Text("可用交换空间","Available Swap Space"),
        "disk.system.available_bytes"=>LocalizationManager.Text("用户可用磁盘空间","User-Available Disk Space"),
        "battery.time_to_full_seconds"=>LocalizationManager.Text("充满剩余时间","Time Until Full Charge"),
        "gpu.unified_memory"=>LocalizationManager.Text("统一内存架构","Unified Memory Architecture"),
        "gpu.recommended_working_set_bytes"=>LocalizationManager.Text("Metal 建议工作集上限","Metal Recommended Working Set Limit"),
        "gpu.low_power"=>LocalizationManager.Text("低功耗 GPU","Low-Power GPU"),
        "gpu.removable"=>LocalizationManager.Text("可移除 GPU","Removable GPU"),
        _=>fallback};

    // Keep platform collection details independent of the generic Windows
    // description keys; those keys intentionally share short metric names.
    internal static string LocalizedDescription(VariableDefinition item,string name,string fallback)
    {
        if(item.Key.StartsWith("sensor.") || item.Key.StartsWith("hardware.") || item.Key.StartsWith("custom."))return fallback;
        if(item.Key.StartsWith("time.") || item.Key.StartsWith("deepseek."))return fallback.Replace("Windows","macOS").Replace("Eastern Standard Time","America/New_York").Replace("China Standard Time","Asia/Shanghai");
        string? exact=item.Key switch {
            "memory.free_bytes"=>LocalizationManager.Text("Mach free_count × 系统页大小。","Mach free_count multiplied by the system page size."),
            "memory.active_bytes"=>LocalizationManager.Text("Mach active_count × 系统页大小。","Mach active_count multiplied by the system page size."),
            "memory.inactive_bytes"=>LocalizationManager.Text("Mach inactive_count × 系统页大小。","Mach inactive_count multiplied by the system page size."),
            "memory.wired_bytes"=>LocalizationManager.Text("Mach wire_count × 系统页大小。","Mach wire_count multiplied by the system page size."),
            "memory.swap_total_bytes"=>LocalizationManager.Text("sysctl vm.swapusage，随系统动态分配。","Total swap from sysctl vm.swapusage, dynamically allocated by the system."),
            "memory.swap_used_bytes"=>LocalizationManager.Text("sysctl vm.swapusage 实际已用值。","Actual used swap from sysctl vm.swapusage."),
            "memory.swap_available_bytes"=>LocalizationManager.Text("sysctl vm.swapusage 实际可用值。","Actual available swap from sysctl vm.swapusage."),
            "disk.system.available_bytes"=>LocalizationManager.Text("APFS Data 卷 statfs f_bavail，不含系统保留空间。","statfs f_bavail for the APFS Data volume, excluding system-reserved space."),
            "battery.time_to_full_seconds"=>LocalizationManager.Text("IOPowerSources 充电时间估计，仅系统提供估计时显示。","IOPowerSources charging-time estimate, shown only when the system supplies an estimate."),
            "gpu.unified_memory"=>LocalizationManager.Text("Metal hasUnifiedMemory，不将统一内存当作独立显存。","Metal hasUnifiedMemory. Unified memory is not counted as dedicated video memory."),
            "gpu.recommended_working_set_bytes"=>LocalizationManager.Text("Metal recommendedMaxWorkingSetSize；是预算，不是显存容量或 GPU 已用内存。","Metal recommendedMaxWorkingSetSize: a budget, not video memory capacity or GPU memory usage."),
            "gpu.low_power"=>LocalizationManager.Text("Metal isLowPower。","Low-power device state from Metal isLowPower."),
            "gpu.removable"=>LocalizationManager.Text("Metal isRemovable。","Removable device state from Metal isRemovable."),
            _=>null};
        if(exact is not null)return exact;
        string source=item.Key.Split('.')[0] switch {
            "cpu"=>LocalizationManager.Text("Mach CPU tick 差分或 sysctl","Differences in Mach CPU ticks or sysctl"),
            "memory"=>LocalizationManager.Text("Mach VM / sysctl；已用 = active + wired + compressed，可用 = 总量减已用，并非内存压力","Mach VM / sysctl; used = active + wired + compressed, available = total minus used; this is not memory pressure"),
            "disk"=>LocalizationManager.Text("APFS Data 卷 statfs；容量包含同一 APFS 容器共享空间","statfs for the APFS Data volume; capacity includes space shared within the same APFS container"),
            "battery"=>LocalizationManager.Text("IOPowerSources；无内置电池时移除，仅在系统提供数据时列出","IOPowerSources; omitted without a built-in battery and listed only when the system provides data"),
            "gpu"=>LocalizationManager.Text("Metal 设备信息；不提供系统未公开的全局 GPU 使用率、温度和显存占用","Metal device information; global GPU usage, temperature and video memory usage are unavailable when not exposed by the system"),
            "network"=>LocalizationManager.Text("macOS en* 物理接口 64 位计数器差分，排除 VPN/loopback 重复流量；或已实现的公网查询及方案计算","Differences in 64-bit counters for macOS en* physical interfaces, excluding duplicate VPN/loopback traffic; or implemented public-IP queries and profile calculations"),
            "display"=>LocalizationManager.Text("CoreGraphics 原生像素分辨率 / AppKit Retina backingScaleFactor；桌面坐标使用逻辑点，DPI 为 96 × 缩放比例","CoreGraphics native pixel resolution / AppKit Retina backingScaleFactor; desktop coordinates use logical points and DPI is 96 multiplied by the scale factor"),
            "clipboard"=>LocalizationManager.Text("macOS 剪贴板；更新时间为 ECP 最近观察到内容变化的时刻","macOS clipboard; update time is when ECP last observed a content change"),
            _=>LocalizationManager.Text("macOS / .NET 或本方案实际配置与查询结果","macOS / .NET or the actual configuration and query results for this profile")};
        return LocalizationManager.Formatted($"{name}。数据来源：{source}。",$"{name}. Data source: {source}.");
    }

    internal static List<HudProfile> AdaptProfiles(IEnumerable<HudProfile> source)
    {
        var result = new List<HudProfile>();
        foreach (var item in source)
        {
            var p = item;
            if (p.BuiltInKey == "system.battery")
            {
                if (!Detected.Contains("battery.percent")) continue;
                p = p with { PrimaryTemplate="{battery.status_text}", SecondaryTemplate="" };
            }
            if (p.BuiltInKey == "system.cpu") p = p with { PrimaryTemplate="{cpu.logical_processors}", SecondaryTemplate=" CPU" };
            if (p.BuiltInKey == "system.gpu")
            {
                if (!Detected.Contains("gpu.name")) continue;
                p = p with { PrimaryTemplate="{gpu.name}", SecondaryTemplate="", RightTemplate="{gpu.count}", RightSuffix=" GPU", ProgressVariable="", ProgressMax=1 };
            }
            if (p.BuiltInKey == "deepseek.balance-period") p = p with { PrimaryTemplate="{deepseek.balance_text}" };
            if (p.BuiltInKey == "network.ping") p = p with { PrimaryTemplate="{probe.latency_text}" };
            result.Add(RemoveUnsupportedReferences(p));
        }
        return result;
    }
    internal static HudProfile RemoveUnsupportedReferences(HudProfile p)
    {
        string Clean(string template) => Regex.Replace(template, @"\{[^{}]+\}", match =>
            TemplateEngine.ExtractKeys(match.Value).Any(k => !Supports(k)) ? "" : match.Value);
        return p with
        {
            TaglineTemplate=Clean(p.TaglineTemplate), TitleTemplate=Clean(p.TitleTemplate),
            PrimaryTemplate=Clean(p.PrimaryTemplate), SecondaryTemplate=Clean(p.SecondaryTemplate),
            RightTemplate=Clean(p.RightTemplate), RightSuffix=Clean(p.RightSuffix),
            ProgressVariable=TemplateEngine.ExtractExpressionKeys(p.ProgressVariable).Any(k => !Supports(k)) ? "" : p.ProgressVariable,
            ColorRules=p.ColorRules.Where(r => Supports(r.Variable)).ToList()
        };
    }
}
