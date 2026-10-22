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
```

The launcher is a `WinExe` targeting .NET Framework 4.8. It has no script files and no console window.

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

- QQ: `a1515333102/QQ-Releases` GitHub releases; filter x64 `.exe` assets.
- LiteLoader: `LiteLoaderQQNT/LiteLoaderQQNT` GitHub releases; use `.zip` assets.

Downloads use GitHub API asset URLs with `Accept: application/octet-stream`, because direct `github.com` release URLs may be unreachable while `api.github.com` works.

## Build order

```text
dotnet build QQPortable.Launcher (net48)
  -> copy launcher exe + NapCat files + 7za into Installer\Assets
  -> dotnet build QQPortable.Installer (net48)
```

The installer project embeds the assets at compile time, so it must be built after the launcher and asset copy.
