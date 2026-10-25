param(
    [switch]$SkipLauncherCopy
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $SkipLauncherCopy) {
    dotnet build (Join-Path $root 'src\QQPortable.Launcher\QQPortable.Launcher.csproj') -c Release
    if ($LASTEXITCODE -ne 0) { throw 'Launcher build failed' }
    Copy-Item -Force `
        (Join-Path $root 'src\QQPortable.Launcher\bin\Release\net48\QQPortable.exe') `
        (Join-Path $root 'src\QQPortable.Installer\Assets\launcher\QQPortable.exe')
    Copy-Item -Force (Join-Path $root 'assets\launcher\NapCatWinBootMain.exe') (Join-Path $root 'src\QQPortable.Installer\Assets\launcher\NapCatWinBootMain.exe')
    Copy-Item -Force (Join-Path $root 'assets\launcher\NapCatWinBootHook.dll') (Join-Path $root 'src\QQPortable.Installer\Assets\launcher\NapCatWinBootHook.dll')
    Copy-Item -Force (Join-Path $root 'assets\tools\7za.exe') (Join-Path $root 'src\QQPortable.Installer\Assets\tools\7za.exe')
    Copy-Item -Force (Join-Path $root 'assets\tools\7za-License.txt') (Join-Path $root 'src\QQPortable.Installer\Assets\tools\7za-License.txt')
}

dotnet build (Join-Path $root 'src\QQPortable.Settings\QQPortable.Settings.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Settings build failed' }

New-Item -ItemType Directory -Force -Path (Join-Path $root 'src\QQPortable.Installer\Assets\settings') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $root 'src\QQPortable.Installer\Assets\plugins\qqportable_settings\src') | Out-Null
Copy-Item -Force `
    (Join-Path $root 'src\QQPortable.Settings\bin\Release\net48\QQPortable.setting.exe') `
    (Join-Path $root 'src\QQPortable.Installer\Assets\settings\QQPortable.setting.exe')
Copy-Item -Force (Join-Path $root 'assets\plugins\qqportable_settings\manifest.json') (Join-Path $root 'src\QQPortable.Installer\Assets\plugins\qqportable_settings\manifest.json')
Copy-Item -Force (Join-Path $root 'assets\plugins\qqportable_settings\src\main.cjs') (Join-Path $root 'src\QQPortable.Installer\Assets\plugins\qqportable_settings\src\main.cjs')
Copy-Item -Force (Join-Path $root 'assets\plugins\qqportable_settings\src\preload.cjs') (Join-Path $root 'src\QQPortable.Installer\Assets\plugins\qqportable_settings\src\preload.cjs')
Copy-Item -Force (Join-Path $root 'assets\plugins\qqportable_settings\src\renderer.mjs') (Join-Path $root 'src\QQPortable.Installer\Assets\plugins\qqportable_settings\src\renderer.mjs')

dotnet build (Join-Path $root 'src\QQPortable.Installer\QQPortable.Installer.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$pluginZip = Join-Path $root 'src\QQPortable.Installer\bin\Release\net48\qqportable_settings.zip'
if (Test-Path $pluginZip) { Remove-Item -Force $pluginZip }
[System.IO.Compression.ZipFile]::CreateFromDirectory(
    (Join-Path $root 'assets\plugins\qqportable_settings'),
    $pluginZip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)

Write-Output "Launcher: $(Join-Path $root 'src\QQPortable.Launcher\bin\Release\net48\QQPortable.exe')"
Write-Output "Settings: $(Join-Path $root 'src\QQPortable.Settings\bin\Release\net48\QQPortable.setting.exe')"
Write-Output "Installer: $(Join-Path $root 'src\QQPortable.Installer\bin\Release\net48\QQPortable.Installer.exe')"
Write-Output "Plugin package: $pluginZip"
