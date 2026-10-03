# Privacy — Endfield Charge Plus For MacOS

ECP reads requested system metrics locally. It does not upload system, clipboard or HUD data to the developer. Clipboard data is read only for a profile that requests clipboard variables.

Automatic/manual update checks contact api.github.com for releases in GlacierGlimmer/zmd-charge-plus-for-macos. Public-IP variables contact api.ipify.org / api6.ipify.org when requested. DeepSeek balance queries send the user-provided API key to api.deepseek.com. HTTP/JSON sources and ICMP/TCP/UDP probes contact the destinations configured by the user. These services receive ordinary connection metadata such as the source IP address.

DeepSeek API keys are stored in the user's macOS login Keychain; the settings file contains a reference. Exported settings do not include the Keychain secret. Custom HTTP headers and URLs are stored in the settings JSON and included in exported files; avoid sharing exports containing credentials. Keys are never passed on a shell command line.

Configuration, backups and logs are stored below ~/Library/Application Support/EndfieldChargePlusForMacOS. Removing the .app does not remove these files or its Keychain entries. Turning off launch at login removes the ECP LaunchAgent from ~/Library/LaunchAgents. The user can remove the application's Keychain entries using Keychain Access.

No root privileges, Accessibility permission, screen recording permission, kernel extension, SIP changes, or external hardware-monitoring service are required for the shipped metrics.
