# Architecture

## Target layout

```text
PortableRoot\
  QQPortable.exe
  QQPortable.setting.exe
  QQPortable.json
  QQ\
    QQ.exe
    versions\<version>\resources\app\package.json
  LiteLoader\
    package.json
    src\
    plugins\
      qqportable_settings\
        manifest.json
        src\
          main.cjs
          preload.cjs
          renderer.mjs
    data\
  Launcher\
    NapCatWinBootMain.exe
    NapCatWinBootHook.dll
    runtime\
      qqnt.json
      loadLiteLoader.js
```

`runtime` 是启动器生成的，不放源码或安装脚本。`QQPortable.json` 是启动器、设置程序和控制插件共用的配置来源。

## Launcher

Startup sequence:

```text
QQPortable.exe
  -> --restart? 打开 Local\QQPortable.Restart.<hash> 并退出
  -> 读取 QQPortable.json；缺失时写入默认值
  -> 按 autoStart 同步 HKCU\...\Run\QQPortable
  -> 取得 Local\QQPortable.Instance.<hash> 单实例互斥量
  -> check running QQ by executable path; activate and exit
  -> find latest QQ\versions\*\resources\app\package.json
  -> set package.main = "./loadNapCat.js"
  -> write Launcher\runtime\qqnt.json
  -> write Launcher\runtime\loadLiteLoader.js
  -> set NAPCAT_PATCH_PACKAGE / NAPCAT_LOAD_PATH
  -> create Local\QQPortable.Restart.<hash> event
  -> session loop
```

A session starts `Launcher\NapCatWinBootMain.exe <QQ.exe> <Hook.dll>` suspended and assigns it to a Windows Job before resuming, so its descendants belong to this session from creation. Job creation notifications identify QQ clients; registered waits on their process handles report actual termination without polling. Job exit notifications alone are insufficient because outstanding process handles can delay them. Once all tracked QQ clients have terminated, the launcher ends the remaining Job members and waits for the Job to become empty. Console hosts and crash handlers do not keep the session alive. Hiding QQ to the tray does not terminate its process and does not trigger cleanup.

The launcher has no tray icon of its own.

### Configuration and autostart

`QQPortable.json` currently contains `autoStart`. Missing files are written with `autoStart: false`. Invalid JSON stops startup with an error dialog; the launcher does not guess a value.

The launcher writes `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\QQPortable` with the quoted path to this directory's `QQPortable.exe`. Disabling removes that value. Enabling also removes the matching `StartupApproved\Run` value so a previous Task Manager disable does not block the new registration. No administrator rights are required. The value name is fixed, so one Windows user keeps one QQPortable startup entry; enabling another portable directory replaces the previous one.

### Restart

The control plugin adds a restart item to QQ's bottom-left “More” menu, below “设置”. Clicking it starts `QQPortable.exe --restart` as a detached helper. That mode only opens the named event and sets it.

The session loop watches the event. On a request it calls `TerminateJobObject` for the current session, waits for the Job to become empty, and creates a new session through the same NapCat/LiteLoader startup path. The restart is a forced process termination, not a QQ logout, so unsent input and local writes may be lost or require recovery on the next login. The launcher does not restart automatically; only an explicit user action does.

The event and mutex names use a hash of the normalized portable root, so different portable directories do not signal or block each other.

## Settings program

`QQPortable.setting.exe` is a net48 WinForms program that reads and writes the same `QQPortable.json`. Toggling the checkbox saves the file atomically and immediately synchronizes the Run value. `--apply` is a silent mode that reads the configuration, synchronizes the Run value, and exits with a nonzero status on failure; the control plugin uses it so the registry logic exists only once.

The settings program embeds a per-monitor V2 manifest, reads the actual DPI with `GetDpiForWindow`, and scales window and control sizes without scaling font points. Description and configuration path text wrap to the available width.

`src/QQPortable.Shared` holds the configuration reader/writer and Run value synchronization. Its source files are linked into the launcher, settings program, and installer; no extra DLL is placed next to the executables.

## Control plugin

`LiteLoader\plugins\qqportable_settings` is the only bundled plugin. Its manifest uses version 4 and injects `main`, `preload`, and `renderer`.

The renderer listens for clicks on the bottom-left three-line “更多” button during the capture phase. Before QQ opens the menu, it finds the button's Vue component instance and inserts a “重启 QQ” item into the `menuItem` array immediately after “设置”. QQ's own context-menu component renders the item, its icon, and its position. Clicking the item invokes a preload IPC that spawns `QQPortable.exe --restart`; the launcher owns the actual session restart.

The settings page is created with `onSettingWindowCreated`. The preload exposes `getConfig`, `setAutoStart`, and `restartQQ`. Saving writes the JSON with unknown fields preserved, then runs `QQPortable.setting.exe --apply`; if that fails, the plugin rolls the JSON back and reports the error.

## Installer

The installer embeds:

- Built `QQPortable.exe`
- Built `QQPortable.setting.exe`
- `qqportable_settings` manifest and source files
- `NapCatWinBootMain.exe`
- `NapCatWinBootHook.dll`
- `7za.exe` and its license

It downloads QQ and LiteLoader from GitHub, extracts to a temporary directory, copies a clean result to the selected empty target, applies the LiteLoader patches, writes the launcher and settings program, installs the control plugin, and writes the default `QQPortable.json`. It does not create a shortcut and does not change plugins the user adds later.

QQ installer handling:

1. Extract the outer archive with 7za.
2. If no `QQ.exe`, scan the installer for 7z signatures and extract slices.
3. Copy the directory that contains `QQ.exe`.

LiteLoader patches:

- `src/main/store.js`: Node 24 `dirent.parentPath ?? dirent.path`.
- `src/preload.js`: skip preload injection on `login.html`.
- `src/preload/module.js`: support `.cjs`.

The patcher fails loudly if a selected LiteLoader version does not match these patterns.

## Catalog

- QQ: `a1515333102/QQ-Releases` and `Rodert/qq-versions` GitHub releases; filter x64 `.exe` assets and merge duplicate versions.
- LiteLoader: `LiteLoaderQQNT/LiteLoaderQQNT` GitHub releases; use `.zip` assets.

The catalog first requests `api.github.com` release JSON, optionally authenticated by `GITHUB_TOKEN` or `GH_TOKEN`. If the API is rate limited, rejects the request, or cannot be reached, it falls back to `releases.atom` plus direct `github.com` release download URLs. The fallback lists the latest Atom entries rather than every historical release.

API catalog downloads use asset URLs with `Accept: application/octet-stream`. Publish-page fallback downloads use direct release URLs, so loading the catalog does not consume API quota.

## Build order

```text
dotnet build QQPortable.Launcher (net48)
  -> copy launcher exe + NapCat files + 7za into Installer\Assets
dotnet build QQPortable.Settings (net48)
  -> copy QQPortable.setting.exe + plugin files into Installer\Assets
dotnet build QQPortable.Installer (net48)
  -> embed launcher, settings program, plugin, NapCat files, 7za
```

The installer project embeds the assets at compile time, so it must be built after the launcher, settings program, and asset copies. `build.ps1` also writes `qqportable_settings.zip` into the installer output directory for manual updates from a release.
