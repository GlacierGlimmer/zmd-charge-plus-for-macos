# ECP For macOS v0.2.0

**v0.1.3 → v0.2.0 · 构建日期 2026.11.07**

## 主要更新

- 重新设计的深色设置界面：左侧导航、荧光黄强调色和黄/绿/粉三色细节，区分当前系统与软件整体占用。
- 方案管理、自定义模板、变量库、HTTP / JSON 数据源与按章节定位的使用手册。
- 20 种随软件打包的离线语言，默认跟随系统语言；可手动选择并设置全局字体及 UI 缩放。
- 统一的异常与数据提示，保留具体原因、复制详情、日志入口及逐变量“不再提示”。
- 在 HUD 上长按鼠标中键，指向环形栏中的方案，松开切换；长按时间、拖动距离和圆盘大小可配置。
- 自动轮播与手动切换共用队列：点击向下切换；滚轮向上/向下切换上一/下一方案，配合对应方向动画。
- 轮播可选简洁、完整、向上翻滚、向下翻滚四种动画；快捷键可控制显示/隐藏、前后方案、暂停/继续、预览和设置。
- 多实例 HUD 保留各自的方案、队列、位置、字体、显示器与层级；共享的全局设置统一管理。

保留 macOS 原生采集器、菜单栏、窗口层级和多空间行为。全局输入等功能受系统权限与窗口管理规则限制；超级置顶属于实验性能力，不绕过受保护的系统界面。

## 主要修复

- 修复自定义标题栏窗口无法调整大小的问题，保留 Cocoa 原生窗口移动与边缘缩放。
- 改进登录启动保存与安装位置校验，保存失败时展示具体原因、异常详情和可操作入口。
- 统一语言、更新与数据提示的窗口风格，改善提示不抢焦点、数据逐变量忽略和异步资源释放。

## 安装与升级

从 [v0.2.0 Releases](https://github.com/GlacierGlimmer/zmd-charge-plus-for-macos/releases/tag/v0.2.0) 下载 Apple Silicon（ARM64）或 Intel（x64）DMG，将 `Endfield Charge Plus For MacOS.app` 拖入 **Applications** 后启动。包内包含 .NET 运行时。

应用在菜单栏驻留。关闭设置窗口后 HUD 继续运行，可从菜单栏退出；要启用登录启动，请先把应用放在 Applications 中。

本次 DMG 使用 ad-hoc 签名，尚未进行 Apple 公证。原生构建、采集、界面、DMG 挂载与包内启动已在 macOS CI 检查，具体机型、屏幕和权限仍可能影响功能。

**支持语言（20）：** 简体中文、English、繁體中文、日本語、한국어、Deutsch、Français、Español、Português、Русский、العربية、हिंदी、বাংলা、اردو、Bahasa Indonesia、Tiếng Việt、ไทย、Türkçe、Italiano、فارسی。

## English — changes since v0.1.3

- Redesigned dark settings with a vertical navigation rail, a fluorescent-yellow accent and three-colour details. System usage and the application’s overall usage are shown separately.
- Profile management, template variables, HTTP / JSON sources and a chapter-based offline manual.
- Twenty bundled offline languages, system-language selection and adjustable global font/UI scale.
- Consistent error and data prompts with concrete reasons, copyable details, log access and per-variable dismissal.
- Hold the middle mouse button on the HUD, point to a profile and release to switch. Hold delay, drag distance and ring size are configurable.
- Manual and automatic cycling share a queue. Clicking moves forward; scrolling up/down selects the previous/next profile with the corresponding transition direction.
- Four cycle animations: Simple, Full, Roll Up and Roll Down. Bind show/hide, previous/next, pause/resume, preview and Settings actions.
- Multiple desktop HUDs retain independent profiles, queues, placement, fonts, displays and layers while sharing global settings.
- Restore native resizing and movement with the custom window title bar.
- Improve login-startup saving and installation-location checks, with actionable failure details.
- Unify language, update and data prompts; improve passive notices, per-variable dismissal and asynchronous resource cleanup.

Download the Apple Silicon (ARM64) or Intel (x64) DMG from [v0.2.0 Releases](https://github.com/GlacierGlimmer/zmd-charge-plus-for-macos/releases/tag/v0.2.0). Drag `Endfield Charge Plus For MacOS.app` into **Applications** before launching or enabling login startup. The .NET runtime is included.

The app remains in the menu bar when Settings closes; use its menu to exit. These DMGs are ad-hoc signed and have not been notarized by Apple. Native collectors, UI, mounting and packaged-app launch were checked on macOS CI; physical hardware, displays and permissions can affect availability.

**Languages:** Simplified Chinese, English, Traditional Chinese, Japanese, Korean, German, French, Spanish, Portuguese, Russian, Arabic, Hindi, Bengali, Urdu, Indonesian, Vietnamese, Thai, Turkish, Italian, Persian.

Hardware metrics depend on the platform, drivers and permissions. Check the SHA-256 file before installing. [Website](https://zmd-bar.x-neko.com/) · [Issues](https://ecp-bugs.x-neko.com/).
