# QQ Portable Suite

在 2026 年 10 月能跑通的 QQ 9.9.36 + LiteLoaderQQNT 1.4.1 便携安装器和启动器。

目标是让安装后的目录只剩四样东西：

```text
<便携目录>\
  QQ\                    官方 QQ 程序
  LiteLoader\            LiteLoader 本体、插件和数据
  Launcher\              启动 EXE、NapCat 注入文件和运行时配置
  QQ.lnk                 指向 Launcher\QQPortable.Launcher.exe
```

没有 `start.vbs`、`prepare-qqnt.ps1`、`Launcher.cs`、`QQ.ico` 或 7-Zip 解压工具留在便携目录里。

## 项目结构

```text
QQPortable\
  QQPortable.sln
  build.ps1
  assets\
    launcher.ico
    launcher\              NapCatWinBootMain.exe / NapCatWinBootHook.dll
    tools\                 7za.exe / 7za-License.txt
  src\QQPortable.Launcher\ net48 单文件启动器
  src\QQPortable.Installer\net48 WinForms 安装器
  src\QQPortable.Installer\Assets\  构建时嵌入安装器的二进制
  docs\architecture.md
```

两个项目都是 .NET Framework 4.8，Windows 10/11 自带运行时，不需要额外安装 .NET。

## 构建与 CI

```powershell
cd F:\Desktop\project\QQPortable
dotnet test QQPortable.sln -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
```

仓库使用传统 `.sln`。`.slnx` 是较新的 XML 解决方案格式，旧版 Visual Studio 和部分 CI 环境支持不完整，项目不依赖它。

GitHub Actions：

- 每次 push 和 pull request 执行 `dotnet test`，然后再构建启动器和安装器，上传两个 EXE 作为构建产物。
- 推送 `v*` 标签时执行同一套测试和构建，并自动创建 GitHub Release，附带安装器、启动器和第三方许可文件。

```powershell
git tag v0.1.0
git push origin v0.1.0
```

Release 的 EXE 没有代码签名。

产物：

```text
src\QQPortable.Launcher\bin\Release\net48\QQPortable.Launcher.exe
src\QQPortable.Installer\bin\Release\net48\QQPortable.Installer.exe
```

`build.ps1` 会先构建启动器，把启动器和 NapCat 注入文件复制到安装器的内嵌资源，再构建安装器。安装器发布时只需要这一个 EXE。

## 安装器

安装器启动后：

1. 选择目标目录，目标必须为空；不会修改现有安装。
2. 选择 QQ 版本，列表来自 [a1515333102/QQ-Releases](https://github.com/a1515333102/QQ-Releases)。
3. 选择 LiteLoader 版本，列表来自 [LiteLoaderQQNT/LiteLoaderQQNT](https://github.com/LiteLoaderQQNT/LiteLoaderQQNT)。
4. 点击安装。安装器会下载 QQ 安装包和 LiteLoader，解压，应用 LiteLoader 兼容补丁，写入启动器，并创建 `QQ.lnk`。

安装完成后，`LiteLoader\plugins` 是空的；插件由用户自己安装和配置，安装器不会改动插件。

命令行模式：

```powershell
QQPortable.Installer.exe --install --target D:\QQPortable `
  [--qq-version 9.9.36] [--liteloader-version 1.4.1] `
  [--qq-installer <本地安装包>] [--liteloader-directory <本地目录>] [--force]
```

`--qq-installer` 和 `--liteloader-directory` 主要用于本地测试或离线安装。

## 启动器

`Launcher\QQPortable.Launcher.exe` 做这些事：

- 检查目标便携目录的 `QQ.exe` 是否已经在运行；已运行则激活现有窗口，不启动第二份。
- 在 `Launcher\runtime` 生成 `qqnt.json` 和 `loadLiteLoader.js`。
- 设置 `NAPCAT_PATCH_PACKAGE`、`NAPCAT_LOAD_PATH`。
- 隐藏启动 `Launcher\NapCatWinBootMain.exe`。

没有 VBS，也没有 PowerShell。生成入口配置使用 .NET Framework 自带的 JSON 序列化，逻辑都在 EXE 里。

`--prepare-only` 只生成配置并退出，用来检查目录结构，不启动 QQ。

## QQ 安装包格式

GitHub 上的 QQ 安装包是 7z SFX，外层 7z 里只有许可文件，真正的 QQ 文件在后面的内层 7z 中。安装器会：

1. 先用内嵌的 `7za.exe` 尝试直接解压。
2. 找不到 `QQ.exe` 时扫描文件中的 7z 签名，切片内层归档再解压。
3. 找到包含 `QQ.exe` 的目录并复制到 `<目标>\QQ`。

这个流程已经用 `QQ_9.9.36_260924_x64_01.exe` 实测。

## 限制

- 便携只覆盖程序目录。QQ 的登录状态、聊天记录和缓存仍在 `%APPDATA%\QQ` 和 `%LOCALAPPDATA%\QQ`。
- LiteLoader 主仓库已归档；安装器内置的是针对 1.4.1 的补丁。选择其他 LiteLoader 版本时，如果补丁匹配失败会直接报错，不会静默跳过。
- QQ 后续版本如果改变安装包或启动结构，安装器和注入文件可能需要跟着更新。
- 没有代码签名。
- 当前已有的 `D:\App\QQ.9.9.36.Portable` 不参与构建和测试；测试使用独立的空目录并已清理。
