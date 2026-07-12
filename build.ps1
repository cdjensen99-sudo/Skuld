param(
    [string]$ValheimPath = "D:\SteamLibrary\steamapps\common\Valheim",
    [string]$DeployProfile = "C:\Users\cdjen\AppData\Roaming\r2modmanPlus-local\Valheim\profiles\Testing",
    [switch]$Deploy,
    [switch]$Package
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root "src\Skuld\Skuld.csproj"
$dll = Join-Path $root "artifacts\Skuld.dll"
$thunderstore = Join-Path $root "thunderstore"
$manifest = Get-Content (Join-Path $thunderstore "manifest.json") | ConvertFrom-Json

dotnet build $project -p:ValheimPath=$ValheimPath -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Built: $dll"

if ($Deploy) {
    $pluginDir = Join-Path $DeployProfile "BepInEx\plugins\Hardwire99-Skuld"
    New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
    $dest = Join-Path $pluginDir "Skuld.dll"

    try {
        Copy-Item $dll $dest -Force
        Copy-Item (Join-Path $thunderstore "manifest.json") (Join-Path $pluginDir "manifest.json") -Force
        Copy-Item (Join-Path $thunderstore "CHANGELOG.md") (Join-Path $pluginDir "CHANGELOG.md") -Force
        Write-Host "Deployed to $dest"
    }
    catch {
        $pending = Join-Path $pluginDir "Skuld.dll.pending"
        Copy-Item $dll $pending -Force
        Write-Warning "Valheim has the plugin locked. Close the game, then replace Skuld.dll with Skuld.dll.pending"
        Write-Host "Built update saved to $pending"
    }
}

if ($Package) {
    $staging = Join-Path $root "artifacts\thunderstore-staging"
    $team = "Hardwire99"
    $packageName = "{0}-{1}-{2}.zip" -f $team, $manifest.name, $manifest.version_number
    $packagePath = Join-Path (Join-Path $root "artifacts") $packageName

    if (Test-Path $staging) {
        Remove-Item $staging -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $staging | Out-Null

    Copy-Item $dll (Join-Path $staging "Skuld.dll") -Force
    Copy-Item (Join-Path $thunderstore "manifest.json") (Join-Path $staging "manifest.json") -Force
    Copy-Item (Join-Path $thunderstore "README.md") (Join-Path $staging "README.md") -Force
    Copy-Item (Join-Path $thunderstore "CHANGELOG.md") (Join-Path $staging "CHANGELOG.md") -Force

    $iconSource = Join-Path $thunderstore "icon.png"
    if (Test-Path $iconSource) {
        Copy-Item $iconSource (Join-Path $staging "icon.png") -Force
    }
    else {
        Write-Warning "thunderstore\icon.png is missing. Add a 256x256 PNG before uploading to Thunderstore."
    }

    if (Test-Path $packagePath) {
        Remove-Item $packagePath -Force
    }

    $files = Get-ChildItem $staging -File | ForEach-Object { $_.FullName }
    Compress-Archive -Path $files -DestinationPath $packagePath -Force
    Write-Host "Packaged: $packagePath"
    Write-Host "Upload as team $team. Thunderstore will show the name as '$($manifest.name)'."
}

if (-not $Deploy -and -not $Package) {
    Write-Host "Skipped deploy/package. Pass -Deploy and/or -Package as needed."
}
