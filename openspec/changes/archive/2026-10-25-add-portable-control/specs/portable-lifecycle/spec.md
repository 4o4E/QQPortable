# Spec Delta

## Purpose

让便携安装按当前用户的配置在 Windows 登录后启动，并能从 QQ 左下角“更多”菜单重启本次会话，同时保持现有的进程监管和 NapCat 注入方式。

## ADDED Requirements

### Requirement: 开机自启按当前用户配置注册

启动器 SHALL 以便携根目录 `QQPortable.json` 的 `autoStart` 为准，在每次启动时同步当前用户的 `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` 项。开启时 SHALL 写入指向本目录 `QQPortable.exe` 的启动命令，关闭时 SHALL 删除该启动项。配置缺失时 SHALL 视为关闭并写入默认配置。配置内容不是有效 JSON 时 SHALL 显示错误并停止启动，不得猜测配置或静默使用默认值。

#### Scenario: 开启后登录启动

- **WHEN** 用户把 `autoStart` 设为 `true` 并启动过一次启动器
- **THEN** 当前用户的下一次 Windows 登录会运行本目录的 `QQPortable.exe`
- **AND** 启动项只存在于当前用户，不需要管理员权限

#### Scenario: 关闭后不再启动

- **WHEN** 用户把 `autoStart` 设为 `false` 并启动过一次启动器
- **THEN** 当前用户的 Run 项中不再存在 QQPortable 的启动命令
- **AND** 下一次登录不会自动运行 QQPortable

#### Scenario: 直接编辑配置

- **WHEN** 用户在 `QQPortable.json` 中直接把 `autoStart` 改为相反值并重新启动启动器
- **THEN** 启动器按新值同步 Run 项，不需要使用设置程序

#### Scenario: 便携目录移动

- **WHEN** 用户移动便携目录后在新位置启动启动器
- **THEN** Run 项中的命令更新为新位置的 `QQPortable.exe`

#### Scenario: 配置损坏

- **WHEN** `QQPortable.json` 不是有效 JSON
- **THEN** 启动器显示明确的错误信息并退出，不启动 QQ，也不改写配置

### Requirement: 开机自启使用当前用户范围

开机自启 SHALL 只写入当前用户的注册表，不得写入 `HKEY_LOCAL_MACHINE`，不得创建 Windows 服务，不得要求管理员权限，也不得创建桌面或开始菜单快捷方式。

#### Scenario: 普通用户启用

- **WHEN** 非管理员用户在设置程序中开启开机自启
- **THEN** 注册表写入成功，系统不弹出管理员权限请求

#### Scenario: 关闭自启

- **WHEN** 用户关闭开机自启
- **THEN** 只删除 QQPortable 的 Run 项，不影响其他程序的启动项

### Requirement: QQ 左下角“更多”菜单提供重启入口

随安装器附带的 LiteLoader 插件 SHALL 在 QQ 左下角三横线“更多”菜单中插入“重启 QQ”菜单项，位置在“设置”下方，不得新增第二个托盘图标。点击该项 SHALL 由启动器结束当前 QQ 会话并重新启动，不得直接启动 `QQ.exe` 或绕过 NapCat/LiteLoader 注入。

#### Scenario: 更多菜单出现重启项

- **WHEN** QQ 通过 QQPortable 启动且插件已加载
- **THEN** 打开 QQ 左下角“更多”菜单，可以看到“设置”下方的“重启 QQ”

#### Scenario: 点击重启

- **WHEN** 用户点击“重启 QQ”
- **THEN** 当前 QQ、NapCat 和本次会话的控制台宿主退出
- **AND** 启动器重新启动一个新的 QQ 会话并重新注入 NapCat 与 LiteLoader

#### Scenario: 启动器不在运行

- **WHEN** 插件收到重启请求但找不到正在监管本目录的启动器
- **THEN** 不启动新的 QQ 进程，并向用户显示明确的错误信息

### Requirement: 重启复用现有会话监管

重启 SHALL 只结束启动器当前 Windows Job 中的进程，SHALL 在旧会话完全退出后再启动新会话，SHALL 继续使用 NapCat 注入和现有启动器流程。重启不得按进程名称结束其他目录的 QQ。

#### Scenario: 重启后只有一个会话

- **WHEN** 重启完成
- **THEN** 本目录只有一个 QQ 客户端会话在运行
- **AND** 其他目录启动的 QQ 不受影响

#### Scenario: 托盘隐藏不触发重启

- **WHEN** 用户关闭 QQ 主窗口使其隐藏到托盘
- **THEN** 不触发重启，QQ 和 NapCat 继续运行

#### Scenario: 插件在重启后继续工作

- **WHEN** 重启完成且 QQ 重新登录
- **THEN** QQ 左下角“更多”菜单中仍然存在“重启 QQ”菜单项
- **AND** 开机自启开关仍然反映 `QQPortable.json` 的当前值
