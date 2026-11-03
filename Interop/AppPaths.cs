namespace EndfieldChargePlus.Interop;
internal static class AppPaths
{
    internal static string DataDirectory
    {
        get
        {
            string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if(!Path.IsPathFullyQualified(profile))
                throw new InvalidOperationException("The current user's home directory is unavailable. Configuration cannot be stored beside the application.");
            return Path.Combine(profile,"Library","Application Support","EndfieldChargePlusForMacOS");
        }
    }
}
