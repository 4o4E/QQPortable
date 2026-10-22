# AGENTS.md

本文件是 QQPortable 的开发说明。用户安装和使用说明只放在 `README.md`，不要把构建、发布、源码结构或实现细节写回 README。

## 项目目标

为 Windows 10/11 x64 提供 QQ 便携安装器和启动器：

- 安装器是单个 `QQPortable.Installer.exe`，内嵌启动器、NapCat 注入文件和 7-Zip。
- 安装后的根目录只有 `QQPortable.exe`、`QQ`、`LiteLoader` 和 `Launcher`。
- 启动器负责准备 LiteLoader/NapCat 入口、启动 QQ，并监管本次启动的进程。
- 不默认创建快捷方式，不修改 QQ 的聊天数据，不捆绑插件。

## 目录

```text
QQPortable\
  README.md                    用户安装和使用说明
  AGENTS.md                    本文件
  QQPortable.sln
  build.ps1                    构建启动器、复制内嵌资源、构建安装器
  assets\
    launcher.ico               启动器和安装器图标
    launcher\                  NapCatWinBootMain.exe / NapCatWinBootHook.dll
    tools\                     7za.exe / 7za-license
  src\
    QQPortable.Launcher\       net48 单文件启动器
    QQPortable.Installer\      net48 WinForms 安装器
    QQPortable.Tests\          net48 xUnit 测试
  docs\architecture.md         详细架构和流程
  openspec\                    OpenSpec 配置、规格和变更
  .agents\skills\              OpenSpec 工作流技能
```

## 构建与测试

```powershell
cd F:\Desktop\project\QQPortable
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
dotnet test QQPortable.sln -c Release --no-restore
```

构建顺序不能颠倒：

1. 构建 `QQPortable.Launcher`。
2. 把启动器、NapCat 文件和 `7za` 复制到 `src\QQPortable.Installer\Assets`。
3. 构建 `QQPortable.Installer`，把这些文件作为嵌入资源打进单个 EXE。

产物：

```text
src\QQPortable.Launcher\bin\Release\net48\QQPortable.exe
src\QQPortable.Installer\bin\Release\net48\QQPortable.Installer.exe
```

两个项目都是 .NET Framework 4.8，使用传统 `.sln`。不要改成 `.slnx`。

## 发布

发布只通过标签触发：

```powershell
git tag v<version>
git push origin v<version>
```

推送 `v*` 标签会运行 `.github/workflows/release.yml`：构建、测试、创建 GitHub Release，并上传安装器、启动器、第三方说明和 7-Zip 许可证。

同时更新两个项目的 `<Version>`，提交信息使用 Conventional Commits：

```text
type(scope): 中文说明
```

不要把版本号或构建细节写进 README。

## 启动器契约

- 先检查目标便携目录的 `QQ.exe` 是否正在运行；已运行则激活窗口并退出，不启动第二份。
- 找到最新的 `QQ\versions\*\resources\app\package.json`。
- 写入 `Launcher\runtime\qqnt.json`，设置 `package.main` 为 `./loadNapCat.js`。
- 写入 `Launcher\runtime\loadLiteLoader.js`。
- 设置 `NAPCAT_PATCH_PACKAGE`、`NAPCAT_LOAD_PATH`。
- 通过 Windows Job 以挂起方式启动 `NapCatWinBootMain.exe`，加入 Job 后再恢复，避免遗漏早期子进程。
- 只跟踪本次 Job 中的 QQ 进程。全部 QQ 客户端退出后，结束本次 NapCat、控制台宿主和启动器；关闭窗口隐藏到托盘不会触发清理。
- 不按进程名称结束其他目录的 QQ 会话。

`--prepare-only` 只生成运行时配置并退出，不启动 QQ。

## 安装器契约

- 目标目录必须为空；不能覆盖已有安装。
- QQ 版本目录优先请求 GitHub API；遇到 403、429、网络错误或超时时，回退到 `releases.atom` 和 GitHub 发布页直链。回退不消耗 API 额度，但只包含 Atom 中的最新版本。
- 支持 `GITHUB_TOKEN` 或 `GH_TOKEN` 提高 API 额度。
- QQ 安装包先尝试直接解压；找不到 `QQ.exe` 时扫描内层 7z 签名再解压；最后复制包含 `QQ.exe` 的目录。
- LiteLoader 的兼容补丁必须匹配当前目标版本；匹配失败时直接报错，不能静默跳过。
- 安装完成后不创建快捷方式，不改动 `LiteLoader\plugins` 内容。

## UI 与高 DPI

- 安装器是 WinForms，必须保持高 DPI 可用。
- 使用嵌入的 per-monitor V2 清单；窗口创建后通过 `GetDpiForWindow` 读取实际 DPI，手动缩放控件尺寸。不要依赖外部 `.exe.config` 提供 DPI 配置，发布包只有单个 EXE。
- 字体按点数由系统渲染，缩放控件尺寸时不要放大字体。
- 安装按钮、底部提示、状态文字和完成页摘要必须随窗口宽度铺满，不能被固定像素宽度裁切。
- 标题栏和任务栏使用嵌入的企鹅图标，不要依赖默认 WinForms 图标。
- 修改 UI 后在 100%、150% 和 200% 缩放下检查文字、按钮和窗口默认尺寸。

## 代码约定

- 错误就地暴露，使用 `throw` 和明确的错误消息；不要吞异常，不要加无意义校验或静默回退。
- 不使用 mock、假实现或只为测试通过的 workaround。
- 不保留调试日志、临时环境变量开关、诊断代码或未使用的辅助方法。
- 新增功能先判断是否属于现有文件；避免为小改动建立平行抽象。
- 二进制资源必须通过 `build.ps1` 更新，不手工编辑内嵌资源。
- 改动启动器或安装器资源时，提交对应的 `src\QQPortable.Installer\Assets\launcher\QQPortable.exe`，保证安装器内嵌版本与源码一致。

## 验证

- 修改安装器逻辑：运行 `dotnet test`，用临时空目录实际安装一次，检查根目录结构和启动。
- 修改启动器进程监管：在独立临时目录启动真实 QQ，检查关闭窗口隐藏到托盘不触发清理、完整退出后不残留 NapCat/控制台宿主。不要使用现有用户目录作为测试目录。
- 修改 UI：启动真实窗体，检查 DPI、窗口尺寸、按钮文字、图标和控件边界。
- 修改版本目录：分别验证正常 API 路径和限流回退路径，并确认直链可访问。
- 不要通过运行测试重复确认用户已经报告过的错误；测试只用于验证修复。

## 文档职责

- `README.md`：只写安装、启动、目录、插件、命令行安装和便携范围。
- `AGENTS.md`：开发流程、构建、发布、代码契约和验证要求。
- `docs/architecture.md`：详细架构、数据流和流程说明；修改架构时同步更新。
- `openspec/`：需求规格和变更记录；业务行为变化先走 OpenSpec。

## OpenSpec

项目使用 OpenSpec 1.x 的 `spec-driven` 工作流，配置在 `openspec/config.yaml`，工作流技能在 `.agents/skills/openspec-*`。所有 OpenSpec 产出使用中文，保留 OpenSpec 的英文结构标题和 `SHALL`/`MUST` 关键字。

流程：

1. 需求涉及新的用户可见行为、兼容性或验收标准时，先用 `openspec-propose` 技能生成 `proposal.md`、delta spec、`design.md` 和 `tasks.md`。
2. 规划阶段只创建规划文件，不修改项目代码；用户确认后再进入 apply。
3. 实现完成后通过 `openspec-archive-change` 归档，并把行为变化同步到 `openspec/specs`。
4. 小范围文档、构建或纯内部维护改动可以直接实施，但仍需遵守本文件。

常用命令：

```powershell
openspec list
openspec status --change <change-name> --json
openspec validate --all
```

OpenSpec 配置中的 context、rules 和 operations 只放项目级约束，不要复制整份用户说明或代码细节。
