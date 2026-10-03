namespace EndfieldChargePlus.Interop;
internal static class AppPaths
{
    internal static string DataDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library", "Application Support", "EndfieldChargePlusForMacOS");
}
