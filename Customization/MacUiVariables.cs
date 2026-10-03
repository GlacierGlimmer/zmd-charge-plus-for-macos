using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using System.Text.RegularExpressions;
namespace EndfieldChargePlus.Customization;
internal static class MacUiVariables
{
    private static long? _clipboardSequence;
    private static DateTime _clipboardChanged;
    internal static async Task CollectAsync(IDictionary<string, object?> values, bool display, bool clipboard)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime) return;
        var collected = await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var v = new Dictionary<string, object?>();
            if (display)
            {
                using var native = Interop.MacNative.Display();
                foreach (var item in native.RootElement.EnumerateObject()) v[item.Name] = item.Value.GetDouble();
            }
            if (clipboard)
            {
                using var native = Interop.MacNative.Clipboard();
                var root=native.RootElement;
                if (!root.TryGetProperty("text",out var value)) return v;
                string text=value.GetString() ?? "";
                string preview=Regex.Replace(text,@"\s+"," ");
                long sequence=root.GetProperty("sequence").GetInt64();
                if (_clipboardSequence != sequence) { _clipboardSequence=sequence; _clipboardChanged=DateTime.Now; }
                v["clipboard.has_text"]=text.Length>0;
                v["clipboard.text_length"]=text.Length;
                v["clipboard.preview"]=preview[..Math.Min(80,preview.Length)];
                v["clipboard.has_image"]=root.GetProperty("hasImage").GetBoolean();
                v["clipboard.last_updated"]=_clipboardChanged.ToString("yyyy-MM-dd HH:mm:ss");
            }
            return v;
        });
        foreach(var pair in collected) values[pair.Key]=pair.Value;
    }
}
