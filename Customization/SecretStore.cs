using System.Security.Cryptography;
using System.Text;
using EndfieldChargePlus.Interop;
namespace EndfieldChargePlus.Customization;
public static class SecretStore
{
    private const string Prefix = "macos-keychain-v1:";
    public static string Protect(string? plain)
    {
        if (string.IsNullOrWhiteSpace(plain)) return "";
        if (!OperatingSystem.IsMacOS()) throw new PlatformNotSupportedException("macOS Keychain is required.");
        string account = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain)));
        int status = MacNative.ecp_keychain_set(account, plain);
        if (status != 0) throw new CryptographicException($"Keychain could not save the key (OSStatus {status}).");
        return Prefix + account;
    }
    public static string Unprotect(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded) || !encoded.StartsWith(Prefix, StringComparison.Ordinal) || !OperatingSystem.IsMacOS()) return "";
        return MacNative.ReadKeychain(encoded[Prefix.Length..]) ?? "";
    }
}
