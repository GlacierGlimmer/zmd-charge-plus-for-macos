# Endfield Charge Plus For MacOS

[简体中文](README.md) · **English** | macOS 13+ | Apple Silicon / Intel

An Endfield-inspired menu bar and floating desktop HUD with customizable profiles, animations, system metrics, clocks, network probes, DeepSeek and HTTP/JSON sources.

Download from [Releases](https://github.com/GlacierGlimmer/zmd-charge-plus-for-macos/releases). **osx-arm64.dmg is the primary build** for M1 / M2 / M3 / M4 / M5 and other Apple Silicon Macs. Choose osx-x64.dmg for Intel Macs. Drag the app into Applications and launch it there. No separate .NET installation is required.

Default builds are **ad-hoc signed, not Apple Developer ID signed or notarized**. Verify the official source and the accompanying SHA-256, then use this app's Open Anyway button under System Settings > Privacy & Security if Gatekeeper blocks it. Do not disable Gatekeeper or SIP.

Metrics use public macOS APIs: Mach CPU ticks, sysctl, Mach VM, IOPowerSources, APFS Data-volume statfs, 64-bit en* network counters and Metal. The variable library includes only implemented, detected capabilities. Battery profiles disappear on Macs without an internal battery. Metal working-set budget is explicitly distinct from VRAM capacity. CPU temperature/frequency, fan speeds, global GPU utilization, VRAM usage and Windows-only WMI/registry/security metrics are removed.

Memory used is active + wired + compressed, not Activity Monitor memory pressure. Network totals cover physical en* interfaces and exclude VPN/loopback duplication. Online integrations report real errors instead of synthetic measurements. Clipboard update time is when ECP observed a content change.

API keys are stored in the login Keychain. Settings contain only references, so re-enter the key after transferring settings to another Mac. Configuration and logs live in `~/Library/Application Support/EndfieldChargePlusForMacOS`. Launch at login uses a per-user LaunchAgent. Update checks use only this repository's stable release with a matching DMG architecture.

Build on macOS with .NET 8 SDK and Xcode Command Line Tools:

```bash
dotnet run --project Tests/EndfieldChargePlus.PlatformTests.csproj -c Release
bash scripts/package-macos.sh osx-arm64
bash scripts/verify-dmg.sh osx-arm64
```

Use osx-x64 for Intel. Optional `MACOS_SIGNING_IDENTITY` and `MACOS_NOTARY_PROFILE` enable Developer ID signing and notarization. CI runs on native ARM64 and Intel macOS 15 runners, checking collectors, all advertised local variables, profiles, GUI, mounted DMG startup and single-instance behavior. CI is not a claim of testing every Mac model or macOS version.

Based on [QinAnze/zmd-charge](https://github.com/QinAnze/zmd-charge), [ECP](https://github.com/GlacierGlimmer/zmd-charge-plus) and the cross-platform UI foundation in [ECP For Linux](https://github.com/GlacierGlimmer/zmd-charge-plus-for-linux). See [LICENSE](LICENSE), [NOTICE](NOTICE.md) and [Privacy](PRIVACY.md). This is an unofficial community project, not affiliated with or endorsed by the developers or publishers of Arknights: Endfield.
