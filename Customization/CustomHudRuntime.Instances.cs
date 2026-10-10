using Avalonia.Threading;
using EndfieldChargePlus.Settings;
using EndfieldChargePlus.Views;

namespace EndfieldChargePlus.Customization;

public sealed partial class CustomHudRuntime
{
    private readonly Dictionary<string, (HudWindow Hud, CustomHudRuntime Runtime)> _instances = new();
    private bool _isChild;
    private bool _editingPreview;
    private void SyncInstances(AppSettings settings)
    {
        if (_isChild) return;
        var enabled = settings.MultiHudEnabled ? settings.HudInstances.Where(i => i.Enabled).ToList() : new();
        var ids = enabled.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var id in _instances.Keys.Where(id => !ids.Contains(id)).ToArray())
        {
            var entry = _instances[id]; entry.Runtime.Dispose(); entry.Hud.Close(); _instances.Remove(id);
        }
        foreach (var instance in enabled)
        {
            var local = instance.Settings with { MultiHudEnabled=false, HudInstances=new(),
                HudEnabled=settings.HudEnabled && instance.Settings.HudEnabled,
                SamplingIntervalSeconds=settings.SamplingIntervalSeconds, UiLanguage=settings.UiLanguage };
            if (_instances.TryGetValue(instance.Id, out var existing))
                { existing.Runtime.ApplySettings(local); existing.Hud.ApplySettings(local); }
            else
            {
                var hud = new HudWindow(); hud.ApplySettings(local);
                var child = new CustomHudRuntime(hud,_variables) { _isChild=true };
                child.ApplySettings(local); _instances.Add(instance.Id, (hud,child)); child.Start();
            }
        }
    }

    public async Task<bool> HighlightInstanceAsync(string id, string name)
    {
        EndInstanceEditing();
        var target = string.IsNullOrEmpty(id) ? (_hud, this) :
            _instances.TryGetValue(id, out var found) ? (found.Hud, found.Runtime) : default;
        if (target.Item1 is null) return false;
        target.Item2._editingPreview=true;
        target.Item1.ApplySettings(target.Item2._appSettings with { PersistentLayer=PersistentHudLayer.Topmost });
        target.Item2._nextPersistentRefresh=DateTime.MinValue;
        target.Item1.SetEditingHighlight(true, name);
        await target.Item2.TickPersistentAsync();
        return true;
    }

    public void EndInstanceEditing()
    {
        if(_editingPreview) _hud.ApplySettings(_appSettings);
        _editingPreview=false; _hud.SetEditingHighlight(false, "");
        foreach (var instance in _instances.Values) { if(instance.Runtime._editingPreview) instance.Hud.ApplySettings(instance.Runtime._appSettings); instance.Runtime._editingPreview=false; instance.Hud.SetEditingHighlight(false, ""); }
    }

    private void DisposeInstances()
    {
        foreach (var instance in _instances.Values) { instance.Runtime.Dispose(); instance.Hud.Close(); }
        _instances.Clear();
    }
}
