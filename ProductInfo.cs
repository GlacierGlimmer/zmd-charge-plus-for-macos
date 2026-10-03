namespace EndfieldChargePlus;
internal static class ProductInfo
{
    internal const string MacName = "Endfield Charge Plus For MacOS";
    internal const string Name = MacName;
    internal const string Repository = "https://github.com/GlacierGlimmer/zmd-charge-plus-for-macos";
    internal static string Brand(string value) => value
        .Replace(MacName, "Endfield Charge Plus", StringComparison.Ordinal)
        .Replace("Endfield Charge Plus", MacName, StringComparison.Ordinal)
        .Replace("ENDFIELD CHARGE PLUS FOR MACOS", "ENDFIELD CHARGE PLUS", StringComparison.Ordinal)
        .Replace("ENDFIELD CHARGE PLUS", "ENDFIELD CHARGE PLUS FOR MACOS", StringComparison.Ordinal);
}
