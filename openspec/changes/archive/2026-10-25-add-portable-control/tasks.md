# Tasks

## 1. 配置与开机自启

- [x] 1.1 新增 `src/QQPortable.Shared/PortableConfig.cs` 和 `AutoStartRegistration.cs`，提供 JSON 读取/原子写入、未知字段保留、Run 命令生成和 Run 项同步；用 `<Compile Include>` 链接进启动器。验证：`dotnet test` 覆盖默认值、往返写入、未知字段保留和带空格路径的引号。
- [x] 1.2 启动器启动时读取根目录 `QQPortable.json`，缺失时写入默认值，按 `autoStart` 同步 `HKCU\...\Run\QQPortable` 并清除 `StartupApproved\Run` 的禁用标记；配置不是有效 JSON 时弹出错误并退出。验证：构造临时根目录，分别用缺失、有效 `true`、有效 `false`、损坏 JSON 启动，检查配置文件和 `reg query` 结果。
- [x] 1.3 在 `docs/architecture.md` 和 `AGENTS.md` 记录配置文件位置、Run 项名称、单用户单启动项限制和缺失/损坏配置的行为。验证：文档中的路径和值名与实现一致。

## 2. 会话重启

- [x] 2.1 重构 `BootstrapSession`：增加会话实例、可重复调用的 `RequestStop` 和 `StopRequested`，Job 仍只包含本次 NapCat/QQ 进程树。验证：构建后启动真实 QQ，调用停止接口，确认 Job 内进程全部退出且其他目录 QQ 不受影响。
- [x] 2.2 启动器增加命名事件 `Local\QQPortable.Restart.<hash>`、后台监听线程和会话循环；收到重启请求后等待旧会话结束再创建新会话，重新执行 NapCat 注入。验证：在真实会话中触发重启，确认旧进程树清空、新会话只有一个 QQ、插件重新加载。
- [x] 2.3 启动器增加 `--restart` 模式，只打开事件并置位；事件不存在时显示错误并返回非零退出码，不启动 QQ。验证：有启动器运行时 `QQPortable.exe --restart` 返回 0 并触发重启；无启动器运行时返回非零且没有新 QQ 进程。
- [x] 2.4 启动器增加按便携根目录生成的单实例互斥量，避免开机自启和手动启动同时创建两个会话；已有实例时激活现有 QQ 并退出。验证：连续启动两次，只有一个启动器持有会话，第二次返回且不产生第二个 NapCat。
- [x] 2.5 在 `AGENTS.md` 和 `docs/architecture.md` 记录重启的进程所有权、强制结束语义和命名事件。验证：文档描述的清理范围与 Job 实现一致。

## 3. 独立设置程序

- [x] 3.1 新增 `src/QQPortable.Settings/` WinForms 项目，输出 `QQPortable.setting.exe`，加入 `QQPortable.sln`；界面包含开机自启复选框、配置文件路径和状态提示，切换后立即保存并同步 Run 项。验证：`dotnet build` 成功，运行后界面显示正确路径。
- [x] 3.2 为设置程序增加 per-monitor V2 清单和 `GetDpiForWindow` 手动缩放，复选框、说明和错误文字使用自适应布局。验证：在 100%、150%、200% 缩放下检查文字和控件边界（本机 192 DPI 至少覆盖 200%）。
- [x] 3.3 增加 `QQPortable.setting.exe --apply` 无窗口模式：读取配置、同步 Run 项、通过退出码和标准错误报告结果；配置文件损坏时不覆盖。验证：命令行切换 `true`/`false` 后 `reg query` 同步；损坏 JSON 时退出码非零且文件内容不变。
- [x] 3.4 更新 `README.md` 的启动、配置和目录说明，注明设置程序不需要管理员权限。验证：README 中的文件名和操作路径与实际产物一致。

## 4. QQ“更多”菜单重启项与插件设置页

- [x] 4.1 新增 `assets/plugins/qqportable_settings/`：manifest v4、main、preload、renderer。renderer 在捕获阶段拦截三横线“更多”按钮的点击，向按钮 Vue 组件的 `menuItem` 数组中“设置”之后插入“重启 QQ”项；点击该项通过 preload 调用 main 的 `restartQQ`，main 分离启动 `QQPortable.exe --restart`。验证：打开“更多”菜单，看到“设置”下方的“重启 QQ”，点击后启动器完成重启。
- [x] 4.2 插件 main 读取和原子写入便携根目录 `QQPortable.json`，保留未知字段；切换开机自启后调用 `QQPortable.setting.exe --apply`，失败时回滚并返回错误。验证：插件切换后 `reg query` 与 JSON 一致；临时移走设置程序时界面显示错误且 JSON 回到原值，随后恢复文件。
- [x] 4.3 preload 使用 `contextBridge` 暴露读取、切换和重启接口，renderer 通过 `onSettingWindowCreated` 插入 LiteLoader 设置页开关，并注入“更多”菜单项；使用 QQ 主题变量并显示处理中/成功/失败状态。验证：在 QQ 设置页打开插件页，切换开关后重新打开状态保持；“更多”菜单项存在且可点击。
- [x] 4.4 更新 `README.md` 插件章节，说明安装器自带 `qqportable_settings`、不要删除、重启入口在 QQ 左下角“更多”菜单的“设置”下方；更新 `AGENTS.md` 和 `docs/architecture.md` 的启动器/插件契约。验证：文档不再声称安装器不附带插件。

## 5. 安装器与构建

- [x] 5.1 扩展 `build.ps1`：构建设置程序，复制 `QQPortable.setting.exe` 和插件文件到安装器资源目录；安装器项目把设置程序、`manifest.json`、`main.cjs`、`preload.cjs`、`renderer.mjs` 作为嵌入资源。验证：`powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1` 成功，安装器仍是单个 EXE，资源名与提取代码一致。
- [x] 5.2 安装器在 LiteLoader 补丁后提取设置程序、写入默认 `QQPortable.json`、创建 `LiteLoader\plugins\qqportable_settings` 并写入插件文件；不修改其他插件。验证：用临时空目录实际安装一次，检查根目录和插件目录结构，以及默认配置为 `autoStart: false`。
- [x] 5.3 更新安装器界面提示文字，说明会安装 QQPortable 控制插件，其他插件仍需自行放入。验证：启动安装器，确认提示文字完整且没有被裁切。
- [x] 5.4 增加安装器提取测试：对临时目录调用 `EmbeddedAssets` 的插件/设置程序提取，断言全部文件存在且内容非空。验证：`dotnet test` 通过。

## 6. 集成验证与交付

- [x] 6.1 执行 `powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1` 和 `dotnet test QQPortable.sln -c Release --no-restore`，确认零错误、测试全部通过。
- [x] 6.2 更新现有安装 `D:\App\QQPortable`：先退出 QQ，覆盖 `QQPortable.exe`、`QQPortable.setting.exe`、`QQPortable.json` 和 `LiteLoader\plugins\qqportable_settings`，保留现有插件、LiteLoader 和聊天数据。验证：目录中新增文件齐全，原有插件目录未被改动。
- [x] 6.3 从 `D:\App\QQPortable\QQPortable.exe` 启动真实 QQ 9.9.36，确认 NapCat 和 LiteLoader 正常加载，QQ 左下角“更多”菜单出现“重启 QQ”菜单项；点击后旧 QQ/NapCat 进程树清空并启动新会话。验证：记录重启前后的 QQ、NapCat 进程数量，确认没有残留和重复会话。
- [x] 6.4 分别用插件设置页和 `QQPortable.setting.exe` 开启、关闭开机自启，检查 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 和 `StartupApproved\Run`；确认不需要管理员权限。验证：`reg query` 输出与配置一致，关闭后两处值均不存在。
- [x] 6.5 检查设置程序在 192 DPI 下的窗口尺寸、复选框、说明和状态文字；检查安装器提示文字。验证：控件文字未被裁切。
- [x] 6.6 清理 `.workspace/tmp` 下的 asar 提取和验证脚本，更新 `.workspace/WORKLOG.md`，确认没有临时文件或诊断代码残留。验证：`git status` 只包含本次功能相关文件。

## Workflow follow-up

- 按项目 review 要求审查改动范围、未确认的自行决定和复杂度，然后归档变更。
- 归档时把 `portable-lifecycle` 和 `portable-settings` 同步到 `openspec/specs`，并确认文档与归档规格一致。
