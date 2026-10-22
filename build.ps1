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

dotnet build (Join-Path $root 'src\QQPortable.Installer\QQPortable.Installer.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed' }

Write-Output "Launcher: $(Join-Path $root 'src\QQPortable.Launcher\bin\Release\net48\QQPortable.exe')"
Write-Output "Installer: $(Join-Path $root 'src\QQPortable.Installer\bin\Release\net48\QQPortable.Installer.exe')"
