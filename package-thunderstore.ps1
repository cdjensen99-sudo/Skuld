param(
    [string]$ValheimPath = "D:\SteamLibrary\steamapps\common\Valheim"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path $root "build.ps1") -ValheimPath $ValheimPath -Package
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
