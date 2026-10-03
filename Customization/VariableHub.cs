using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EndfieldChargePlus.Customization;

public sealed class VariableHub : IDisposable
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(8) };
    private readonly HttpClient _http;
    private readonly Dictionary<string, HttpCacheEntry> _httpCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, PingTargetState> _pingStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _pingGate = new();
    private readonly AdvancedVariableProvider _advanced;
    private readonly MacVariableProvider _mac = new();

    public VariableHub(HttpClient? http = null)
    {
        _http = http ?? Http;
        _advanced = new AdvancedVariableProvider(http);
    }

    private const int PingTimeoutMs = 1000;
    private const double PingFullScaleMs = 999d;

    private DeepSeekCache? _deepSeekCache;
    private string _deepSeekCacheKey = "";
    private AppLanguage _lastLanguage = LocalizationManager.Current;
    public async Task<Dictionary<string, object?>> SnapshotAsync(CustomHudSettings settings,
        IEnumerable<string>? requestedVariables = null, string? gpuAdapterId = null,
        string? pingTarget = null, string? probeProtocol = null, int probePort = 443,
        CancellationToken ct = default)
    {
        if (_lastLanguage != LocalizationManager.Current)
        {
            _lastLanguage = LocalizationManager.Current;
            lock (_pingGate) _pingStates.Clear();
        }
        var requested = requestedVariables?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var vars = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (NeedsPrefix(requested, "system.") || NeedsPrefix(requested, "time."))
            AddClockAndSystem(vars, NeedsPrefix(requested, "system."), NeedsPrefix(requested, "time."));
        if (NeedsPrefix(requested, "app."))
        {
            AddApp(vars);
            vars["app.theme"] = "Endfield";
            vars["app.active_profile"] = settings.ActiveProfileId;
            vars["app.preset_name"] = settings.Profiles.FirstOrDefault(p => p.Id == settings.ActiveProfileId)?.Name ?? "";
        }
        await Task.Run(() => _mac.Collect(vars, requested, gpuAdapterId), ct).ConfigureAwait(false);
        if ((NeedsPrefix(requested, "cpu.") && !vars.ContainsKey("cpu.usage")) ||
            (NeedsPrefix(requested, "network.") && !vars.ContainsKey("network.download_bps")))
        {
            await Task.Delay(100, ct).ConfigureAwait(false);
            await Task.Run(() => _mac.Collect(vars, requested, gpuAdapterId), ct).ConfigureAwait(false);
        }
        if (NeedsPrefix(requested, "display.") || NeedsPrefix(requested, "clipboard."))
            await MacUiVariables.CollectAsync(vars, NeedsPrefix(requested, "display."), NeedsPrefix(requested, "clipboard."));
        if (NeedsPrefix(requested, "ping.") || NeedsPrefix(requested, "probe."))
            await AddPingAsync(vars, pingTarget, probeProtocol, probePort, ct).ConfigureAwait(false);
        await _advanced.EnrichAsync(vars, settings, requested, gpuAdapterId, ct).ConfigureAwait(false);
        if (NeedsPrefix(requested, "deepseek.period.")) AddDeepSeekPeriod(vars, settings);
        if (NeedsPrefix(requested, "deepseek.") && !NeedsOnlyPeriodVariables(requested))
            await AddDeepSeekBalanceAsync(vars, settings, ct).ConfigureAwait(false);
        if (NeedsPrefix(requested, "custom.")) await AddCustomHttpAsync(vars, settings, ct, requested).ConfigureAwait(false);
        return vars;
    }
    public static IReadOnlyList<string> BuiltInVariableKeys => VariableCatalog.AllBuiltIns.Select(x => x.Key).ToList();

    private static void AddClockAndSystem(IDictionary<string, object?> v, bool includeSystem, bool includeTime)
    {
        var now = DateTime.Now;
        var uptimeSeconds = Math.Max(0d, Environment.TickCount64 / 1000d);

        if (includeSystem)
        {
            var tz = TimeZoneInfo.Local;
            var osVersion = Environment.OSVersion.Version;
            v["system.time"] = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            v["system.date"] = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            v["system.datetime"] = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            v["system.uptime_seconds"] = uptimeSeconds;
            v["system.uptime_text"] = FormatDurationLong(uptimeSeconds);
            v["system.boot_time"] = now.AddSeconds(-uptimeSeconds).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            v["system.machine_name"] = Environment.MachineName;
            try { v["system.host_name"] = Dns.GetHostName(); } catch { v["system.host_name"] = Environment.MachineName; }
            v["system.user_name"] = Environment.UserName;
            v["system.os_description"] = RuntimeInformation.OSDescription.Trim();
            v["system.os_version"] = osVersion.ToString();
            v["system.os_architecture"] = RuntimeInformation.OSArchitecture.ToString();
            v["system.process_architecture"] = RuntimeInformation.ProcessArchitecture.ToString();
            v["system.framework_version"] = RuntimeInformation.FrameworkDescription;
            v["system.processor_count"] = Environment.ProcessorCount;
            v["system.timezone_id"] = tz.Id;
            v["system.timezone_name"] = tz.DisplayName;
            v["system.utc_offset_hours"] = tz.GetUtcOffset(now).TotalHours;
            v["system.culture"] = CultureInfo.CurrentCulture.Name;
        }

        if (includeTime)
        {
            var seconds = now.TimeOfDay.TotalSeconds;
            var dayTotal = TimeSpan.FromDays(1).TotalSeconds;
            var weekStart = now.Date.AddDays(-(((int)now.DayOfWeek + 6) % 7));
            var weekEnd = weekStart.AddDays(7);
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var monthEnd = monthStart.AddMonths(1);
            var yearStart = new DateTime(now.Year, 1, 1);
            var yearEnd = yearStart.AddYears(1);
            double weekElapsed = Math.Max(0d, (now - weekStart).TotalSeconds);
            double weekTotal = Math.Max(1d, (weekEnd - weekStart).TotalSeconds);
            double monthElapsed = Math.Max(0d, (now - monthStart).TotalSeconds);
            double monthTotal = Math.Max(1d, (monthEnd - monthStart).TotalSeconds);
            double yearElapsed = Math.Max(0d, (now - yearStart).TotalSeconds);
            double yearTotal = Math.Max(1d, (yearEnd - yearStart).TotalSeconds);
            var dto = new DateTimeOffset(now);

            v["time.current"] = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            v["time.current_12h"] = now.ToString("hh:mm:ss tt", CultureInfo.CurrentCulture);
            v["time.date"] = now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            v["time.datetime"] = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            v["time.iso"] = dto.ToString("O", CultureInfo.InvariantCulture);
            v["time.year"] = now.Year;
            v["time.month"] = now.Month;
            v["time.month_name"] = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(now.Month);
            v["time.day"] = now.Day;
            v["time.day_of_week"] = DayOfWeekZh(now.DayOfWeek);
            v["time.day_of_week_en"] = now.DayOfWeek.ToString();
            v["time.day_of_year"] = now.DayOfYear;
            v["time.week_of_year"] = ISOWeek.GetWeekOfYear(now);
            v["time.hour"] = now.Hour;
            v["time.minute"] = now.Minute;
            v["time.second"] = now.Second;
            v["time.millisecond"] = now.Millisecond;
            v["time.is_weekend"] = now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            v["time.unix_seconds"] = dto.ToUnixTimeSeconds();
            v["time.unix_milliseconds"] = dto.ToUnixTimeMilliseconds();

            v["time.day.elapsed_seconds"] = seconds;
            v["time.day.remaining_seconds"] = Math.Max(0d, dayTotal - seconds);
            v["time.day.progress"] = Math.Clamp(seconds / dayTotal * 100d, 0d, 100d);
            v["time.week.elapsed_seconds"] = weekElapsed;
            v["time.week.remaining_seconds"] = Math.Max(0d, weekTotal - weekElapsed);
            v["time.week.progress"] = Math.Clamp(weekElapsed / weekTotal * 100d, 0d, 100d);
            v["time.month.elapsed_seconds"] = monthElapsed;
            v["time.month.remaining_seconds"] = Math.Max(0d, monthTotal - monthElapsed);
            v["time.month.progress"] = Math.Clamp(monthElapsed / monthTotal * 100d, 0d, 100d);
            v["time.year.elapsed_seconds"] = yearElapsed;
            v["time.year.remaining_seconds"] = Math.Max(0d, yearTotal - yearElapsed);
            v["time.year.progress"] = Math.Clamp(yearElapsed / yearTotal * 100d, 0d, 100d);
        }
    }

    private static void AddApp(IDictionary<string, object?> v)
    {
        try
        {
            using var p = Process.GetCurrentProcess();
            var asm = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
            var version = asm.GetName().Version?.ToString(3) ?? "0.1.0";
            var start = p.StartTime;
            var uptime = Math.Max(0d, (DateTime.Now - start).TotalSeconds);
            v["app.name"] = ProductInfo.Name;
            v["app.version"] = version;
            v["app.pid"] = p.Id;
            v["app.start_time"] = start.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            v["app.uptime_seconds"] = uptime;
            v["app.uptime_text"] = FormatDurationLong(uptime);
            v["app.working_set_bytes"] = (double)p.WorkingSet64;
            v["app.virtual_memory_bytes"] = (double)p.VirtualMemorySize64;
            v["app.cpu_time_seconds"] = p.TotalProcessorTime.TotalSeconds;
        }
        catch { }
    }

    private async Task AddPingAsync(
        IDictionary<string, object?> v,
        string? configuredTarget,
        string? configuredProtocol,
        int configuredPort,
        CancellationToken ct)
    {
        string target = string.IsNullOrWhiteSpace(configuredTarget) ? "1.1.1.1" : configuredTarget.Trim();
        string protocol = NormalizeProbeProtocol(configuredProtocol);
        int port = Math.Clamp(configuredPort <= 0 ? 443 : configuredPort, 1, 65535);
        string stateKey = $"{protocol}|{target}|{(protocol == "ICMP" ? 0 : port)}";

        PingTargetState state;
        Task<PingProbeResult>? probeTask = null;
        var now = DateTime.UtcNow;

        lock (_pingGate)
        {
            if (!_pingStates.TryGetValue(stateKey, out var existingState))
            {
                state = new PingTargetState();
                _pingStates[stateKey] = state;
            }
            else
            {
                state = existingState;
            }

            if (state.LastProbe is null || now >= state.NextProbeUtc)
            {
                if (state.InFlight is null || state.InFlight.IsCompleted)
                {
                    state.InFlight = ProbeEndpointAsync(target, protocol, port, ct);
                    state.NextProbeUtc = now.AddMilliseconds(800);
                }
                probeTask = state.InFlight;
            }
        }

        if (probeTask is not null)
        {
            PingProbeResult result;
            try { result = await probeTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                result = new PingProbeResult(false, IPStatus.Unknown, 0, "", 0, LocalizationManager.Text("检测失败", "Failed"), ex.Message, DateTime.Now);
            }

            lock (_pingGate)
            {
                state.LastProbe = result;
                state.InFlight = null;
                state.History.Enqueue(result);
                while (state.History.Count > 20)
                    state.History.Dequeue();
                if (result.Success)
                    state.LastSuccessLocal = result.LocalTime;
            }
        }

        PingProbeResult current;
        List<PingProbeResult> history;
        DateTime? lastSuccess;
        lock (_pingGate)
        {
            current = state.LastProbe ?? new PingProbeResult(false, IPStatus.TimedOut, 0, "", 0, LocalizationManager.Text("等待检测", "Waiting"), LocalizationManager.Text("尚未完成检测", "Probe not completed"), DateTime.Now);
            history = state.History.ToList();
            lastSuccess = state.LastSuccessLocal;
        }

        int sent = history.Count;
        int received = history.Count(x => x.Success);
        int lost = Math.Max(0, sent - received);
        double loss = sent <= 0 ? (current.Success ? 0d : 100d) : lost * 100d / sent;
        var successLatencies = history.Where(x => x.Success).Select(x => (double)x.LatencyMs).ToList();
        double avg = successLatencies.Count == 0 ? 0d : successLatencies.Average();
        double min = successLatencies.Count == 0 ? 0d : successLatencies.Min();
        double max = successLatencies.Count == 0 ? 0d : successLatencies.Max();
        double jitter = 0d;
        if (successLatencies.Count > 1)
        {
            double sum = 0d;
            for (int i = 1; i < successLatencies.Count; i++)
                sum += Math.Abs(successLatencies[i] - successLatencies[i - 1]);
            jitter = sum / (successLatencies.Count - 1);
        }

        double latencyForProgress = current.Success ? current.LatencyMs : PingFullScaleMs;
        double latencyProgress = Math.Clamp(latencyForProgress / PingFullScaleMs * 100d, 0d, 100d);
        double latencyMs = current.Success ? current.LatencyMs : PingFullScaleMs;
        string latencyText = current.Success ? $"{current.LatencyMs}ms" : current.StatusText;
        string endpoint = protocol == "ICMP" ? target : $"{target}:{port}";

        // Protocol-neutral variables used by the built-in network packet probe preset.
        v["probe.target"] = target;
        v["probe.address"] = current.Address;
        v["probe.protocol"] = protocol;
        v["probe.port"] = protocol == "ICMP" ? 0 : port;
        v["probe.endpoint"] = endpoint;
        v["probe.online"] = current.Success;
        v["probe.status_text"] = current.StatusText;
        v["probe.reply_status"] = current.ReplyStatus;
        v["probe.latency_ms"] = latencyMs;
        v["probe.latency_text"] = latencyText;
        v["probe.latency_progress"] = latencyProgress;
        v["probe.full_scale_ms"] = PingFullScaleMs;
        v["probe.timeout_ms"] = PingTimeoutMs;
        v["probe.ttl"] = current.Ttl;
        v["probe.sent"] = sent;
        v["probe.received"] = received;
        v["probe.lost"] = lost;
        v["probe.loss_percent"] = Math.Clamp(loss, 0d, 100d);
        v["probe.avg_latency_ms"] = avg;
        v["probe.min_latency_ms"] = min;
        v["probe.max_latency_ms"] = max;
        v["probe.jitter_ms"] = jitter;
        v["probe.last_success"] = lastSuccess?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";
        v["probe.error"] = current.Success ? "" : current.Error;
        if (current.DnsResolveMs.HasValue) v["probe.dns_resolve_time"] = current.DnsResolveMs.Value;
        if (current.TcpConnectMs.HasValue) v["probe.tcp_connect_time"] = current.TcpConnectMs.Value;

        // Backward-compatible ping.* aliases remain available for existing custom schemes.
        v["ping.target"] = target;
        v["ping.address"] = current.Address;
        v["ping.protocol"] = protocol;
        v["ping.port"] = protocol == "ICMP" ? 0 : port;
        v["ping.endpoint"] = endpoint;
        v["ping.online"] = current.Success;
        v["ping.status_text"] = current.StatusText;
        v["ping.reply_status"] = current.ReplyStatus;
        v["ping.latency_ms"] = latencyMs;
        v["ping.latency_text"] = latencyText;
        v["ping.progress"] = latencyProgress;
        v["ping.full_scale_ms"] = PingFullScaleMs;
        v["ping.timeout_ms"] = PingTimeoutMs;
        v["ping.ttl"] = current.Ttl;
        v["ping.sent"] = sent;
        v["ping.received"] = received;
        v["ping.lost"] = lost;
        v["ping.loss_percent"] = Math.Clamp(loss, 0d, 100d);
        v["ping.avg_latency_ms"] = avg;
        v["ping.min_latency_ms"] = min;
        v["ping.max_latency_ms"] = max;
        v["ping.jitter_ms"] = jitter;
        v["ping.last_success"] = lastSuccess?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "";
        v["ping.error"] = current.Success ? "" : current.Error;
        if (OperatingSystem.IsMacOS())
        {
            // A failed probe has no measured latency. Keep its real status, never a fake 999 ms.
            foreach (string prefix in new[] { "probe", "ping" })
            {
                if (!current.Success) v[prefix + ".latency_ms"] = current.StatusText;
                if (successLatencies.Count == 0)
                    foreach (string metric in new[] { "avg_latency_ms", "min_latency_ms", "max_latency_ms", "jitter_ms" })
                        v[prefix + "." + metric] = current.StatusText;
            }
            if (!current.DnsResolveMs.HasValue) v["probe.dns_resolve_time"] = current.StatusText;
            if (!current.TcpConnectMs.HasValue) v["probe.tcp_connect_time"] = protocol == "TCP"
                ? current.StatusText : LocalizationManager.Text("当前协议不适用", "Not applicable to this protocol");
        }
    }

    private static string NormalizeProbeProtocol(string? configuredProtocol)
    {
        string protocol = (configuredProtocol ?? "ICMP").Trim().ToUpperInvariant();
        return protocol is "TCP" or "UDP" ? protocol : "ICMP";
    }

    private static Task<PingProbeResult> ProbeEndpointAsync(string target, string protocol, int port, CancellationToken ct) =>
        protocol switch
        {
            "TCP" => ProbeTcpAsync(target, port, ct),
            "UDP" => ProbeUdpAsync(target, port, ct),
            _ => ProbeIcmpAsync(target, ct),
        };

    private static async Task<PingProbeResult> ProbeIcmpAsync(string target, CancellationToken ct)
    {
        double? dnsMs = null;
        try
        {
            string probeTarget = target;
            if (!IPAddress.TryParse(target, out _))
            {
                var dns = Stopwatch.StartNew();
                var addresses = await Dns.GetHostAddressesAsync(target).WaitAsync(ct).ConfigureAwait(false);
                dns.Stop();
                dnsMs = dns.Elapsed.TotalMilliseconds;
                var address = addresses.FirstOrDefault(x => x.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6);
                if (address is not null) probeTarget = address.ToString();
            }
            else dnsMs = 0d;

            using var ping = new Ping();
            var reply = await ping.SendPingAsync(probeTarget, PingTimeoutMs).WaitAsync(ct).ConfigureAwait(false);
            bool ok = reply.Status == IPStatus.Success;
            return new PingProbeResult(
                ok,
                reply.Status,
                ok ? reply.RoundtripTime : 0,
                reply.Address?.ToString() ?? "",
                reply.Options?.Ttl ?? 0,
                ok ? LocalizationManager.Text("在线", "Online") : PingStatusText(reply.Status),
                reply.Status.ToString(),
                DateTime.Now,
                dnsMs,
                null);
        }
        catch (OperationCanceledException) { throw; }
        catch (PingException ex)
        {
            return new PingProbeResult(false, IPStatus.Unknown, 0, "", 0, LocalizationManager.Text("检测失败", "Failed"), ex.InnerException?.Message ?? ex.Message, DateTime.Now, dnsMs, null);
        }
        catch (Exception ex)
        {
            return new PingProbeResult(false, IPStatus.Unknown, 0, "", 0, LocalizationManager.Text("检测失败", "Failed"), ex.Message, DateTime.Now, dnsMs, null);
        }
    }

    private static async Task<PingProbeResult> ProbeTcpAsync(string target, int port, CancellationToken ct)
    {
        var total = Stopwatch.StartNew();
        double? dnsMs = null;
        try
        {
            IPAddress address;
            if (IPAddress.TryParse(target, out var parsed) && parsed is not null)
            {
                address = parsed;
                dnsMs = 0d;
            }
            else
            {
                var dns = Stopwatch.StartNew();
                var addresses = await Dns.GetHostAddressesAsync(target).WaitAsync(ct).ConfigureAwait(false);
                dns.Stop();
                dnsMs = dns.Elapsed.TotalMilliseconds;
                address = addresses.FirstOrDefault(x => x.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                          ?? throw new SocketException((int)SocketError.HostNotFound);
            }

            using var tcp = new TcpClient(address.AddressFamily);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(PingTimeoutMs);
            var connect = Stopwatch.StartNew();
            await tcp.ConnectAsync(address, port, timeout.Token).ConfigureAwait(false);
            connect.Stop();
            total.Stop();
            string remote = (tcp.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString() ?? address.ToString();
            return new PingProbeResult(true, IPStatus.Success, total.ElapsedMilliseconds, remote, 0, LocalizationManager.Text("在线", "Online"), "Connected", DateTime.Now, dnsMs, connect.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            total.Stop();
            return new PingProbeResult(false, IPStatus.TimedOut, 0, "", 0, LocalizationManager.Text("超时", "Timed out"), "TimedOut", DateTime.Now, dnsMs, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (SocketException ex)
        {
            total.Stop();
            return new PingProbeResult(false, IPStatus.Unknown, 0, "", 0, LocalizationManager.Text("连接失败", "Connection failed"), ex.SocketErrorCode.ToString(), DateTime.Now, dnsMs, null);
        }
        catch (Exception ex)
        {
            total.Stop();
            return new PingProbeResult(false, IPStatus.Unknown, 0, "", 0, LocalizationManager.Text("连接失败", "Connection failed"), ex.Message, DateTime.Now, dnsMs, null);
        }
    }

    private static async Task<PingProbeResult> ProbeUdpAsync(string target, int port, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        double? dnsMs = null;
        try
        {
            IPAddress address;
            if (IPAddress.TryParse(target, out var parsedAddress) && parsedAddress is not null)
            {
                address = parsedAddress;
                dnsMs = 0d;
            }
            else
            {
                var dns = Stopwatch.StartNew();
                var addresses = await Dns.GetHostAddressesAsync(target).WaitAsync(ct).ConfigureAwait(false);
                dns.Stop();
                dnsMs = dns.Elapsed.TotalMilliseconds;
                address = addresses.FirstOrDefault(x => x.AddressFamily is AddressFamily.InterNetwork or AddressFamily.InterNetworkV6)
                          ?? throw new SocketException((int)SocketError.HostNotFound);
            }

            using var udp = new UdpClient(address.AddressFamily);
            udp.Connect(new IPEndPoint(address, port));
            byte[] payload = { 0x00 };
            await udp.SendAsync(payload, payload.Length).ConfigureAwait(false);
            var result = await udp.ReceiveAsync().WaitAsync(TimeSpan.FromMilliseconds(PingTimeoutMs), ct).ConfigureAwait(false);
            sw.Stop();
            return new PingProbeResult(true, IPStatus.Success, sw.ElapsedMilliseconds, result.RemoteEndPoint.Address.ToString(), 0, LocalizationManager.Text("在线", "Online"), "Response", DateTime.Now, dnsMs, null);
        }
        catch (TimeoutException)
        {
            sw.Stop();
            return new PingProbeResult(false, IPStatus.TimedOut, 0, "", 0, LocalizationManager.Text("超时", "Timed out"), "TimedOut", DateTime.Now, dnsMs, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (SocketException ex)
        {
            sw.Stop();
            string status = ex.SocketErrorCode == SocketError.ConnectionReset ? LocalizationManager.Text("端口不可达", "Port unreachable") : LocalizationManager.Text("检测失败", "Failed");
            return new PingProbeResult(false, IPStatus.Unknown, 0, "", 0, status, ex.SocketErrorCode.ToString(), DateTime.Now, dnsMs, null);
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new PingProbeResult(false, IPStatus.Unknown, 0, "", 0, LocalizationManager.Text("检测失败", "Failed"), ex.Message, DateTime.Now, dnsMs, null);
        }
    }

    private static string PingStatusText(IPStatus status) => status switch
    {
        IPStatus.Success => LocalizationManager.Text("在线", "Online"),
        IPStatus.TimedOut => LocalizationManager.Text("超时", "Timed out"),
        IPStatus.DestinationHostUnreachable => LocalizationManager.Text("主机不可达", "Host unreachable"),
        IPStatus.DestinationNetworkUnreachable => LocalizationManager.Text("网络不可达", "Network unreachable"),
        IPStatus.DestinationPortUnreachable => LocalizationManager.Text("端口不可达", "Port unreachable"),
        IPStatus.BadDestination => LocalizationManager.Text("目标无效", "Invalid target"),
        IPStatus.BadRoute => LocalizationManager.Text("路由无效", "Invalid route"),
        IPStatus.TtlExpired => LocalizationManager.Text("TTL 已过期", "TTL expired"),
        _ => LocalizationManager.Text("不可达", "Unreachable")
    };

    private async Task AddDeepSeekBalanceAsync(IDictionary<string, object?> v, CustomHudSettings settings, CancellationToken ct)
    {
        var apiKey = SecretStore.Unprotect(settings.DeepSeekApiKeyProtected);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (OperatingSystem.IsMacOS()) DeepSeekFailure(v, LocalizationManager.Text("请配置 API Key", "Set API key"));
            return;
        }

        if (_deepSeekCacheKey == apiKey && _deepSeekCache is { } cache && DateTime.UtcNow < cache.ExpiresAt)
        {
            CopyDeepSeek(v, cache);
            return;
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.deepseek.com/user/balance");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            var apiTimer = Stopwatch.StartNew();
            using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
            apiTimer.Stop();
            res.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false));
            var root = doc.RootElement;
            bool available = root.TryGetProperty("is_available", out var av) && av.ValueKind == JsonValueKind.True;
            double total = 0, granted = 0, topped = 0;
            bool foundCurrency = false;

            if (root.TryGetProperty("balance_infos", out var infos) && infos.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in infos.EnumerateArray())
                {
                    if (item.TryGetProperty("currency", out var cur)
                        && !string.Equals(cur.GetString(), "CNY", StringComparison.OrdinalIgnoreCase))
                        continue;
                    foundCurrency = true;
                    double Balance(string key)
                    {
                        if (!OperatingSystem.IsMacOS()) return ReadJsonNumber(item, key);
                        if (!item.TryGetProperty(key, out var field) || !double.TryParse(field.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number))
                            throw new JsonException("Invalid balance response.");
                        return number;
                    }
                    total += Balance("total_balance");
                    granted += Balance("granted_balance");
                    topped += Balance("topped_up_balance");
                }
            }

            if (OperatingSystem.IsMacOS() && !foundCurrency) throw new JsonException("Missing CNY balance.");
            _deepSeekCacheKey = apiKey;
            _deepSeekCache = new DeepSeekCache(available, total, granted, topped, apiTimer.Elapsed.TotalMilliseconds, DateTime.UtcNow.AddMinutes(1));
            CopyDeepSeek(v, _deepSeekCache);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            if (OperatingSystem.IsMacOS()) DeepSeekFailure(v, LocalizationManager.Text("API 请求失败", "API request failed"));
        }
    }

    private static void DeepSeekFailure(IDictionary<string, object?> v, string status)
    {
        foreach (var key in new[] { "available", "available_text", "balance", "balance_text", "granted_balance", "topped_up_balance", "api.latency_ms" })
            v["deepseek." + key] = status;
    }

    private static void CopyDeepSeek(IDictionary<string, object?> v, DeepSeekCache c)
    {
        v["deepseek.available"] = c.Available;
        v["deepseek.available_text"] = c.Available ? LocalizationManager.Text("可用", "Available") : LocalizationManager.Text("不可用", "Unavailable");
        v["deepseek.balance"] = c.Total;
        v["deepseek.balance_text"] = $"¥{c.Total:0.00}";
        v["deepseek.granted_balance"] = c.Granted;
        v["deepseek.topped_up_balance"] = c.Topped;
        v["deepseek.api.latency_ms"] = c.LatencyMs;
    }

    public static string GetDeepSeekPeriodNameZh(CustomHudSettings settings)
    {
        var (_, _, peak) = GetDeepSeekPeriodState(settings);
        return peak ? "高峰" : "低谷";
    }

    private static (DateTime Beijing, List<(TimeSpan Start, TimeSpan End)> Windows, bool Peak) GetDeepSeekPeriodState(CustomHudSettings settings)
    {
        DateTime beijing;
        try
        {
            beijing = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai")).DateTime;
        }
        catch
        {
            beijing = DateTime.UtcNow.AddHours(8);
        }

        var windows = ParseWindows(settings.DeepSeekPeakWindows);
        bool weekday = beijing.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;
        bool peak = weekday && windows.Any(w => beijing.TimeOfDay >= w.Start && beijing.TimeOfDay < w.End);
        return (beijing, windows, peak);
    }

    private static void AddDeepSeekPeriod(IDictionary<string, object?> v, CustomHudSettings settings)
    {
        var (beijing, windows, peak) = GetDeepSeekPeriodState(settings);
        var next = FindNextTransition(beijing, windows);
        var remaining = Math.Max(0, (next - beijing).TotalSeconds);
        var segmentStart = FindCurrentSegmentStart(beijing, windows, peak);
        var total = Math.Max(1, (next - segmentStart).TotalSeconds);
        var progress = Math.Clamp((beijing - segmentStart).TotalSeconds / total * 100d, 0d, 100d);

        string periodNameZh = peak ? "高峰" : "低谷";
        string periodNameEn = peak ? "PEAK" : "OFF-PEAK";
        string remainingText = LocalizationManager.Text(
            $"{periodNameZh}时段剩余{FormatDuration(remaining)}",
            $"{(peak ? "Peak" : "Off-peak")} left {FormatDuration(remaining)}");
        string progressText = LocalizationManager.Text(
            $"{periodNameZh}已过{Math.Round(progress, MidpointRounding.AwayFromZero):0}%",
            $"{(peak ? "Peak" : "Off-peak")} {Math.Round(progress, MidpointRounding.AwayFromZero):0}%");

        v["deepseek.period.name"] = periodNameEn;
        v["deepseek.period.name_zh"] = LocalizationManager.IsEnglish ? periodNameEn : periodNameZh;
        v["deepseek.period.is_peak"] = peak;
        v["deepseek.period.is_off_peak"] = !peak;
        v["deepseek.period.remaining_seconds"] = remaining;
        v["deepseek.period.remaining_text"] = remainingText;
        v["deepseek.period.progress"] = progress;
        v["deepseek.period.progress_text"] = progressText;

        // DeepSeek peak/off-peak rules are defined in Beijing Time (UTC+08:00).
        // Keep the existing next_switch_* variables in Beijing Time for compatibility,
        // and expose explicit local-time variants for users outside China.
        var nextBeijing = new DateTimeOffset(
            DateTime.SpecifyKind(next, DateTimeKind.Unspecified),
            TimeSpan.FromHours(8));
        var nextLocal = nextBeijing.ToLocalTime();

        v["deepseek.period.next_switch_time"] = nextBeijing.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        v["deepseek.period.next_switch_datetime"] = nextBeijing.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        v["deepseek.period.next_switch_time_local"] = nextLocal.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        v["deepseek.period.next_switch_datetime_local"] = nextLocal.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        v["deepseek.period.timezone"] = LocalizationManager.Text("北京时间 (UTC+08:00)", "Beijing Time (UTC+08:00)");
        v["deepseek.period.local_timezone"] = $"{TimeZoneInfo.Local.Id} (UTC{FormatUtcOffset(nextLocal.Offset)})";
    }

    private async Task AddCustomHttpAsync(
        IDictionary<string, object?> v,
        CustomHudSettings settings,
        CancellationToken ct,
        HashSet<string>? requested)
    {
        foreach (var source in settings.HttpSources.Where(x => x.Enabled && !string.IsNullOrWhiteSpace(x.Url)))
        {
            var sourcePrefix = $"custom.{Sanitize(source.Name)}.";
            if (requested is not null && !requested.Any(k => k.StartsWith(sourcePrefix, StringComparison.OrdinalIgnoreCase)))
                continue;

            string cacheKey = source.Name + "|" + source.Url;
            if (!_httpCache.TryGetValue(cacheKey, out var cache) || DateTime.UtcNow >= cache.ExpiresAt)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, source.Url);
                    foreach (var h in source.Headers)
                    {
                        var value = ExpandEnvironment(h.Value);
                        req.Headers.TryAddWithoutValidation(h.Key, value);
                    }

                    using var res = await _http.SendAsync(req, ct).ConfigureAwait(false);
                    res.EnsureSuccessStatusCode();
                    cache = new HttpCacheEntry(
                        await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false),
                        DateTime.UtcNow.AddSeconds(Math.Clamp(source.RefreshSeconds, 5, 86400)));
                    _httpCache[cacheKey] = cache;
                }
                catch
                {
                    if (OperatingSystem.IsMacOS())
                        foreach (var f in source.Fields)
                            v[sourcePrefix + Sanitize(f.Variable)] = LocalizationManager.Text("数据源请求失败", "Source request failed");
                    continue;
                }
            }

            try
            {
                using var doc = JsonDocument.Parse(cache.Json);
                foreach (var f in source.Fields)
                {
                    if (TryJsonPath(doc.RootElement, f.JsonPath, out var value))
                        v[$"custom.{Sanitize(source.Name)}.{Sanitize(f.Variable)}"] = JsonToObject(value)
                            ?? (OperatingSystem.IsMacOS() ? LocalizationManager.Text("JSON 值为空", "JSON value is null") : null);
                    else if (OperatingSystem.IsMacOS()) v[sourcePrefix + Sanitize(f.Variable)] = LocalizationManager.Text("JSON 路径不存在", "JSON path missing");
                }
            }
            catch
            {
                if (OperatingSystem.IsMacOS())
                    foreach (var f in source.Fields)
                        v[sourcePrefix + Sanitize(f.Variable)] = LocalizationManager.Text("JSON 数据无效", "Invalid JSON");
            }
        }
    }

    private static bool NeedsPrefix(HashSet<string>? requested, string prefix)
    {
        if (requested is null) return true;
        return requested.Any(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool NeedsAny(HashSet<string>? requested, params string[] keys)
    {
        if (requested is null) return true;
        return keys.Any(requested.Contains);
    }

    private static bool NeedsOnlyPeriodVariables(HashSet<string>? requested)
    {
        if (requested is null) return false;
        var deepSeekKeys = requested.Where(k => k.StartsWith("deepseek.", StringComparison.OrdinalIgnoreCase)).ToList();
        return deepSeekKeys.Count > 0 && deepSeekKeys.All(k => k.StartsWith("deepseek.period.", StringComparison.OrdinalIgnoreCase));
    }

    private static double ToDoubleSafe(object? value)
    {
        try { return Convert.ToDouble(value ?? 0, CultureInfo.InvariantCulture); }
        catch { return 0d; }
    }

    private static int ToIntSafe(object? value)
    {
        try { return Convert.ToInt32(value ?? 0, CultureInfo.InvariantCulture); }
        catch { return 0; }
    }

    private static bool ToBool(object? value)
    {
        try { return Convert.ToBoolean(value ?? false, CultureInfo.InvariantCulture); }
        catch { return false; }
    }

    private static string CpuArchitectureName(object? value)
    {
        int code = ToIntSafe(value);
        return code switch
        {
            0 => "x86",
            5 => "ARM",
            6 => "Itanium",
            9 => "x64",
            12 => "ARM64",
            _ => RuntimeInformation.ProcessArchitecture.ToString()
        };
    }

    private static double MaxGpuType(IReadOnlyDictionary<string, double> totals, string type)
    {
        double max = 0d;
        string suffix = "|" + type;
        foreach (var pair in totals)
        {
            if (pair.Key.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                max = Math.Max(max, pair.Value);
        }
        return Math.Clamp(max, 0d, 100d);
    }

    private static string DayOfWeekZh(DayOfWeek day) => LocalizationManager.IsEnglish
        ? day.ToString()
        : day switch
        {
            DayOfWeek.Monday => "星期一",
            DayOfWeek.Tuesday => "星期二",
            DayOfWeek.Wednesday => "星期三",
            DayOfWeek.Thursday => "星期四",
            DayOfWeek.Friday => "星期五",
            DayOfWeek.Saturday => "星期六",
            DayOfWeek.Sunday => "星期日",
            _ => ""
        };

    private static string FormatDurationLong(double seconds)
    {
        long safe = Math.Max(0L, (long)Math.Floor(seconds));
        var ts = TimeSpan.FromSeconds(safe);
        if (ts.TotalDays >= 1)
            return LocalizationManager.Text($"{(int)ts.TotalDays}天 {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}", $"{(int)ts.TotalDays}d {ts.Hours:00}:{ts.Minutes:00}:{ts.Seconds:00}");
        return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
    }

    private static string ExpandEnvironment(string value) =>
        System.Text.RegularExpressions.Regex.Replace(
            value ?? "",
            @"\$\{env:(?<n>[A-Za-z_][A-Za-z0-9_]*)\}",
            m => Environment.GetEnvironmentVariable(m.Groups["n"].Value) ?? "");

    private static string Sanitize(string s) =>
        new((s ?? "").ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());

    private static bool TryJsonPath(JsonElement root, string path, out JsonElement value)
    {
        value = root;
        if (string.IsNullOrWhiteSpace(path) || path == "$" || path == ".") return true;
        var p = path.Trim().TrimStart('$').TrimStart('.');
        foreach (var part in p.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var token = part;
            int bracket = token.IndexOf('[');
            string prop = bracket >= 0 ? token[..bracket] : token;
            if (!string.IsNullOrEmpty(prop))
            {
                if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(prop, out value))
                    return false;
            }

            while (bracket >= 0)
            {
                int end = token.IndexOf(']', bracket + 1);
                if (end < 0
                    || !int.TryParse(token[(bracket + 1)..end], out var idx)
                    || value.ValueKind != JsonValueKind.Array
                    || idx < 0
                    || idx >= value.GetArrayLength())
                    return false;
                value = value[idx];
                bracket = token.IndexOf('[', end + 1);
            }
        }
        return true;
    }

    private static object? JsonToObject(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number when e.TryGetDouble(out var d) => d,
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => e.GetRawText()
    };

    private static double ReadJsonNumber(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var p)) return 0;
        if (p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out var d)) return d;
        if (p.ValueKind == JsonValueKind.String
            && double.TryParse(p.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out d))
            return d;
        return 0;
    }

    private static string FormatDuration(double seconds)
    {
        var safe = Math.Max(0L, (long)Math.Floor(seconds));
        var ts = TimeSpan.FromSeconds(safe);
        return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
    }

    private static string FormatUtcOffset(TimeSpan offset)
    {
        string sign = offset < TimeSpan.Zero ? "-" : "+";
        offset = offset.Duration();
        return $"{sign}{(int)offset.TotalHours:00}:{offset.Minutes:00}";
    }

    private static List<(TimeSpan Start, TimeSpan End)> ParseWindows(string text)
    {
        var list = new List<(TimeSpan, TimeSpan)>();
        foreach (var part in (text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var x = part.Split('-', 2, StringSplitOptions.TrimEntries);
            if (x.Length == 2
                && TimeSpan.TryParse(x[0], out var a)
                && TimeSpan.TryParse(x[1], out var b)
                && b > a)
                list.Add((a, b));
        }
        return list.OrderBy(x => x.Item1).ToList();
    }

    private static DateTime FindNextTransition(DateTime now, List<(TimeSpan Start, TimeSpan End)> windows)
    {
        for (int d = 0; d < 8; d++)
        {
            var day = now.Date.AddDays(d);
            bool weekday = day.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;
            if (!weekday) continue;
            foreach (var w in windows)
            {
                var a = day + w.Start;
                var b = day + w.End;
                if (a > now) return a;
                if (b > now) return b;
            }
        }
        return now.AddHours(1);
    }

    private static DateTime FindCurrentSegmentStart(DateTime now, List<(TimeSpan Start, TimeSpan End)> windows, bool peak)
    {
        if (peak)
        {
            var w = windows.FirstOrDefault(x => now.TimeOfDay >= x.Start && now.TimeOfDay < x.End);
            return now.Date + w.Start;
        }

        for (int d = 0; d < 8; d++)
        {
            var day = now.Date.AddDays(-d);
            bool weekday = day.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday;
            if (!weekday) continue;
            foreach (var w in windows.OrderByDescending(x => x.End))
            {
                var end = day + w.End;
                if (end <= now) return end;
            }
        }
        return now.AddHours(-1);
    }

    private sealed record DeepSeekCache(bool Available, double Total, double Granted, double Topped, double LatencyMs, DateTime ExpiresAt);
    private sealed class PingTargetState
    {
        public DateTime NextProbeUtc { get; set; } = DateTime.MinValue;
        public Task<PingProbeResult>? InFlight { get; set; }
        public PingProbeResult? LastProbe { get; set; }
        public Queue<PingProbeResult> History { get; } = new();
        public DateTime? LastSuccessLocal { get; set; }
    }

    private sealed record PingProbeResult(
        bool Success,
        IPStatus Status,
        long LatencyMs,
        string Address,
        int Ttl,
        string StatusText,
        string ReplyStatus,
        DateTime LocalTime,
        double? DnsResolveMs = null,
        double? TcpConnectMs = null)
    {
        public string Error => Success ? "" : ReplyStatus;
    }

    private sealed record HttpCacheEntry(string Json, DateTime ExpiresAt);

    public void Dispose() { }
}
