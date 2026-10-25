# Design

## Context

启动器当前在 `Program.Main` 中同步调用 `BootstrapSession.Run`，没有消息循环，也没有任何常驻 UI。`BootstrapSession` 用 Windows Job 限定本次 NapCat/QQ 进程树，全部 QQ 客户端退出后结束本次 Job 成员并返回。启动器本身在 Job 之外，因此可以在 QQ 退出后继续执行收尾或重启逻辑。

QQ 主界面是渲染层 Vue DOM，左下角三横线按钮由 Vue 组件负责，点击后根据组件实例的 `menuItem` 数组渲染 DOM 菜单。启动器仍然负责实际的会话重启。

安装器是单文件 WinForms，内嵌启动器、NapCat 文件和 7-Zip，只向空目录安装。Windows 10/11 x64，无管理员权限，不创建快捷方式。

## Goals / Non-Goals

**Goals:**

- `QQPortable.json` 作为唯一配置来源，启动器、设置程序、插件共享。
- 启动器按配置同步当前用户 Run 项，不需要管理员权限。
- 安装后自带 `QQPortable.setting.exe` 和 `qqportable_settings` 插件。
- 插件在 QQ 左下角“更多”菜单的“设置”下方注入“重启 QQ”菜单项，由启动器结束并重建本次会话。
- 设置程序和插件设置页都支持高 DPI。
- 安装器保持单文件，新增组件全部内嵌。

**Non-Goals:**

- 定时重启 QQ、重启整台电脑、崩溃检测和自动拉起次数统计。
- Windows 服务、计划任务、管理员权限或全局安装。
- 第二个 QQPortable 托盘图标。
- 安装器覆盖升级已有目录。
- 自动更新、快捷方式、修改聊天数据。

## Decisions

### 配置文件放在便携根目录

`QQPortable.json` 放在 `QQPortable.exe` 同级，内容是 `{ "autoStart": false }`。启动器、设置程序和插件都从根目录读取。文件缺失时视为 `autoStart=false` 并写入默认文件；文件不是有效 JSON 时抛出错误并提示用户，不使用默认值继续启动。

理由：用户可以直接编辑，启动器不依赖 LiteLoader 的数据目录，插件和设置程序也不需要各自的配置副本。替代方案是放在 `LiteLoader\data`，但启动器和设置程序在 LiteLoader 之外运行，路径耦合且不便于用户发现。

### 开机自启使用 HKCU Run

写入 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 的 `QQPortable` 值，内容为带引号的 `QQPortable.exe` 完整路径。开启时同时删除 `HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run` 中同名的禁用标记；关闭时删除两处值。启动器每次启动都按配置同步，设置程序和插件在切换后立即调用同一份同步逻辑。

理由：不需要管理员权限，用户可以在任务管理器启动项中看到并禁用，行为透明。替代方案是计划任务，创建和清理更复杂，还会引入任务名称和权限问题；启动文件夹快捷方式会多一个 `.lnk` 文件。

限制：Run 值名固定为 `QQPortable`，同一用户启用多个便携目录时最后启用的目录获胜。这是单实例使用场景下的简单取舍，不引入按路径哈希生成的多个启动项。

### 启动器新增会话重启循环

启动器保留现有流程，但把一次启动改为可重复的会话循环：

1. 读取配置并同步 Run 项。
2. 取得当前便携目录的单实例互斥量；已有实例时激活 QQ 并退出。
3. 准备一次运行时文件。
4. 进入循环：创建会话，等待会话结束或重启请求；如果收到重启请求，结束当前 Job、等待会话完全退出，再创建新会话。
5. 没有重启请求时按原行为退出。

`BootstrapSession` 增加 `RequestStop` 和 `StopRequested`：`RequestStop` 记录请求并调用 `TerminateJobObject`，`Run` 返回后由循环判断是否需要重建会话。请求可重复调用，重复请求不产生第二个会话。重启监听线程是后台线程，进程退出时自动结束。

理由：只有启动器持有 Job，能保证重启不遗漏 NapCat、控制台宿主和早期子进程。替代方案是在插件里直接结束 QQ 并启动 `QQPortable.exe`，但插件自身运行在 QQ 进程内，结束 QQ 会同时结束发出请求的进程，且无法保证旧 Job 已经清空。

### 重启信令使用命名事件

启动器创建名为 `Local\QQPortable.Restart.<hash>` 的自动重置事件，`hash` 是便携根目录完整路径转大写后的 SHA-256 前 16 位十六进制。插件点击“重启 QQ”时，以分离进程方式启动 `QQPortable.exe --restart`；该模式只打开事件并置位，然后退出。事件不存在时显示错误信息，不直接启动 QQ。

理由：信令只需要一个布尔动作，命名事件比命名管道或本地 HTTP 服务简单，且不需要额外端口或防火墙规则。替代方案是插件直接调用 `app.relaunch()`，但这样会绕过启动器的 NapCat 注入和环境变量准备。

### 插件在“更多”菜单注入重启项

QQ 的左下角三横线按钮由渲染层 Vue 组件负责，点击后该组件根据 `ctx.menuItem` 数组渲染 DOM 菜单。插件 renderer 在捕获阶段监听该按钮的 click，在 QQ 的点击处理之前找到按钮的 Vue 组件实例，向 `menuItem` 数组的“设置”项之后插入 `{ label: "重启 QQ", icon: "refresh", handler: ... }`。QQ 自己的菜单组件负责渲染菜单项、图标、高度和位置，因此该项出现在“设置”下方。

点击该项通过 preload 调用 main 进程的 `restartQQ`，main 分离启动 `QQPortable.exe --restart`。启动器完成实际重启，插件不直接结束 QQ 进程，避免与 Job 清理和注入流程冲突。

理由：菜单数据和渲染都在渲染层，插件可以在 QQ 打开菜单之前修改数组，不需要新增托盘图标，也不依赖主进程原生菜单。替代方案是克隆菜单 DOM，但需要在菜单打开后调整高度和位置；直接注入数据让 QQ 自己完成布局。

### 设置程序与共享代码

新增 `src/QQPortable.Settings/`，输出 `QQPortable.setting.exe`，WinForms，net48。界面只有一个“开机自动启动 QQ”复选框、配置文件路径和状态提示。切换后立即保存并同步 Run 项。

启动器和设置程序共享两个源码文件：配置文件读写和 Run 项同步。通过 `<Compile Include="..\QQPortable.Shared\*.cs" Link="Shared\...">` 编入两个项目，不生成额外的 DLL，保持启动器单文件。替代方案是共享类库，但会在根目录多一个 DLL；复制两份代码则容易让注册表值名和 JSON 字段漂移。

设置程序使用与安装器相同的 per-monitor V2 清单、`GetDpiForWindow` 和手动尺寸缩放方式；字体保持点数，只缩放控件尺寸。

`QQPortable.setting.exe --apply` 是不显示窗口的模式：读取配置、同步 Run 项、退出。插件用它完成注册表写入，避免在 JavaScript 中再实现一份 Run 项逻辑。失败时通过进程退出码和标准错误报告。

### 插件与安装器打包

插件源码放在 `assets/plugins/qqportable_settings/`，包含 `manifest.json`、`src/main.cjs`、`src/preload.cjs`、`src/renderer.mjs`。插件使用 manifest version 4，platform 为 `win32`。main 入口负责配置读写、重启 IPC 和调用设置程序；preload 通过 `contextBridge` 暴露读取、切换和重启接口；renderer 负责设置页和“更多”菜单项注入。

`build.ps1` 的顺序变为：

1. 构建启动器，复制启动器、NapCat 文件和 7-Zip。
2. 构建设置程序，复制 `QQPortable.setting.exe`。
3. 复制插件源码到安装器资源目录。
4. 构建安装器，把启动器、设置程序、插件文件和 7-Zip 作为嵌入资源打进单个 EXE。

安装器在 LiteLoader 补丁完成后提取设置程序、写入默认 `QQPortable.json`、创建 `LiteLoader\plugins\qqportable_settings` 并写入插件文件。安装器仍然只接受空目录，不处理已有插件。

### 高 DPI

设置程序沿用安装器已经验证过的方式：嵌入 per-monitor V2 清单，窗口创建后读取实际 DPI，按比例缩放窗口和控件尺寸，不放大字体。复选框、说明文字和状态文字使用 `Dock` 或百分比布局，不设固定像素宽度。验证时检查 100%、150% 和 200%。

## Risks / Trade-offs

- **QQ 主界面菜单结构变化** → 本次按 9.9.36 的 `.func-menu-more-component` 与 `ctx.menuItem` 验证。QQ 大改菜单组件时需更新注入点；启动器、自启和重启信令不受影响。
- **重启是强制结束进程** → 用户点击重启时 QQ 正在运行，`TerminateJobObject` 不会走 QQ 的正常退出流程，未保存的输入和本地数据库写入可能丢失或需要下次启动恢复。只提供手动重启，不做自动或定时重启；文档明确说明。
- **重启时 QQ 正好在启动或退出** → `RequestStop` 可重复调用，会话循环等待 `BootstrapSession.Run` 返回后再创建新会话；启动中的会话在 Job 创建后同样会被结束。
- **Run 值名冲突** → 同一用户只有一个 `QQPortable` 启动项，多个便携目录最后启用的获胜。文档说明单实例限制。
- **配置写入并发** → 设置程序和插件都采用临时文件加替换的原子写入；启动器只在启动和设置切换时读取，损坏的 JSON 直接报错。
- **任务管理器禁用启动项** → 用户在任务管理器禁用后，配置仍是开启；下次手动启动启动器会重新写入并清除禁用标记。这是配置为唯一来源的直接结果。
- **设置程序缺失** → 插件调用 `--apply` 失败时回滚配置并提示；安装器保证全新安装包含该文件。

## Migration Plan

- 全新安装：使用新版安装器，自动获得设置程序、默认配置和控制插件。
- 已有安装（例如 `D:\App\QQPortable`）：退出 QQ 后手工覆盖 `QQPortable.exe`、`QQPortable.setting.exe`、`QQPortable.json` 和 `LiteLoader\plugins\qqportable_settings`，保留现有插件、LiteLoader 和聊天数据。
- 回滚：删除 `QQPortable` Run 项，删除控制插件目录，恢复旧启动器；`QQPortable.json` 可以保留，旧启动器不会读取。

## Open Questions

无。QQ 主界面“更多”菜单的组件结构属于运行验证项；若 QQ 后续调整结构，更新 renderer 注入点即可，不改变规格或任务拆分。
