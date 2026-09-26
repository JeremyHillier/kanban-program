# Builds a self-contained publish and compiles an installer for each requested channel.
# Run from anywhere; paths are resolved relative to this script's location.
# Defaults to Production only; pass -Channels Production,Test to also build the Test installer.

param([string[]]$Channels = @("Production"))

$ErrorActionPreference = "Stop"

$installerDir = $PSScriptRoot
$repoRoot = Split-Path $installerDir -Parent
$csprojPath = Join-Path $repoRoot "KanbanApp.csproj"

[xml]$csproj = Get-Content $csprojPath
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) {
    throw "Could not read <Version> from $csprojPath"
}
Write-Output "Version: $version"

$iscc = $null
$isccCandidates = @(
    (Get-Command iscc -ErrorAction SilentlyContinue).Source,
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
foreach ($candidate in $isccCandidates) {
    if ($candidate -and (Test-Path $candidate)) { $iscc = $candidate; break }
}
if (-not $iscc) {
    throw "ISCC.exe (Inno Setup compiler) not found. Install it with: winget install --id JRSoftware.InnoSetup -e"
}
Write-Output "ISCC: $iscc"

# Code signing (installer\Signing.ps1): off until installer\signing.json exists, then Production is signed.
. (Join-Path $installerDir "Signing.ps1")
$signing = Get-SigningSetup $installerDir
if ($signing) { Write-Output "Signing: on, with $($signing.SignTool)" } else { Write-Output "Signing: off (no installer\signing.json)" }

$channels = $Channels

foreach ($channel in $channels) {
    $publishDir = Join-Path $repoRoot "publish\$channel"
    Write-Output "`n=== Publishing $channel to $publishDir ==="
    & dotnet publish $csprojPath `
        -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:AppChannel=$channel `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed for $channel channel" }
    if ($signing -and $channel -eq "Production") { Invoke-CodeSigning $signing (Join-Path $publishDir "KanbanApp.exe") }
}

New-Item -ItemType Directory -Force -Path (Join-Path $installerDir "Output") | Out-Null

foreach ($channel in $channels) {
    Write-Output "`n=== Compiling installer for $channel ==="
    $isccArgs = @("/DChannel=$channel", "/DMyAppVersion=$version")
    if ($signing -and $channel -eq "Production") { $isccArgs += "/DSigned=1", (Get-InnoSignSwitch $signing) }
    & $iscc @isccArgs (Join-Path $installerDir "KanbanTaskBoard.iss")
    if ($LASTEXITCODE -ne 0) { throw "ISCC failed for $channel channel" }
    if ($signing -and $channel -eq "Production") {
        Test-CodeSignatures $signing @((Join-Path $repoRoot "publish\Production\KanbanApp.exe"), (Join-Path $installerDir "Output\Kanban Task Board-Setup-$version.exe"))
    }
}

Write-Output "`nDone. Installers are in $installerDir\Output"
