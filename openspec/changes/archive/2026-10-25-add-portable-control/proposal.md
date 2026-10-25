# Proposal

## Why

便携目录长期运行时，QQ 需要在登录 Windows 后自动启动，并且用户希望直接从 QQ 左下角“更多”菜单重启 QQ。当前启动器既没有开机自启，也没有任何重启入口；配置只能靠手工编辑运行时文件。安装器需要把控制入口一并装好，避免用户重新安装插件。

## What Changes

- 在便携根目录新增 `QQPortable.json`，当前只包含 `autoStart`；启动器、设置程序、插件都以该文件为唯一配置来源。
- 启动器在启动时按配置同步当前用户的 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 项：开启时写入指向本目录 `QQPortable.exe` 的命令，关闭时删除该项；不申请管理员权限。
- 新增 `QQPortable.setting.exe`：WinForms 设置程序，切换开机自启并立即同步注册表，支持高 DPI。
- 新增随安装器附带的 LiteLoader 插件 `qqportable_settings`：
  - 在 QQ 设置页提供开机自启开关；
  - 在 QQ 左下角三横线“更多”菜单中插入“重启 QQ”菜单项，位置在“设置”下方，不新增第二个托盘图标；
  - 重启通过启动器执行，仍由现有 Windows Job 监管本次 NapCat/QQ 进程树。
- 启动器新增 `--restart` 信令模式和常驻重启监听：收到插件的重启请求后结束本次会话并重新启动，不绕过 NapCat 注入。
- 安装器嵌入并安装设置程序、插件和默认配置；安装完成后的根目录新增 `QQPortable.setting.exe` 与 `QQPortable.json`。
- 文档同步更新 README、AGENTS、架构说明和 OpenSpec 规格。

**BREAKING**：安装器从“不附带插件”改为附带一个控制插件，便携根目录新增两个文件；这是本次需求明确要求的契约变化，旧文档中的“不捆绑插件”约定作废。

## Capabilities

### New Capabilities

- `portable-lifecycle`: 开机自启注册、配置同步、QQ 左下角“更多”菜单重启入口和启动器会话重启。
- `portable-settings`: `QQPortable.json`、`QQPortable.setting.exe`、插件设置页，以及安装器附带控制组件。

### Modified Capabilities

无。当前 `openspec/specs` 为空，本次建立新的能力路径。

## Impact

- 启动器：`src/QQPortable.Launcher/Program.cs`、`BootstrapSession.cs`；新增配置读取、注册表同步、命名事件信令和重启循环。
- 新项目：`src/QQPortable.Settings/`，输出 `QQPortable.setting.exe`。
- 新插件：`assets/plugins/qqportable_settings/`，包含 `manifest.json`、`src/main.cjs`、`src/preload.cjs`、`src/renderer.mjs`。
- 安装器：`EmbeddedAssets.cs`、`InstallerCore.cs`、`QQPortable.Installer.csproj`、`MainForm.cs` 提示文字。
- 构建：`build.ps1` 增加设置程序设计、插件资源复制和安装器嵌入；`QQPortable.sln` 增加设置项目。
- 测试：`QQPortable.Tests` 增加配置读写和自启命令生成测试；真实“更多”菜单、重启和注册表在 Windows 上冒烟验证。
- 兼容性：Windows 10/11 x64；只写当前用户注册表；不安装服务、不需要管理员权限。“更多”菜单是 QQ 渲染层 Vue 菜单；本次按 9.9.36 的 `ctx.menuItem` 结构验证，QQ 大改菜单组件时需要重新验证注入点。
- 明确不做：定时重启、重启整台电脑、崩溃次数统计、自动更新、快捷方式、修改聊天数据、覆盖升级安装器。
- 保持不变：目标目录必须为空；启动器只清理本次 Job 内的进程；关闭窗口到托盘不算退出；不按进程名结束其他目录的 QQ。
