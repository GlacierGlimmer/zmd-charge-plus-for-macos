using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace EndfieldChargePlus.Customization;

internal static class MacUiVariables
{
    private static string? _clipboardFingerprint;
    private static DateTime _clipboardChanged;

    internal static async Task CollectAsync(IDictionary<string, object?> values, bool display, bool clipboard)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime) return;
        var collected = await Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var v = new Dictionary<string, object?>();
            var desktop = (IClassicDesktopStyleApplicationLifetime)Application.Current.ApplicationLifetime;
            var window = desktop.Windows.FirstOrDefault(w => w.IsVisible) ?? desktop.MainWindow;
            if (window is null) return v;
            if (display)
            {
                using var native = Interop.MacNative.Display();
                foreach (var item in native.RootElement.EnumerateObject()) v[item.Name] = item.Value.GetDouble();
            }
            if (clipboard && window.Clipboard is { } cb)
            {
                try
                {
                    var text = await cb.GetTextAsync().WaitAsync(TimeSpan.FromSeconds(2)) ?? "";
                    var formats = await cb.GetFormatsAsync().WaitAsync(TimeSpan.FromSeconds(2)) ?? Array.Empty<string>();
                    bool image = formats.Any(f => f.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || f is "public.png" or "public.tiff");
                    v["clipboard.has_text"] = text.Length > 0;
                    v["clipboard.text_length"] = text.Length;
                    v["clipboard.preview"] = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ")[..Math.Min(80, System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Length)];
                    v["clipboard.has_image"] = image;
                    string fingerprint = string.Join(',', formats) + ":" + text;
                    if (_clipboardFingerprint != fingerprint) { _clipboardFingerprint = fingerprint; _clipboardChanged = DateTime.Now; }
                    v["clipboard.last_updated"] = _clipboardChanged.ToString("yyyy-MM-dd HH:mm:ss");
                }
                catch (TimeoutException) { }
            }
            return v;
        });
        foreach (var pair in collected) values[pair.Key] = pair.Value;
    }
}
