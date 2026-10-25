# Spec Delta

## Purpose

提供同一份配置文件、独立设置程序和 QQ 内插件设置页，让用户在 QQ 运行或关闭时都能查看和修改开机自启配置。

## ADDED Requirements

### Requirement: 配置文件是唯一配置来源

便携根目录 `QQPortable.json` SHALL 是开机自启配置的唯一来源。启动器、`QQPortable.setting.exe` 和 LiteLoader 插件 SHALL 读取同一个文件；设置程序和插件写回时 SHALL 保留文件中未知的字段。所有组件在打开设置界面时 SHALL 显示文件中当前的值，不得各自维护第二份状态。

#### Scenario: 两个入口显示相同状态

- **WHEN** 用户通过设置程序把 `autoStart` 改为 `true`
- **THEN** 打开 QQ 内的插件设置页看到同一个开关处于开启状态

#### Scenario: 手工编辑后读取

- **WHEN** 用户在文本编辑器中把 `autoStart` 改为 `false` 并保存
- **THEN** 下次打开设置程序或插件设置页显示关闭状态

#### Scenario: 保留未知字段

- **WHEN** 配置文件中存在当前版本不认识的字段
- **THEN** 设置程序或插件保存 `autoStart` 后，该字段仍然存在

### Requirement: 独立设置程序

便携根目录 SHALL 包含 `QQPortable.setting.exe`。该程序 SHALL 使用 WinForms，SHALL 在 100%、150% 和 200% 缩放下完整显示开关、说明和错误提示，SHALL 在切换开机自启后立即同步 Run 项，并 SHALL 显示当前配置文件的完整路径。写入失败时 SHALL 显示明确的错误信息，不得静默忽略。

#### Scenario: 开启自启

- **WHEN** 用户打开设置程序并勾选开机自启
- **THEN** `QQPortable.json` 中的 `autoStart` 变为 `true`
- **AND** 当前用户 Run 项立即出现指向本目录的启动命令

#### Scenario: 关闭自启

- **WHEN** 用户取消勾选开机自启
- **THEN** `QQPortable.json` 中的 `autoStart` 变为 `false`
- **AND** 当前用户 Run 项立即删除该启动命令

#### Scenario: 配置损坏

- **WHEN** 打开设置程序时 `QQPortable.json` 不是有效 JSON
- **THEN** 设置程序显示解析错误，不覆盖原文件

#### Scenario: 高 DPI 可用

- **WHEN** 在 150% 或 200% 缩放下打开设置程序
- **THEN** 标题、开关、说明和按钮文字完整显示，不被窗口或固定宽度裁切

### Requirement: QQ 内插件设置页

安装器 SHALL 在 `LiteLoader\plugins\qqportable_settings` 安装控制插件。插件 SHALL 在 LiteLoader 的 QQ 设置页中提供开机自启开关，切换后 SHALL 写入同一份 `QQPortable.json` 并立即同步 Run 项。同步失败时插件 SHALL 回滚配置文件并显示错误，不得让界面显示与注册表不一致的状态。

#### Scenario: 打开插件设置页

- **WHEN** QQ 启动后用户打开 LiteLoader 插件设置中的 QQPortable 控制页
- **THEN** 看到开机自启开关和当前状态

#### Scenario: 在 QQ 内开启自启

- **WHEN** 用户在插件设置页打开开机自启
- **THEN** 配置文件和当前用户 Run 项同时更新

#### Scenario: 注册表写入失败

- **WHEN** 插件无法写入当前用户 Run 项
- **THEN** 插件把 `autoStart` 回滚到修改前的值并显示错误信息

### Requirement: 安装器附带控制组件

安装器 SHALL 在安装完成后把 `QQPortable.setting.exe`、默认 `QQPortable.json` 和控制插件写入目标目录。安装器 SHALL NOT 改动用户自行放入 `LiteLoader\plugins` 的其他插件，SHALL NOT 创建快捷方式，SHALL NOT 修改聊天数据。

#### Scenario: 全新安装

- **WHEN** 安装器完成一次全新安装
- **THEN** 根目录包含 `QQPortable.setting.exe` 和 `QQPortable.json`
- **AND** `LiteLoader\plugins\qqportable_settings` 包含完整插件文件

#### Scenario: 其他插件不受影响

- **WHEN** 用户自行放入其他 LiteLoader 插件
- **THEN** 安装 QQPortable 控制组件不会修改这些插件的文件或配置

#### Scenario: 默认配置

- **WHEN** 安装完成但用户尚未修改配置
- **THEN** `QQPortable.json` 中 `autoStart` 为 `false`，安装器不会自动注册开机自启
