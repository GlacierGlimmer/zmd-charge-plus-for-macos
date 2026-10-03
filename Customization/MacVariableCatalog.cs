using System.Text.RegularExpressions;
namespace EndfieldChargePlus.Customization;

/// <summary>Shared by the library, built-ins and imported profiles.</summary>
internal static class MacVariableCatalog
{
    private static readonly object Gate = new();
    private static HashSet<string>? _detected;
    private static readonly HashSet<string> UiKeys = new((
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
            ("memory.wired_bytes", "联动内存", "内存", "Byte", "数值", "Mach wire_count × 系统页大小。"),
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
