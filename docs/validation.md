# macOS 验证范围

2026-10-03，首版使用 GitHub 托管 macOS 15.7.9 环境分别执行 ARM64 与 Intel x64 原生构建。两端均通过 209 个断言及 DMG 挂载启动、单实例检查；[首次完整通过的工作流](https://github.com/GlacierGlimmer/zmd-charge-plus-for-macos/actions/runs/37122763257)。后续界面修正及发布标签仍会完整重跑相同检查。

检查内容：

- Mach CPU tick 差分、计数器回绕，网络接口新增/断开/重置处理。
- 原生 CPU、内存、磁盘、网络、Metal 数据及数值范围。
- 本机变量库中全部本地变量的实际取值，内置方案不显示缺失值占位符。
- macOS 剪贴板实际读写、全局鼠标位置、HUD 点击穿透/所有桌面/透明窗口属性。
- 实际钥匙串保存与读取；LaunchAgent XML 路径转义。
- 本地 TCP 服务探测；DeepSeek、公网 IP、HTTP/JSON 使用确定响应测试，不代表生产服务可用性或真实账户余额。
- 中英设置、关于、变量库、HUD 实际渲染截图。
- Mach-O 架构、应用签名结构、DMG 完整性、挂载后启动及第二实例退出；下载后核对 SHA-256。

每次运行的 `macos-osx-arm64` 与 `macos-osx-x64` artifacts 包含 DMG、校验文件、变量 JSON、截图及启动日志。

## 尚不能视为实机认证的部分

测试机器为 macOS 虚拟环境，未覆盖每一代 M 系列、所有 Intel GPU、内置电池的充放电状态、Retina 混合缩放多屏、刘海屏、休眠唤醒，以及 macOS 13/14/26 的完整交互流程。最低部署目标设为 macOS 13，但自动测试的系统版本为 15.7.9。

电池、GPU 及可选采集项依本机真实能力筛选；系统未提供的数据不会以伪造数字补齐。网络服务还取决于用户的配置、权限与服务状态。

默认构建为 ad-hoc 签名，未经过 Apple Developer ID 签名或公证；签名结构验证通过不等于 Gatekeeper 公证通过。首次打开的操作见 README。
