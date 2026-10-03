using System.Runtime.InteropServices;
using System.Text.Json;
namespace EndfieldChargePlus.Settings;
internal static class MacUpdateRelease
{
    internal static string? SelectTag(JsonElement releases, Architecture architecture)
    {
        string rid = architecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        return releases.EnumerateArray()
            .Where(r => !r.GetProperty("draft").GetBoolean() && !r.GetProperty("prerelease").GetBoolean())
            .Where(r => r.TryGetProperty("assets", out var assets) && assets.EnumerateArray().Any(a =>
                a.TryGetProperty("name", out var name) && name.GetString() is { } file &&
                file.StartsWith("EndfieldChargePlusForMacOS-", StringComparison.OrdinalIgnoreCase) &&
                file.EndsWith("-" + rid + ".dmg", StringComparison.OrdinalIgnoreCase)))
            .Select(r => r.GetProperty("tag_name").GetString())
            .Where(tag => Version.TryParse(tag?.TrimStart('v','V'), out _))
            .OrderByDescending(tag => Version.Parse(tag!.TrimStart('v','V'))).FirstOrDefault();
    }
}
