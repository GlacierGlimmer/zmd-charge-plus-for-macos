using EndfieldChargePlus.Customization;
namespace EndfieldChargePlus.Settings;
internal sealed partial class SettingsStatusSampler
{
    private readonly MacVariableProvider _statusPlatform=new();
    private partial IDictionary<string,object?> ReadOtherSystem()
    {
        var values=new Dictionary<string,object?>();
        _statusPlatform.Collect(values,new HashSet<string>{"cpu.usage","memory.usage","memory.total_bytes"},null);
        _otherAdapters=MacVariableProvider.ReadGpus().Select(g=>new GpuStatusReading(g.Id,g.Name,g.Unified?null:g.Budget,null,null,g.Unified)).ToArray();
        return values;
    }
}
