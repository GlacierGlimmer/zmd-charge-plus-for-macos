namespace EndfieldChargePlus.Customization;
public sealed record GpuAdapterInfo(
    string Id,
    string Name,
    int PhysicalIndex,
    double DedicatedMemoryBytes,
    double SharedMemoryBytes,
    IReadOnlyList<string> PerfLuidTokens,
    IReadOnlyList<string> LegacyIds)
{
    public string PerfLuidToken => PerfLuidTokens.FirstOrDefault() ?? "";

    // Keep this as metadata only. The HUD/selector intentionally reports dedicated VRAM,
    // because shared system-memory limits are not the GPU's physical VRAM capacity.
    public bool UsesUnifiedMemory => MacVariableProvider.ReadGpus().FirstOrDefault(g => g.Id == Id)?.Unified == true;

    public double DisplayMemoryBytes => Math.Max(0, DedicatedMemoryBytes);

    public bool MatchesId(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate)) return false;
        if (string.Equals(Id, candidate, StringComparison.OrdinalIgnoreCase)) return true;
        return LegacyIds.Any(x => string.Equals(x, candidate, StringComparison.OrdinalIgnoreCase));
    }

    public bool MatchesCounterName(string counterName) =>
        PerfLuidTokens.Any(token => !string.IsNullOrWhiteSpace(token)
                                    && counterName.Contains(token, StringComparison.OrdinalIgnoreCase));

    public override string ToString() => Name;
}

public static class GpuAdapterCatalog
{
    public static IReadOnlyList<GpuAdapterInfo> GetAdapters(bool forceRefresh = false) => MacVariableProvider.GetGpuAdapters();
}
