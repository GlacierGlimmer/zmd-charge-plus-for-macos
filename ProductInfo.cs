namespace EndfieldChargePlus;
internal static class ProductInfo
{
    internal const string BuildDate = "2026.10.23";
    internal const string MacName = "Endfield Charge Plus For MacOS";
    internal static string Name
    {
        get
        {
            try
            {
                var localized=LocalizationManager.PlatformTitle;
                return string.IsNullOrWhiteSpace(localized) ? MacName : localized;
            }
            catch
            {
                return MacName;
            }
        }
    }
    internal const string Repository = "https://github.com/GlacierGlimmer/zmd-charge-plus-for-macos";
    internal static string Brand(string value) => value
        .Replace(MacName, "Endfield Charge Plus", StringComparison.Ordinal)
        .Replace("Endfield Charge Plus", MacName, StringComparison.Ordinal)
        .Replace("ENDFIELD CHARGE PLUS FOR MACOS", "ENDFIELD CHARGE PLUS", StringComparison.Ordinal)
        .Replace("ENDFIELD CHARGE PLUS", "ENDFIELD CHARGE PLUS FOR MACOS", StringComparison.Ordinal);
}
