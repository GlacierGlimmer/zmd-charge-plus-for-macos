using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EndfieldChargePlus.Customization;

public sealed record VariableDefinition(
    string Key,
    string Name,
    string Category,
    string Description,
    string ValueType,
    string Unit,
    string RecommendedUse,
    string RecommendedFormats)
{
    public string TemplateToken => "{" + Key + "}";
    public override string ToString() => $"{Name}    {Key}";
}

public static class VariableCatalog
{
    private static VariableDefinition V(
        string key, string name, string category, string description,
        string valueType = "数值", string unit = "", string use = "按数据含义决定", string formats = "0 或 0.0") =>
        new(key, name, category, description, valueType, unit, use, formats);

    private static readonly IReadOnlyList<VariableDefinition> StaticBuiltIns = new List<VariableDefinition>
    {
        // ===== 电池 =====
        V("battery.percent", "电池电量", "电池", "当前电池剩余电量百分比。", unit: "%", use: "右侧状态 / 圆环"),
        V("battery.ac_online", "外接电源状态", "电池", "当前是否接入外部电源。", "布尔", use: "标题 / 条件", formats: "无需格式化"),
        V("battery.charging", "正在充电", "电池", "当前是否正在充电。", "布尔", use: "标题 / 条件", formats: "无需格式化"),
        V("battery.discharging", "正在放电", "电池", "当前是否正在使用电池放电。", "布尔", use: "标题 / 条件", formats: "无需格式化"),
        V("battery.status_text", "电池状态文字", "电池", "根据电源状态生成“充电中 / 使用电池 / 已接通电源 / 未知”等文字。", "文本", use: "标题 / 右侧状态", formats: "无需格式化"),

        // ===== CPU =====
        V("cpu.usage", "CPU 使用率", "CPU", "当前整机 CPU 总使用率。", unit: "%", use: "右侧状态 / 圆环"),
        V("cpu.user_usage", "CPU 用户态使用率", "CPU", "CPU 时间中用于用户态代码的比例。", unit: "%", use: "右侧状态 / 圆环"),
        V("cpu.kernel_usage", "CPU 内核态使用率", "CPU", "CPU 时间中用于内核态且不含空闲时间的比例。", unit: "%", use: "右侧状态 / 圆环"),
        V("cpu.idle_percent", "CPU 空闲率", "CPU", "CPU 当前空闲时间比例。", unit: "%", use: "右侧状态 / 圆环"),
        V("cpu.name", "CPU 名称", "CPU", "处理器完整型号名称。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("cpu.physical_cores", "CPU 物理核心数", "CPU", "所有处理器插槽的物理核心总数。", "整数", "核", "左侧信息", "0"),
        V("cpu.logical_processors", "CPU 逻辑处理器数", "CPU", "系统可见的逻辑处理器总数。", "整数", "线程", "左侧信息", "0"),

        // ===== 内存 =====
        V("memory.used_bytes", "已用物理内存", "内存", "当前已使用的物理内存。", unit: "Byte", use: "左侧主值", formats: "gb:1 / gb:2 / bytes"),
        V("memory.available_bytes", "可用物理内存", "内存", "当前可用物理内存。", unit: "Byte", use: "左侧信息", formats: "gb:1 / bytes"),
        V("memory.total_bytes", "物理内存总量", "内存", "物理内存总容量。", unit: "Byte", use: "左侧次值", formats: "gb:1 / bytes"),
        V("memory.usage", "内存使用率", "内存", "已用物理内存占总物理内存的比例。", unit: "%", use: "右侧状态 / 圆环"),

        // ===== GPU =====
        V("gpu.name", "GPU 名称", "GPU", "当前方案选择的显示适配器名称。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("gpu.count", "GPU 数量", "GPU", "当前系统检测到的物理 GPU 数量。", "整数", "个", "左侧信息", "0"),

        // ===== 网络 =====
        V("network.download_bps", "系统总下载速度", "网络", "所有已连接、非回环网络接口合计的实时下载速度。", unit: "Byte/s", use: "左侧主值", formats: "speed"),
        V("network.upload_bps", "系统总上传速度", "网络", "所有已连接、非回环网络接口合计的实时上传速度。", unit: "Byte/s", use: "左侧次值", formats: "speed"),
        V("network.total_bps", "系统总吞吐量", "网络", "系统总下载速度与总上传速度之和。", unit: "Byte/s", use: "状态计算", formats: "speed"),
        V("network.download_mbps", "系统总下载速度（Mbps）", "网络", "系统总下载速度，换算为 Mbps。", unit: "Mbps", use: "左侧主值", formats: "0.0 / 0.00"),
        V("network.upload_mbps", "系统总上传速度（Mbps）", "网络", "系统总上传速度，换算为 Mbps。", unit: "Mbps", use: "左侧次值", formats: "0.0 / 0.00"),
        V("network.total_mbps", "系统总吞吐量（Mbps）", "网络", "下载与上传合计，换算为 Mbps。", unit: "Mbps", use: "左侧信息", formats: "0.0 / 0.00"),
        V("network.display_download", "方案下载速度文字", "网络", "按当前网络方案选择的单位生成的下载速度文字。", "文本", use: "左侧主值", formats: "无需格式化"),
        V("network.display_upload", "方案上传速度文字", "网络", "按当前网络方案选择的单位生成的上传速度文字。", "文本", use: "左侧次值", formats: "无需格式化"),
        V("network.profile_percent", "方案网络百分比", "网络", "按方案选择的百分比模式与“100% 对应速度”计算。", unit: "%", use: "右侧状态 / 圆环"),
        V("network.profile_percent_text", "方案网络百分比文字", "网络", "根据模式生成“50% / ↓ 50% / ↑ 50%”等文字；较大值模式不显示箭头。", "文本", use: "右侧状态", formats: "无需格式化"),
        V("network.profile_percent_bps", "百分比计算速度", "网络", "当前网络方案实际用于计算百分比的速度。", unit: "Byte/s", use: "状态计算", formats: "speed"),
        V("network.profile_percent_mode", "网络百分比模式", "网络", "当前方案百分比模式：Total / Download / Upload / Max。", "文本", use: "标题 / 条件", formats: "无需格式化"),
        V("network.total_received_bytes", "累计接收流量", "网络", "当前开机期间所有活动网络接口累计接收字节数之和。", unit: "Byte", use: "左侧信息", formats: "bytes / gb:1"),
        V("network.total_sent_bytes", "累计发送流量", "网络", "当前开机期间所有活动网络接口累计发送字节数之和。", unit: "Byte", use: "左侧信息", formats: "bytes / gb:1"),
        V("network.total_transferred_bytes", "累计总流量", "网络", "累计接收与发送流量之和。", unit: "Byte", use: "左侧信息", formats: "bytes / gb:1"),
        V("network.active_interface_count", "活动网络接口数量", "网络", "当前处于 Up 状态且非回环的网络接口数量。", "整数", "个", "左侧信息", "0"),
        V("network.interface_names", "活动网络接口名称", "网络", "所有活动网络接口名称，以逗号分隔。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("network.ipv4_addresses", "本机 IPv4 地址", "网络", "活动网络接口上的 IPv4 单播地址列表。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("network.ipv6_addresses", "本机 IPv6 地址", "网络", "活动网络接口上的 IPv6 单播地址列表。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("network.available", "网络可用状态", "网络", "macOS 是否检测到至少一个可用网络连接；不代表一定可访问互联网。", "布尔", use: "标题 / 条件", formats: "无需格式化"),
        V("network.packets_received", "累计接收数据包", "网络", "活动网络接口累计接收单播数据包数量。", "数值", "包", "左侧信息", "0"),
        V("network.packets_sent", "累计发送数据包", "网络", "活动网络接口累计发送单播数据包数量。", "数值", "包", "左侧信息", "0"),
        V("network.receive_errors", "接收错误", "网络", "活动网络接口累计接收错误数量。", "数值", "个", "状态", "0"),
        V("network.send_errors", "发送错误", "网络", "活动网络接口累计发送错误数量。", "数值", "个", "状态", "0"),

        // ===== Ping / 实时连通性 =====
        V("probe.target", "探测地址", "网络探测", "当前网络包探测器使用的 IPv4、IPv6 或域名。", "文本", use: "左侧信息 / 标题", formats: "无需格式化"),
        V("probe.address", "实际响应地址", "网络探测", "目标解析后实际参与检测或返回响应的 IP 地址。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("probe.protocol", "检测协议", "网络探测", "当前使用的检测协议：ICMP、TCP 或 UDP。", "文本", use: "左侧信息 / 标题", formats: "无需格式化"),
        V("probe.port", "检测端口", "网络探测", "TCP / UDP 检测端口；ICMP 模式返回 0。", "整数", use: "左侧信息", formats: "0"),
        V("probe.endpoint", "探测端点", "网络探测", "ICMP 显示目标地址；TCP / UDP 显示“地址:端口”。", "文本", use: "左侧信息 / 标题", formats: "无需格式化"),
        V("probe.online", "探测是否成功", "网络探测", "最近一次网络探测是否成功。", "布尔", use: "状态 / 条件", formats: "无需格式化"),
        V("probe.status_text", "探测状态文字", "网络探测", "最近一次探测状态，例如“在线”“超时”“端口不可达”。", "文本", use: "右侧状态 / 标题", formats: "无需格式化"),
        V("probe.reply_status", "探测原始状态", "网络探测", "协议探测返回的底层状态，例如 Success、Connected、Response、TimedOut。", "文本", use: "状态", formats: "无需格式化"),
        V("probe.latency_ms", "探测延迟", "网络探测", "最近一次成功探测的往返/连接延迟；失败时按 999 ms 处理。", unit: "ms", use: "左侧主体信息", formats: "0 / 0.0"),
        V("probe.latency_text", "探测延迟文字", "网络探测", "成功时显示“20ms”；失败时显示超时或错误状态。", "文本", use: "左侧主体信息", formats: "无需格式化"),
        V("probe.latency_progress", "延迟进度", "网络探测", "0 ms 为 0%，999 ms 及以上为 100%；适合用作延迟圆环。", unit: "%", use: "圆环"),
        V("probe.full_scale_ms", "延迟 100% 对应值", "网络探测", "延迟圆环 100% 对应 999 ms。", unit: "ms", use: "状态计算", formats: "0"),
        V("probe.timeout_ms", "单次探测超时", "网络探测", "单次 ICMP/TCP/UDP 探测等待响应的超时时间。", unit: "ms", use: "状态计算", formats: "0"),
        V("probe.ttl", "ICMP TTL", "网络探测", "最近一次 ICMP 成功响应的 TTL；TCP/UDP 为 0。", "整数", use: "左侧信息", formats: "0"),
        V("probe.sent", "探测样本数", "网络探测", "当前目标最近滚动统计窗口内的探测样本数量。", "整数", "次", "左侧信息", "0"),
        V("probe.received", "成功样本数", "网络探测", "当前目标最近滚动统计窗口内成功的探测次数。", "整数", "次", "左侧信息", "0"),
        V("probe.lost", "失败样本数", "网络探测", "当前目标最近滚动统计窗口内失败/超时次数。", "整数", "次", "左侧信息", "0"),
        V("probe.loss_percent", "丢包率", "网络探测", "当前地址、协议和端口组合最近 20 次探测的失败/超时比例。", unit: "%", use: "右侧状态 / 圆环"),
        V("probe.avg_latency_ms", "平均延迟", "网络探测", "当前探测配置最近 20 次成功样本的平均延迟。", unit: "ms", use: "左侧信息", formats: "0.0"),
        V("probe.min_latency_ms", "最低延迟", "网络探测", "当前探测配置最近 20 次成功样本中的最低延迟。", unit: "ms", use: "左侧信息", formats: "0"),
        V("probe.max_latency_ms", "最高延迟", "网络探测", "当前探测配置最近 20 次成功样本中的最高延迟。", unit: "ms", use: "左侧信息", formats: "0"),
        V("probe.jitter_ms", "延迟抖动", "网络探测", "相邻成功样本延迟差的平均绝对值，用于近似表示抖动。", unit: "ms", use: "左侧信息", formats: "0.0"),
        V("probe.last_success", "最近成功时间", "网络探测", "当前探测配置最近一次成功的本地时间。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("probe.error", "探测错误", "网络探测", "最近一次检测失败时的底层状态或错误。", "文本", use: "状态", formats: "无需格式化"),

        V("ping.target", "Ping 目标", "Ping（兼容）", "当前方案正在检测的 IP 地址或域名。", "文本", use: "左侧主值 / 标题", formats: "无需格式化"),
        V("ping.address", "Ping 实际地址", "Ping（兼容）", "Ping 响应返回的实际 IP 地址；域名目标解析成功后可查看最终地址。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("ping.protocol", "Ping/探测协议", "Ping（兼容）", "兼容变量：当前网络探测协议 ICMP、TCP 或 UDP。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("ping.port", "Ping/探测端口", "Ping（兼容）", "兼容变量：TCP / UDP 端口；ICMP 为 0。", "整数", use: "左侧信息", formats: "0"),
        V("ping.endpoint", "Ping/探测端点", "Ping（兼容）", "兼容变量：当前地址和端口组合。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("ping.online", "Ping 是否在线", "Ping（兼容）", "最近一次 ICMP Ping 是否成功。", "布尔", use: "状态 / 条件", formats: "无需格式化"),
        V("ping.status_text", "Ping 状态文字", "Ping（兼容）", "最近一次检测状态，例如“在线”“超时”“不可达”。", "文本", use: "右侧状态 / 标题", formats: "无需格式化"),
        V("ping.reply_status", "Ping 原始状态", "Ping（兼容）", "System.Net.NetworkInformation.IPStatus 返回的原始状态名称。", "文本", use: "状态", formats: "无需格式化"),
        V("ping.latency_ms", "Ping 延迟", "Ping（兼容）", "最近一次成功 Ping 的往返延迟；失败时按 999 ms 计入进度。", unit: "ms", use: "右侧状态", formats: "0 / 0.0"),
        V("ping.latency_text", "Ping 延迟文字", "Ping（兼容）", "成功时显示“20ms”一类文字；超时或不可达时显示对应状态。", "文本", use: "右侧状态", formats: "无需格式化"),
        V("ping.progress", "Ping 延迟进度", "Ping（兼容）", "延迟换算后的圆环进度：0 ms 为 0%，999 ms 及以上为 100%；超时/失败为 100%。", unit: "%", use: "右侧状态 / 圆环"),
        V("ping.full_scale_ms", "Ping 100% 对应延迟", "Ping（兼容）", "Ping 圆环 100% 对应的延迟，固定为 999 ms。", unit: "ms", use: "状态计算", formats: "0"),
        V("ping.timeout_ms", "Ping 超时时间", "Ping（兼容）", "单次 Ping 等待响应的超时时间。", unit: "ms", use: "状态计算", formats: "0"),
        V("ping.ttl", "Ping TTL", "Ping（兼容）", "最近一次成功响应返回的 TTL。", "整数", use: "左侧信息", formats: "0"),
        V("ping.sent", "Ping 统计发送次数", "Ping（兼容）", "当前目标最近滚动统计窗口内的 Ping 样本数量。", "整数", "次", "左侧信息", "0"),
        V("ping.received", "Ping 统计成功次数", "Ping（兼容）", "当前目标最近滚动统计窗口内成功的 Ping 样本数量。", "整数", "次", "左侧信息", "0"),
        V("ping.lost", "Ping 丢包次数", "Ping（兼容）", "当前目标最近滚动统计窗口内失败/超时的 Ping 样本数量。", "整数", "次", "左侧信息", "0"),
        V("ping.loss_percent", "Ping 丢包率", "Ping（兼容）", "当前目标最近 20 次检测的丢包率。", unit: "%", use: "右侧状态 / 圆环"),
        V("ping.avg_latency_ms", "Ping 平均延迟", "Ping（兼容）", "当前目标最近 20 次成功检测的平均延迟。", unit: "ms", use: "左侧信息", formats: "0.0"),
        V("ping.min_latency_ms", "Ping 最低延迟", "Ping（兼容）", "当前目标最近 20 次成功检测中的最低延迟。", unit: "ms", use: "左侧信息", formats: "0"),
        V("ping.max_latency_ms", "Ping 最高延迟", "Ping（兼容）", "当前目标最近 20 次成功检测中的最高延迟。", unit: "ms", use: "左侧信息", formats: "0"),
        V("ping.jitter_ms", "Ping 抖动", "Ping（兼容）", "当前目标最近成功样本相邻延迟差的平均绝对值，用于近似表示网络抖动。", unit: "ms", use: "左侧信息", formats: "0.0"),
        V("ping.last_success", "Ping 最近成功时间", "Ping（兼容）", "当前目标最近一次成功响应的本地时间。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("ping.error", "Ping 错误信息", "Ping（兼容）", "最近一次检测失败时的错误或状态说明。", "文本", use: "状态", formats: "无需格式化"),

        // ===== 磁盘 =====
        V("disk.system.root", "系统盘挂载点", "磁盘", "macOS 可写数据卷挂载路径。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("disk.system.filesystem", "系统盘文件系统", "磁盘", "系统盘文件系统，如 APFS。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("disk.system.used_bytes", "系统盘已用空间", "磁盘", "macOS 系统盘已使用空间。", unit: "Byte", use: "左侧主值", formats: "gb:1 / bytes"),
        V("disk.system.free_bytes", "系统盘可用空间", "磁盘", "macOS 系统盘可用空间。", unit: "Byte", use: "左侧信息", formats: "gb:1 / bytes"),
        V("disk.system.total_bytes", "系统盘总容量", "磁盘", "macOS 系统盘总容量。", unit: "Byte", use: "左侧次值", formats: "gb:1 / bytes"),
        V("disk.system.usage", "系统盘使用率", "磁盘", "系统盘已用空间占总容量的比例。", unit: "%", use: "右侧状态 / 圆环"),

        // ===== 系统 =====
        V("system.time", "当前时间", "系统", "本机当前时间，精确到秒。", "文本", use: "左侧信息 / 标题", formats: "无需格式化"),
        V("system.date", "当前日期", "系统", "本机当前日期。", "文本", use: "左侧信息 / 标题", formats: "无需格式化"),
        V("system.datetime", "当前日期时间", "系统", "本机当前日期与时间。", "文本", use: "左侧信息 / 标题", formats: "无需格式化"),
        V("system.uptime_seconds", "系统运行时间", "系统", "当前 macOS 自开机以来运行的秒数。", unit: "秒", use: "左侧信息", formats: "duration"),
        V("system.uptime_text", "系统运行时间文字", "系统", "系统运行时间的可读文字。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("system.boot_time", "系统启动时间", "系统", "根据系统运行时间估算的本次 macOS 启动时间。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("system.machine_name", "计算机名称", "系统", "macOS 计算机名称。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("system.host_name", "主机名", "系统", "DNS 主机名。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("system.user_name", "当前用户名", "系统", "当前 macOS 用户名。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("system.os_description", "操作系统名称", "系统", ".NET RuntimeInformation 提供的 macOS 操作系统描述。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("system.os_version", "操作系统版本", "系统", "macOS 操作系统版本号。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("system.os_architecture", "操作系统架构", "系统", "当前 macOS 架构，如 X64 / Arm64。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("system.process_architecture", "应用进程架构", "系统", "Endfield Charge Plus 当前进程架构。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("system.framework_version", ".NET 版本", "系统", "当前进程使用的 .NET FrameworkDescription。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("system.processor_count", "系统逻辑处理器数", "系统", "Environment.ProcessorCount。", "整数", "个", "左侧信息", "0"),
        V("system.timezone_id", "时区 ID", "系统", "当前本地时区 ID。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("system.timezone_name", "时区名称", "系统", "当前本地时区显示名称。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("system.utc_offset_hours", "UTC 时差", "系统", "当前时区相对 UTC 的小时偏移。", unit: "小时", use: "左侧信息", formats: "0.0"),
        V("system.culture", "系统区域语言", "系统", "当前进程文化区域名称，如 zh-CN。", "文本", use: "左侧信息", formats: "无需格式化"),

        // ===== 应用进程 =====
        V("app.name", "应用进程名称", "ECP 应用", "当前 Endfield Charge Plus 进程名称。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("app.version", "应用版本", "ECP 应用", "Endfield Charge Plus 产品版本。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("app.pid", "应用 PID", "ECP 应用", "当前应用进程 ID。", "整数", use: "左侧信息", formats: "0"),
        V("app.start_time", "应用启动时间", "ECP 应用", "当前应用进程启动时间。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("app.uptime_seconds", "应用运行时间", "ECP 应用", "当前应用进程已运行的秒数。", unit: "秒", use: "左侧信息", formats: "duration"),
        V("app.uptime_text", "应用运行时间文字", "ECP 应用", "当前应用运行时间的可读文字。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("app.working_set_bytes", "应用工作集", "ECP 应用", "Endfield Charge Plus 当前工作集内存。", unit: "Byte", use: "左侧主值", formats: "mb:1 / bytes"),
        V("app.virtual_memory_bytes", "应用虚拟内存", "ECP 应用", "当前应用进程虚拟内存大小。", unit: "Byte", use: "左侧信息", formats: "mb:1 / bytes"),
        V("app.cpu_time_seconds", "应用累计 CPU 时间", "ECP 应用", "当前应用进程累计消耗的处理器时间。", unit: "秒", use: "左侧信息", formats: "duration"),

        // ===== 显示器 =====
        V("display.primary_width_px", "主显示器宽度", "显示器", "macOS 主显示器宽度。", "整数", "px", "左侧信息", "0"),
        V("display.primary_height_px", "主显示器高度", "显示器", "macOS 主显示器高度。", "整数", "px", "左侧信息", "0"),
        V("display.virtual_x_px", "虚拟桌面 X 起点", "显示器", "多显示器虚拟桌面的左边界坐标。", "整数", "px", "左侧信息", "0"),
        V("display.virtual_y_px", "虚拟桌面 Y 起点", "显示器", "多显示器虚拟桌面的上边界坐标。", "整数", "px", "左侧信息", "0"),
        V("display.virtual_width_px", "虚拟桌面宽度", "显示器", "多显示器虚拟桌面总宽度。", "整数", "px", "左侧信息", "0"),
        V("display.virtual_height_px", "虚拟桌面高度", "显示器", "多显示器虚拟桌面总高度。", "整数", "px", "左侧信息", "0"),
        V("display.monitor_count", "显示器数量", "显示器", "macOS 当前检测到的显示器数量。", "整数", "台", "左侧信息", "0"),
        V("display.system_dpi", "系统 DPI", "显示器", "macOS 系统 DPI。", "整数", "DPI", "左侧信息", "0"),
        V("display.scale_percent", "系统缩放比例", "显示器", "根据系统 DPI 换算的显示缩放百分比。", unit: "%", use: "右侧状态 / 圆环"),

        // ===== 时间 =====
        V("time.current", "当前时间（24 小时）", "时间", "本地当前时间，格式 HH:mm:ss。", "文本", use: "左侧主值", formats: "无需格式化"),
        V("time.current_12h", "当前时间（12 小时）", "时间", "本地当前时间，12 小时制并包含 AM/PM。", "文本", use: "左侧主值", formats: "无需格式化"),
        V("time.date", "当前日期", "时间", "本地当前日期，格式 yyyy-MM-dd。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("time.datetime", "当前日期时间", "时间", "本地当前日期时间。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("time.iso", "ISO 日期时间", "时间", "当前本地时间的 ISO 8601 表示。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("time.year", "年份", "时间", "当前年份。", "整数", "年", "左侧信息", "0"),
        V("time.month", "月份", "时间", "当前月份数字。", "整数", "月", "左侧信息", "0"),
        V("time.month_name", "月份名称", "时间", "当前文化区域下的月份名称。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("time.day", "日期（日）", "时间", "当前月中的日期。", "整数", "日", "左侧信息", "0"),
        V("time.day_of_week", "星期（中文）", "时间", "当前星期的中文名称。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("time.day_of_week_en", "星期（英文）", "时间", "当前星期英文名称。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("time.day_of_year", "一年中的第几天", "时间", "当前日期是一年中的第几天。", "整数", "天", "左侧信息", "0"),
        V("time.week_of_year", "周序号", "时间", "ISO 风格的当前周序号。", "整数", "周", "左侧信息", "0"),
        V("time.hour", "小时", "时间", "当前小时（0-23）。", "整数", "时", "左侧信息", "0"),
        V("time.minute", "分钟", "时间", "当前分钟。", "整数", "分", "左侧信息", "0"),
        V("time.second", "秒", "时间", "当前秒。", "整数", "秒", "左侧信息", "0"),
        V("time.millisecond", "毫秒", "时间", "当前毫秒。", "整数", "ms", "左侧信息", "0"),
        V("time.is_weekend", "是否周末", "时间", "当前日期是否为周六或周日。", "布尔", use: "标题 / 条件", formats: "无需格式化"),
        V("time.unix_seconds", "Unix 时间戳（秒）", "时间", "当前 Unix 时间戳，单位秒。", "数值", "秒", "左侧信息", "0"),
        V("time.unix_milliseconds", "Unix 时间戳（毫秒）", "时间", "当前 Unix 时间戳，单位毫秒。", "数值", "ms", "左侧信息", "0"),
        V("time.day.progress", "当天进程", "时间", "从当天 00:00:00 到次日 00:00:00 已经过的百分比。", unit: "%", use: "右侧状态 / 圆环"),
        V("time.day.elapsed_seconds", "当天已过时间", "时间", "当天已经过的秒数。", unit: "秒", use: "左侧信息", formats: "duration"),
        V("time.day.remaining_seconds", "当天剩余时间", "时间", "距离次日 00:00:00 的剩余秒数。", unit: "秒", use: "左侧信息", formats: "duration"),
        V("time.week.progress", "本周进程", "时间", "从本周周一 00:00 到下周周一 00:00 的已过百分比。", unit: "%", use: "右侧状态 / 圆环"),
        V("time.week.elapsed_seconds", "本周已过时间", "时间", "从本周周一开始已经过的秒数。", unit: "秒", use: "左侧信息", formats: "duration"),
        V("time.week.remaining_seconds", "本周剩余时间", "时间", "距离下周周一的剩余秒数。", unit: "秒", use: "左侧信息", formats: "duration"),
        V("time.month.progress", "本月进程", "时间", "从本月 1 日 00:00 到下月 1 日 00:00 的已过百分比。", unit: "%", use: "右侧状态 / 圆环"),
        V("time.month.elapsed_seconds", "本月已过时间", "时间", "本月已经过的秒数。", unit: "秒", use: "左侧信息", formats: "duration"),
        V("time.month.remaining_seconds", "本月剩余时间", "时间", "距离下月 1 日的剩余秒数。", unit: "秒", use: "左侧信息", formats: "duration"),
        V("time.year.progress", "本年进程", "时间", "从今年 1 月 1 日到明年 1 月 1 日的已过百分比。", unit: "%", use: "右侧状态 / 圆环"),
        V("time.year.elapsed_seconds", "本年已过时间", "时间", "今年已经过的秒数。", unit: "秒", use: "左侧信息", formats: "duration"),
        V("time.year.remaining_seconds", "本年剩余时间", "时间", "距离明年 1 月 1 日的剩余秒数。", unit: "秒", use: "左侧信息", formats: "duration"),
        V("time.display.progress", "时间方案显示进度", "时间", "时间方案用于圆环的进度；目标时间模式下为剩余比例。", unit: "%", use: "右侧状态 / 圆环"),
        V("time.display.status_text", "时间方案状态文字", "时间", "普通模式显示当天进程；目标时间模式显示“剩余??%”。", "文本", use: "右侧状态", formats: "无需格式化"),
        V("time.target.value", "目标时间", "时间", "当前时间方案设置的每日目标时间。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("time.target.remaining_seconds", "距离目标时间", "时间", "距离下一次每日目标时间的剩余秒数。", unit: "秒", use: "左侧信息", formats: "duration"),
        V("time.target.remaining_percent", "目标时间剩余比例", "时间", "距离下一次目标时间的剩余时长占 24 小时的百分比。", unit: "%", use: "右侧状态 / 圆环"),
        V("time.target.progress", "目标时间已过比例", "时间", "每日目标时间周期中已经过的比例。", unit: "%", use: "右侧状态 / 圆环"),
        V("time.target.remaining_text", "目标时间剩余文字", "时间", "按“剩余??%”生成的目标时间状态文字。", "文本", use: "右侧状态", formats: "无需格式化"),

        // ===== DeepSeek API =====
        V("deepseek.balance", "DeepSeek 总余额", "DeepSeek API", "DeepSeek API 账户人民币总余额。", unit: "CNY", use: "左侧主值", formats: "0.00"),
        V("deepseek.balance_text", "DeepSeek 总余额文字", "DeepSeek API", "已格式化为“¥0.00”的总余额文字。", "文本", use: "左侧主值", formats: "无需格式化"),
        V("deepseek.granted_balance", "DeepSeek 赠送余额", "DeepSeek API", "DeepSeek API 账户赠送余额。", unit: "CNY", use: "左侧信息", formats: "0.00"),
        V("deepseek.topped_up_balance", "DeepSeek 充值余额", "DeepSeek API", "DeepSeek API 账户充值余额。", unit: "CNY", use: "左侧信息", formats: "0.00"),
        V("deepseek.available", "DeepSeek API 可用状态", "DeepSeek API", "余额接口返回的账户可用状态。", "布尔", use: "标题 / 条件", formats: "无需格式化"),
        V("deepseek.available_text", "DeepSeek API 可用状态文字", "DeepSeek API", "根据可用状态生成“可用 / 不可用”。", "文本", use: "标题 / 右侧状态", formats: "无需格式化"),
        V("deepseek.period.name", "当前时段（英文）", "DeepSeek API", "当前为 PEAK 或 OFF-PEAK。", "文本", use: "标题", formats: "无需格式化"),
        V("deepseek.period.name_zh", "当前时段（中文）", "DeepSeek API", "当前时段中文名称：高峰或低谷。", "文本", use: "标题", formats: "无需格式化"),
        V("deepseek.period.is_peak", "是否高峰", "DeepSeek API", "当前是否处于高峰时段。", "布尔", use: "标题 / 条件", formats: "无需格式化"),
        V("deepseek.period.is_off_peak", "是否低谷", "DeepSeek API", "当前是否处于低谷时段。", "布尔", use: "标题 / 条件", formats: "无需格式化"),
        V("deepseek.period.remaining_seconds", "距离时段切换", "DeepSeek API", "距离下一次峰谷时段切换的剩余秒数。", unit: "秒", use: "左侧次值", formats: "duration"),
        V("deepseek.period.remaining_text", "时段剩余文字", "DeepSeek API", "“高峰时段剩余??:??:??”或“低谷时段剩余??:??:??”。", "文本", use: "左侧次值", formats: "无需格式化"),
        V("deepseek.period.progress", "当前时段已过进度", "DeepSeek API", "当前峰谷时段已经经过的百分比。", unit: "%", use: "右侧状态 / 圆环"),
        V("deepseek.period.progress_text", "时段已过文字", "DeepSeek API", "“高峰已过??%”或“低谷已过??%”。", "文本", use: "右侧状态", formats: "无需格式化"),
        V("deepseek.period.next_switch_time", "下次切换时间（北京时间）", "DeepSeek API", "下一次高峰/低谷切换的北京时间，格式 HH:mm:ss。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("deepseek.period.next_switch_datetime", "下次切换日期时间（北京时间）", "DeepSeek API", "下一次峰谷切换的北京时间日期与时间，格式 yyyy-MM-dd HH:mm:ss。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("deepseek.period.next_switch_time_local", "下次切换时间（本地）", "DeepSeek API", "将下一次峰谷切换时刻转换为当前 macOS 用户时区后的本地时间，格式 HH:mm:ss；自动考虑当地夏令时。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("deepseek.period.next_switch_datetime_local", "下次切换日期时间（本地）", "DeepSeek API", "将下一次峰谷切换时刻转换为当前 macOS 用户时区后的本地日期与时间，格式 yyyy-MM-dd HH:mm:ss；自动考虑当地夏令时。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("deepseek.period.timezone", "DeepSeek 峰谷基准时区", "DeepSeek API", "DeepSeek 峰谷规则使用的固定基准时区：北京时间 UTC+08:00。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("deepseek.period.local_timezone", "用户本地时区", "DeepSeek API", "当前 macOS 本地时区 ID，以及下一次切换时刻对应的 UTC 偏移；夏令时地区会按切换日期自动计算偏移。", "文本", use: "左侧信息", formats: "无需格式化"),
    };

    private static readonly IReadOnlyList<VariableDefinition> AdvancedBuiltIns = new List<VariableDefinition>
    {
        // ===== 电池进阶 =====
        V("battery.estimated_time_to_empty", "预计耗尽剩余时间", "电池", "macOS 电源管理估算的距离电池耗尽剩余秒数；系统无法估算时变量不可用。", unit: "秒", use: "左侧信息", formats: "duration / duration-long"),

        // ===== CPU 进阶 =====

        // ===== 内存进阶 =====

        // ===== GPU 进阶 =====

        // ===== 网络进阶 =====
        V("network.public_ipv4", "公网 IPv4", "网络", "通过 api.ipify.org 查询的公网 IPv4，5 分钟缓存；仅使用该变量时才发起请求。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("network.public_ipv6", "公网 IPv6", "网络", "通过 api6.ipify.org 查询的公网 IPv6，5 分钟缓存；没有 IPv6 时变量不可用。", "文本", use: "左侧信息", formats: "无需格式化"),

        // ===== 系统进阶 =====

        // ===== 进程进阶 =====

        // ===== 软件自身进阶 =====
        V("app.theme", "应用主题", "ECP 应用", "当前 Endfield Charge Plus 使用的主题。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("app.preset_name", "当前方案名称", "ECP 应用", "当前选中的 HUD 方案显示名称。", "文本", use: "标题 / 左侧信息", formats: "无需格式化"),
        V("app.active_profile", "当前方案 ID", "ECP 应用", "当前 HUD 方案内部 ID。", "文本", use: "条件", formats: "无需格式化"),

        // ===== 显示器进阶 =====

        // ===== 世界时间 =====
        V("time.world.nyc", "纽约时间", "时间", "按 macOS Eastern Standard Time 实时换算。", "文本", use: "左侧主值", formats: "无需格式化"),
        V("time.world.london", "伦敦时间", "时间", "按 macOS GMT Standard Time 实时换算。", "文本", use: "左侧主值", formats: "无需格式化"),
        V("time.world.tokyo", "东京时间", "时间", "按 macOS Tokyo Standard Time 实时换算。", "文本", use: "左侧主值", formats: "无需格式化"),
        V("time.world.beijing", "北京时间", "时间", "按 macOS China Standard Time 实时换算。", "文本", use: "左侧主值", formats: "无需格式化"),

        // ===== 网络包探测器进阶 =====
        V("probe.dns_resolve_time", "DNS 解析耗时", "网络探测", "当检测地址为域名时，当前探测周期的 DNS 解析耗时；直接 IP 时为 0。", unit: "ms", use: "左侧信息", formats: "0.0"),
        V("probe.tcp_connect_time", "TCP 建连耗时", "网络探测", "TCP 模式下从发起连接到连接成功的耗时。", unit: "ms", use: "左侧信息", formats: "0.0"),

        // ===== DeepSeek API 进阶 =====
        V("deepseek.api.latency_ms", "DeepSeek API 延迟", "DeepSeek API", "最近一次官方 /user/balance 请求的 HTTP 往返耗时；余额请求 1 分钟缓存。", unit: "ms", use: "右侧状态 / 圆环", formats: "0.0"),

        // ===== 剪贴板 =====
        V("clipboard.has_text", "剪贴板含文本", "剪贴板", "当前 macOS 剪贴板是否包含 Unicode 文本。", "布尔", use: "状态 / 条件", formats: "无需格式化"),
        V("clipboard.text_length", "剪贴板文本长度", "剪贴板", "当前剪贴板文本字符数。", "整数", "字符", "左侧信息", "0"),
        V("clipboard.preview", "剪贴板文本预览", "剪贴板", "当前剪贴板文本去除多余空白后的前 80 个字符。", "文本", use: "左侧信息", formats: "无需格式化"),
        V("clipboard.has_image", "剪贴板含图像", "剪贴板", "当前剪贴板是否包含 Bitmap/DIB 图像格式。", "布尔", use: "状态 / 条件", formats: "无需格式化"),
        V("clipboard.last_updated", "剪贴板最后变化时间", "剪贴板", "应用观察到 Clipboard Sequence Number 变化的本地时间。", "文本", use: "左侧信息", formats: "time:HH:mm:ss"),

        // ===== USB / 外设 =====

        // ===== 开发者工具 =====

        // ===== 安全 =====
    };

    private static readonly HashSet<string> AdvancedKeySet = new(AdvancedBuiltIns.Select(x => x.Key), StringComparer.OrdinalIgnoreCase);

    internal static bool IsAdvancedKey(string key) => AdvancedKeySet.Contains(key);

    // Variable Library display order is intentionally semantic rather than alphabetical.
    // This keeps Chinese and English UI ordering identical and stable across localization.
    private static readonly IReadOnlyDictionary<string, int> CategoryDisplayRank =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["CPU"] = 0,
            ["GPU"] = 1,
            ["内存"] = 2,
            ["磁盘"] = 3,
            ["电池"] = 4,
            ["网络"] = 5,
            ["网络探测"] = 6,
            ["Ping（兼容）"] = 7,
            ["系统"] = 8,
            ["显示器"] = 9,
            ["时间"] = 10,
            ["进程"] = 11,
            ["ECP 应用"] = 12,
            ["DeepSeek API"] = 13,
            ["安全"] = 14,
            ["USB / 外设"] = 15,
            ["剪贴板"] = 16,
            ["开发者工具"] = 17,
            ["自定义数据"] = 18,
        };

    // Within a category, common live/status values are shown before detailed/static values.
    // Prefixes are only display hints; variable keys and runtime behavior are not changed.
    private static readonly IReadOnlyDictionary<string, string[]> VariableDisplayPrefixes =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["CPU"] =
            [
                "cpu.usage", "cpu.idle", "cpu.frequency",
                "cpu.temperature", "cpu.power", "cpu.core", "cpu.bus",
                "cpu.name", "cpu.manufacturer", "cpu.architecture",
                "cpu.physical", "cpu.logical", "cpu.socket", "cpu.virtualization",
                "cpu.l2", "cpu.l3",
                "cpu.instructions", "cpu.context", "cpu.interrupts", "cpu.dpc", "cpu.system_calls"
            ],
            ["GPU"] =
            [
                "gpu.usage",
                "gpu.temperature", "gpu.hotspot", "gpu.memory_junction",
                "gpu.power", "gpu.voltage", "gpu.core_clock", "gpu.memory_clock",
                "gpu.dedicated_used", "gpu.dedicated_total", "gpu.dedicated_usage",
                "gpu.shared_used", "gpu.shared_limit", "gpu.shared_usage",
                "gpu.memory_used", "gpu.memory_total",
                "gpu.total_memory_used", "gpu.total_memory_limit", "gpu.total_memory_usage",
                "gpu.vram", "gpu.uses_unified_memory",
                "gpu.name", "gpu.count", "gpu.physical_index", "gpu.adapter_id",
                "gpu.pcie", "gpu.driver", "gpu.bios",
                "gpu.nvidia_smi", "gpu.amd_adrenalin"
            ],
            ["内存"] =
            [
                "memory.usage", "memory.used", "memory.available", "memory.total", "memory.free_percent",
                "memory.commit",
                "memory.virtual",
                "memory.cache", "memory.standby", "memory.modified",
                "memory.paged_pool", "memory.nonpaged_pool",
                "memory.hardware_reserved",
                "memory.speed", "memory.slot", "memory.form_factor", "memory.type"
            ],
            ["磁盘"] =
            [
                "disk.system.usage", "disk.system.used", "disk.system.free", "disk.system.total",
                "disk.system.read", "disk.system.write", "disk.system.io",
                "disk.system.active", "disk.system.queue",
                "disk.system.root", "disk.system.label", "disk.system.filesystem",
                "disk.fixed.usage", "disk.fixed.used", "disk.fixed.free", "disk.fixed.total",
                "disk.fixed.count", "disk.fixed.list"
            ],
            ["电池"] =
            [
                "battery.status_text", "battery.percent", "battery.power_source",
                "battery.ac_online", "battery.charging", "battery.discharging", "battery.saver_on",
                "battery.rate_watts", "battery.charge_rate_watts", "battery.discharge_rate_watts",
                "battery.remaining_wh", "battery.full_wh", "battery.design_wh",
                "battery.remaining_mwh", "battery.full_mwh", "battery.design_mwh",
                "battery.health_percent", "battery.design_vs_current_health",
                "battery.time_remaining", "battery.estimated_time_to_empty", "battery.estimated_time_to_full",
                "battery.full_life",
                "battery.voltage", "battery.temperature", "battery.cycle_count", "battery.chemistry"
            ],
            ["网络"] =
            [
                "network.available",
                "network.download", "network.upload", "network.total_bps",
                "network.download_mbps", "network.upload_mbps", "network.total_mbps",
                "network.display", "network.profile",
                "network.link_speed", "network.max_link_speed", "network.utilization",
                "network.active_interface", "network.interface",
                "network.ipv4", "network.ipv6", "network.default_gateways", "network.dns_servers",
                "network.public",
                "network.signal_dbm", "network.wifi",
                "network.vpn", "network.proxy",
                "network.dns_latency", "network.tcp_connections", "network.udp_connections",
                "network.total_received", "network.total_sent", "network.total_transferred",
                "network.packets", "network.receive_errors", "network.send_errors"
            ],
            ["网络探测"] =
            [
                "probe.target", "probe.endpoint", "probe.protocol", "probe.port", "probe.address",
                "probe.online", "probe.status_text", "probe.reply_status",
                "probe.latency_ms", "probe.latency_text", "probe.latency_progress",
                "probe.avg_latency", "probe.min_latency", "probe.max_latency", "probe.jitter",
                "probe.loss_percent", "probe.sent", "probe.received", "probe.lost",
                "probe.full_scale", "probe.timeout", "probe.ttl",
                "probe.dns_resolve_time", "probe.tcp_connect_time",
                "probe.last_success", "probe.error"
            ],
            ["Ping（兼容）"] =
            [
                "ping.target", "ping.endpoint", "ping.protocol", "ping.port", "ping.address",
                "ping.online", "ping.status_text", "ping.reply_status",
                "ping.latency_ms", "ping.latency_text", "ping.progress",
                "ping.avg_latency", "ping.min_latency", "ping.max_latency", "ping.jitter",
                "ping.loss_percent", "ping.sent", "ping.received", "ping.lost",
                "ping.full_scale", "ping.timeout", "ping.ttl",
                "ping.last_success", "ping.error"
            ],
            ["系统"] =
            [
                "system.os_description", "system.os_version", "system.os_build",
                "system.os_architecture", "system.process_architecture", "system.framework_version",
                "system.machine_name", "system.host_name", "system.user_name", "system.user_domain",
                "system.uptime", "system.boot_time",
                "system.timezone", "system.utc_offset", "system.culture",
                "system.processor_count",
                "system.power_plan_name", "system.power_plan",
                "system.bios", "system.motherboard", "system.fan",
                "system.update", "system.defender", "system.firewall", "system.bitlocker",
                "system.hyper_v", "system.wsl",
                "system.time", "system.date", "system.datetime"
            ],
            ["显示器"] =
            [
                "display.monitor_count",
                "display.primary.name", "display.primary_width", "display.primary_height",
                "display.primary.refresh_rate", "display.primary.color_depth",
                "display.system_dpi", "display.scale_percent",
                "display.virtual",
                "display.secondary"
            ],
            ["时间"] =
            [
                "time.current", "time.current_12h", "time.date", "time.datetime", "time.iso",
                "time.world",
                "time.year", "time.month", "time.month_name", "time.day",
                "time.day_of_week", "time.day_of_year", "time.week_of_year",
                "time.hour", "time.minute", "time.second", "time.millisecond",
                "time.is_weekend", "time.unix",
                "time.day.progress", "time.day.elapsed", "time.day.remaining",
                "time.week.progress", "time.week.elapsed", "time.week.remaining",
                "time.month.progress", "time.month.elapsed", "time.month.remaining",
                "time.year.progress", "time.year.elapsed", "time.year.remaining",
                "time.display",
                "time.target"
            ],
            ["进程"] =
            [
                "process.top_cpu", "process.top_memory", "process.top_disk", "process.gpu.top",
                "process.background"
            ],
            ["ECP 应用"] =
            [
                "app.name", "app.version", "app.theme", "app.preset_name", "app.active_profile",
                "app.pid", "app.start_time", "app.uptime",
                "app.cpu_time", "app.gpu_usage",
                "app.working_set", "app.private_memory", "app.virtual_memory",
                "app.thread_count", "app.handle_count"
            ],
            ["DeepSeek API"] =
            [
                "deepseek.available", "deepseek.api.latency",
                "deepseek.balance_text", "deepseek.balance", "deepseek.granted_balance", "deepseek.topped_up_balance",
                "deepseek.period.name", "deepseek.period.is_peak", "deepseek.period.is_off_peak",
                "deepseek.period.remaining", "deepseek.period.progress",
                "deepseek.period.next_switch_time_local", "deepseek.period.next_switch_datetime_local",
                "deepseek.period.local_timezone",
                "deepseek.period.next_switch_time", "deepseek.period.next_switch_datetime",
                "deepseek.period.timezone"
            ],
            ["安全"] =
            [
                "security.secure_boot", "security.tpm", "security.uac", "security.smartscreen",
                "security.defender", "security.firewall", "security.bitlocker",
                "security.windows_update", "security.vpn", "security.proxy"
            ],
            ["USB / 外设"] =
            [
                "usb.device", "usb.storage",
                "peripheral.mouse", "peripheral.keyboard", "peripheral.gamepad"
            ],
            ["剪贴板"] =
            [
                "clipboard.has_text", "clipboard.preview", "clipboard.text_length",
                "clipboard.has_image", "clipboard.image_size",
                "clipboard.file_count", "clipboard.last_updated"
            ],
            ["开发者工具"] =
            [
                "dev.ide", "dev.vscode", "dev.terminal",
                "dev.git",
                "dev.node", "dev.python", "dev.java", "dev.golang", "dev.rust",
                "dev.docker", "dev.wsl", "dev.llm"
            ],
        };

    private static int GetCategoryDisplayRank(string category) =>
        CategoryDisplayRank.TryGetValue(category, out int rank) ? rank : int.MaxValue;

    private static int GetVariablePrefixRank(VariableDefinition item)
    {
        if (!VariableDisplayPrefixes.TryGetValue(item.Category, out var prefixes))
            return int.MaxValue;

        for (int i = 0; i < prefixes.Length; i++)
        {
            if (item.Key.StartsWith(prefixes[i], StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return int.MaxValue;
    }

    public static IReadOnlyList<VariableDefinition> AllBuiltIns => MacVariableCatalog.Build(StaticBuiltIns.Concat(AdvancedBuiltIns));

    internal static IEnumerable<VariableDefinition> PlatformIndependentDefinitions => StaticBuiltIns.Concat(AdvancedBuiltIns);

    public static VariableDefinition? Find(string key) =>
        AllBuiltIns.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));

    public static VariableDefinition CreateCustom(string key) => new(
        key,
        key.Split('.').LastOrDefault() ?? key,
        "自定义数据",
        "来自 HTTP / JSON 数据源的自定义变量。实际含义由数据源映射决定。",
        "动态",
        "",
        "按数据含义决定",
        "0 / 0.0 / gb:1 / speed / duration 等");

}
