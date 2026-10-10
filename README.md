# Endfield Charge Plus For MacOS

当前源码版本：**v0.1.3**（尚未发布 Releases）。版本只在用户明确要求时修改，见[版本约定](docs/VERSIONING.md)。

**简体中文** · [English](README.en.md) | macOS 13+ | Apple Silicon / Intel

终末地风格菜单栏与桌面悬浮 HUD。保留 ECP 的自定义方案、动画、时间、系统监控、网络探测、DeepSeek API 和 HTTP/JSON 数据源，使用 macOS 原生接口采集数据。

## 其他平台下载

各平台由独立仓库维护和发布。请前往对应的 Releases 页面查看可用版本、安装包和校验文件。

| 平台 | 仓库 | 发布页面 |
| --- | --- | --- |
| Windows | [项目仓库](https://github.com/GlacierGlimmer/zmd-charge-plus) | [Releases](https://github.com/GlacierGlimmer/zmd-charge-plus/releases) |
| Linux | [项目仓库](https://github.com/GlacierGlimmer/zmd-charge-plus-for-linux) | [Releases](https://github.com/GlacierGlimmer/zmd-charge-plus-for-linux/releases) |
| Android | [项目仓库](https://github.com/GlacierGlimmer/zmd-charge-plus-for-android) | [Releases](https://github.com/GlacierGlimmer/zmd-charge-plus-for-android/releases) |

Android 仓库已建立；可下载版本以其 Releases 页面为准。

## 下载

从[本仓库 Releases](https://github.com/GlacierGlimmer/zmd-charge-plus-for-macos/releases)下载：

| 安装包 | 适用设备 |
| --- | --- |
| **EndfieldChargePlusForMacOS-osx-arm64.dmg（以 Releases 实际文件名为准）** | **主版本**：M1 / M2 / M3 / M4 / M5 等 Apple Silicon |
| EndfieldChargePlusForMacOS-osx-x64.dmg（以 Releases 实际文件名为准） | Intel Mac |

打开 DMG，把 **Endfield Charge Plus For MacOS.app** 拖到 **Applications** 后启动。包内自带 .NET 运行时。应用在菜单栏驻留，关闭设置窗口后继续运行，通过菜单栏退出。

默认发布使用 ad-hoc 签名，**未经 Apple Developer ID 签名和公证**。核实官方下载来源及 SHA-256 后，如系统阻止打开，在“系统设置 → 隐私与安全性”中为此应用选择“仍要打开”。无需关闭 Gatekeeper / SIP。校验命令：`shasum -a 256 -c <安装包文件名>.sha256`。

## macOS 适配

- CPU 使用率：Mach CPU tick 差分，包含用户、内核和空闲比例；核心数与型号来自 sysctl。
- 内存：Mach VM 与 vm.swapusage。已用 = active + wired + compressed，可用 = 总内存 − 已用。这是明确的内存统计口径，不等于 Activity Monitor 的内存压力。
- 电池：IOPowerSources。无内置电池的 Mac 不显示电池方案；时间估计只在系统实际返回时列出。不把容量百分比当成 mWh。
- 磁盘：APFS Data 卷 statfs，正确读取可写数据卷，不拿只读系统快照作为用户磁盘。
- 网络：en* 物理接口 64 位收发计数器差分，排除 loopback / VPN 虚拟接口，避免重复计算；接口新增、断开及计数器重置不产生突增。
- GPU：Metal 型号、数量、统一内存标记、低功耗/可移除状态和建议工作集预算。**工作集预算不是显存容量**。
- 显示器、剪贴板、中英界面、全局鼠标顶部唤出、HUD 点击穿透、菜单栏、登录启动和单实例均有 macOS 实现。
- API Key 保存到 macOS 登录钥匙串。配置和日志位于 `~/Library/Application Support/EndfieldChargePlusForMacOS`。导出设置只带钥匙串引用，换 Mac 后需重新输入 Key。
- 登录启动使用当前用户 `~/Library/LaunchAgents/com.glacierglimmer.endfieldchargeplus.macos.plist`。必须先安装到本机，不能从 DMG 临时路径注册。

变量库在启动及“重新检测 macOS 变量”时按本机实际数据筛选，内置方案及导入方案同步清理不可用引用。外部服务失败时显示真实错误状态，不伪造测量值。Windows 专用的 WMI / 注册表 / Defender / BitLocker / Hyper-V / TPM 等变量已移除；无可靠公共接口的实时 CPU 频率、温度、风扇、全局 GPU 使用率和显存占用也不显示。

更新检查仅访问本仓库正式 Releases，且必须包含当前运行架构对应的 DMG；不会跳到 Windows 或 Linux 仓库。

## 构建与检查

在 Mac 上安装 .NET 8 SDK 和 Xcode Command Line Tools 后：

```bash
dotnet run --project Tests/EndfieldChargePlus.PlatformTests.csproj -c Release
bash scripts/package-macos.sh osx-arm64
# Intel Mac 使用 osx-x64
bash scripts/verify-dmg.sh osx-arm64
```

GitHub Actions 分别在 `macos-15`（ARM64）和 `macos-15-intel` 验证原生采集、逐个本地变量、内置方案、GUI、DMG 签名结构、挂载运行和单实例。检查报告和截图保存在工作流 artifacts；参阅[验证范围](docs/validation.md)。它们不等于所有机型、系统版本、刘海屏、混合缩放多显示器及电池状态的完整实机认证。

开发者可设置 `MACOS_SIGNING_IDENTITY` 和已配置的 `MACOS_NOTARY_PROFILE`，让打包脚本使用 Developer ID 签名并公证。没有这两项时，发布说明明确标注 ad-hoc 状态。

## 来源与许可

基于 [QinAnze/zmd-charge](https://github.com/QinAnze/zmd-charge) 及 [Endfield Charge Plus](https://github.com/GlacierGlimmer/zmd-charge-plus)，复用 [Linux 版](https://github.com/GlacierGlimmer/zmd-charge-plus-for-linux)的跨平台界面基础。参阅 [LICENSE](LICENSE)、[NOTICE.md](NOTICE.md) 和 [PRIVACY.md](PRIVACY.md)。

本项目为非官方社区作品，与《明日方舟：终末地》开发方及发行方无隶属、授权或背书关系。
