Endfield Charge Plus For MacOS v0.1.3（源码待发布）。

新增：同步 HTTP／JSON、轮播、颜色规则、热区与 2030 年 DeepSeek 日历修复；版本仅由用户要求调整。


修复首次使用时“保存并应用”失败：当登录启动关闭且 `~/Library/LaunchAgents` 尚不存在时，按未注册登录启动处理，不再因删除不存在的 plist 而中断保存。无需手动创建目录。已存在的 ECP 启动项仍可正常移除；权限错误等真实异常仍会提示。

新增隔离用户目录下实际点击“保存并应用”、配置落盘、HUD 应用、重复保存和备份的回归检查，ARM64 与 Intel x64 均执行。

- **主版本：osx-arm64.dmg**，适用于 M1 / M2 / M3 / M4 / M5 等 Apple Silicon Mac。
- **Intel 版本：osx-x64.dmg**。两种安装包均自包含 .NET，无需另装运行时。
- macOS 13+；打开 DMG 后把应用拖入 Applications，再启动。
- 菜单栏驻留、点击穿透 HUD、中英双语、方案管理、时间、网络探测、HTTP/JSON、DeepSeek。
- 原生 Mach / sysctl / IOPowerSources / APFS / Metal 数据。变量按实际检测能力筛选。
- 移除 Windows 专属变量及无法可靠采集的 GPU 使用率、温度、风扇、实时 CPU 频率等项目。
- API Key 存入 macOS 登录钥匙串；配置目录为 `~/Library/Application Support/EndfieldChargePlusForMacOS`。
- 更新检查只使用本仓库中与当前架构匹配的正式 DMG。

默认包为 **ad-hoc 签名，未经过 Apple Developer ID 签名及公证**。首次启动可能需要在“系统设置 → 隐私与安全性”中为此应用选择“仍要打开”。请核对同名 `.sha256` 文件；无需关闭 Gatekeeper 或 SIP。

CI 会在 Apple Silicon 和 Intel macOS 运行环境分别执行原生采集、逐变量读取、方案渲染、界面、DMG 挂载、启动和单实例检查。CI 覆盖范围不代表每一代 Mac 或每个 macOS 版本都经过实机测试。
