<h1 align="center">
  <img src="logo.png" width="48" height="48" alt="CodeIsland Logo" valign="middle">&nbsp;
  CodeIsland
</h1>

<p align="center">
  <b>面向 AI 编程 Agent 的实时状态小岛，支持 macOS 刘海屏和 Windows 桌面右下角浮窗。</b><br>
  <a href="#快速开始">快速开始</a> ·
  <a href="#windows-版">Windows 版</a> ·
  <a href="#macos-版">macOS 版</a> ·
  <a href="#支持的工具">支持工具</a> ·
  <a href="#从源码构建">构建</a>
</p>

---

<p align="center">
  <img src="docs/images/notch-panel.png" width="700" alt="CodeIsland 面板预览">
</p>

## CodeIsland 是什么

CodeIsland 用一个低干扰的小岛面板显示 AI 编程 Agent 的实时状态：当前会话、工具调用、审批请求、问题、完成通知和最近消息。它的目标是让你不用频繁切回终端，也能知道 Claude、Codex、Gemini、Cursor、Trae、OpenCode 等工具正在做什么。

项目最初是 macOS 刘海屏体验，现在仓库内也包含 Windows 版本。Windows 版使用 WPF、系统托盘和 named pipe bridge，把小岛默认放在右下角，避免占用台式机屏幕顶部。

## 当前状态

- macOS 版：原始主版本，使用 Swift 和 Unix socket。
- Windows 版：可日常试用的阶段性版本，已具备右下角小岛、托盘、设置页、hook 安装、named pipe bridge、completion 卡片、审批/问题面板和多 CLI 覆盖。
- Windows 版还不是 macOS 版的 1:1 完整复刻。Windows Terminal 精确 tab/pane 跳转、Remote/Buddy/Companion/ESP32、全 CLI 端到端验收仍在继续补齐。

## 功能

- 实时展示 Agent 会话状态、工具调用和最近消息。
- 普通工具事件低干扰更新，不强制展开大面板。
- 审批和问题事件可在小岛中处理。
- 完成事件以轻量 completion 卡片预览展示。
- 支持像素风 mascot 和 CLI 图标。
- 支持一键跳回相关终端或工作区。
- 支持系统托盘、设置页、诊断导出和 hook 安装/修复/卸载。
- 支持可选 8-bit 事件音效。
- Windows 版支持按 CLI 开关 hook 安装范围。

## 支持的工具

| 工具 | macOS | Windows | 说明 |
| --- | --- | --- | --- |
| Claude Code | 支持 | 支持 | hook、审批、会话状态 |
| Codex | 支持 | 支持 | hook、会话状态、工具事件 |
| Gemini CLI / Google Antigravity | 支持 | 支持 | hook、会话状态 |
| Cursor | 支持 | 支持 | hook、IDE/窗口跳转基础能力 |
| Trae / Trae CN | 支持 | 支持 | hook、会话状态 |
| Qwen / Qoder | 支持 | 支持 | hook、会话状态 |
| CodeBuddy / Factory / WorkBuddy 等 Claude-like 工具 | 支持 | 支持 | Windows 版按兼容配置路径安装 |
| Copilot | 支持 | 支持 | hook 配置 |
| Kimi | 支持 | 支持 | TOML hook 块 |
| Cline | 支持 | 支持 | VS Code hooks 目录 |
| OpenCode | 支持 | 支持 | Windows 插件会调用 `CodeIsland.Bridge.exe` |
| Pi / OMP | 支持 | 支持 | Windows 扩展会调用 `CodeIsland.Bridge.exe` |

## 快速开始

### Windows 版

已经构建好的 Windows 发布目录位于：

```text
windows\CodeIsland.Desktop\bin\Release\net8.0-windows\win-x64\publish\
```

直接运行：

```powershell
.\windows\CodeIsland.Desktop\bin\Release\net8.0-windows\win-x64\publish\CodeIsland.Desktop.exe
```

启动后，右下角会出现 CodeIsland 小岛，系统托盘也会出现 CodeIsland 图标。第一次使用建议打开小岛或托盘菜单，执行 `Install hooks`，把 bridge hook 安装到已检测到的 CLI 工具中。

Windows 版的事件通道是：

```text
\\.\pipe\codeisland
```

### macOS 版

Homebrew 安装：

```bash
brew tap wxtsky/tap
brew install --cask codeisland
```

也可以从 Releases 下载 DMG，将 `CodeIsland.app` 拖入应用程序目录。

## 从源码构建

### Windows

需要安装 .NET 8 SDK。

构建可直接分发的 Windows 版本：

```powershell
dotnet publish windows\CodeIsland.Bridge\CodeIsland.Bridge.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

dotnet publish windows\CodeIsland.Desktop\CodeIsland.Desktop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false

Copy-Item windows\CodeIsland.Bridge\bin\Release\net8.0-windows\win-x64\publish\CodeIsland.Bridge.exe windows\CodeIsland.Desktop\bin\Release\net8.0-windows\win-x64\publish\CodeIsland.Bridge.exe -Force
```

运行：

```powershell
.\windows\CodeIsland.Desktop\bin\Release\net8.0-windows\win-x64\publish\CodeIsland.Desktop.exe
```

开发模式构建：

```powershell
dotnet build windows\CodeIsland.Desktop\CodeIsland.Desktop.csproj -v:minimal
dotnet build windows\CodeIsland.Bridge\CodeIsland.Bridge.csproj -c Release -v:minimal
```

### macOS

需要 macOS 14+ 和 Swift 5.9+。

```bash
git clone https://github.com/tangdan2204/CodeIsland.git
cd CodeIsland

swift build && ./.build/debug/CodeIsland

./build.sh
open .build/release/CodeIsland.app
```

## Windows 版说明

Windows 版主工程在：

```text
windows\CodeIsland.Desktop
```

Bridge 工程在：

```text
windows\CodeIsland.Bridge
```

主要能力：

- WPF 顶层小岛窗口，默认右下角。
- 系统托盘菜单：显示、安装 hooks、设置、导出诊断、退出。
- named pipe server：`\\.\pipe\codeisland`。
- bridge 日志：`%USERPROFILE%\.codeisland\bridge.log`。
- 设置页：General、Behavior、Appearance、Shortcuts、CLIs、Mascots、Sound、Hooks、About。
- completion 卡片：任务完成后轻量弹出，鼠标移入保留，离开后自动收起。
- 普通事件智能抑制：普通工具事件只更新小岛，不展开大面板。
- 审批/问题面板：支持 Allow Once、Always、Deny、Send 和问题输入框回车发送。

## 工作原理

### macOS

```text
AI 工具
  -> 触发 hook
    -> codeisland-bridge
      -> Unix socket /tmp/codeisland-<uid>.sock
        -> CodeIsland 更新刘海小岛
```

### Windows

```text
AI 工具
  -> 触发 hook
    -> CodeIsland.Bridge.exe
      -> named pipe \\.\pipe\codeisland
        -> CodeIsland.Desktop 更新右下角小岛
```

## 验证

Windows 版当前已通过以下基础验证：

```powershell
dotnet build windows\CodeIsland.Desktop\CodeIsland.Desktop.csproj -v:minimal
dotnet build windows\CodeIsland.Desktop\CodeIsland.Desktop.csproj -c Release -v:minimal
dotnet build windows\CodeIsland.Bridge\CodeIsland.Bridge.csproj -c Release -v:minimal
```

发布版 smoke 已验证：Desktop publish 版启动后，publish 目录中的 `CodeIsland.Bridge.exe` 可以向 Desktop 发送 `Stop` 事件，并在日志中显示 `connected` 和 `non-blocking sent`。

## iPhone 与 Apple Watch Buddy

Code Island Buddy 可将 Mac 会话状态同步到 iPhone 动态岛、锁屏、StandBy 和 Apple Watch。相关源码位于：

```text
ios\CodeIslandCompanion
apple-companion
```

App Store：

```text
https://apps.apple.com/us/app/code-island-buddy/id6773881129
```

## 致谢

本项目受到 `claude-island` 的启发，感谢原项目把 AI Agent 状态带入 macOS 刘海区域的创意。

## 许可证

MIT License，详见 [LICENSE](LICENSE)。
