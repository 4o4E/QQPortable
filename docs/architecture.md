# Architecture

## Target layout

```text
PortableRoot\
  QQPortable.exe
  QQ\
    QQ.exe
    versions\<version>\resources\app\package.json
  LiteLoader\
    package.json
    src\
    plugins\
    data\
  Launcher\
    NapCatWinBootMain.exe
    NapCatWinBootHook.dll
    runtime\
      qqnt.json
      loadLiteLoader.js
```

`runtime` 是启动器生成的，不放源码或安装脚本。

## Launcher

Startup sequence:

```text
QQPortable.exe
  -> check running QQ by executable path
  -> find latest QQ\versions\*\resources\app\package.json
  -> set package.main = "./loadNapCat.js"
  -> write Launcher\runtime\qqnt.json
  -> write Launcher\runtime\loadLiteLoader.js
  -> set NAPCAT_PATCH_PACKAGE / NAPCAT_LOAD_PATH
  -> start Launcher\NapCatWinBootMain.exe <QQ.exe> <Hook.dll>
  -> supervise this session until QQ client processes exit
  -> terminate remaining bootstrap/console helpers in this session
```

The launcher is a `WinExe` targeting .NET Framework 4.8. It has no script files and no console window.

The bootstrap is created suspended and assigned to a Windows Job before resuming, so its descendants belong to this session from creation. Job creation notifications identify QQ clients; registered waits on their process handles report actual termination without polling. Job exit notifications alone are insufficient because outstanding process handles can delay them. Once all tracked QQ clients have terminated, the launcher ends the remaining Job members and waits for the Job to become empty. Console hosts and crash handlers do not keep the session alive. Hiding QQ to the tray does not terminate its process and does not trigger cleanup.

## Installer

The installer embeds:

- Built `QQPortable.exe`
- `NapCatWinBootMain.exe`
- `NapCatWinBootHook.dll`
- `7za.exe` and its license

It downloads QQ and LiteLoader from GitHub, extracts to a temporary directory, copies a clean result to the selected empty target, applies the LiteLoader patches, and writes the launcher to the target root. It does not create a shortcut.

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
  -> dotnet build QQPortable.Installer (net48)
```

The installer project embeds the assets at compile time, so it must be built after the launcher and asset copy.
