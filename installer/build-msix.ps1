# Builds a self-contained publish and packages it as an MSIX for Microsoft Store submission
# (or local sideload testing). Run from anywhere; paths are resolved relative to this script's
# location.
#
# IMPORTANT before Store submission: installer\msix\AppxManifest.xml still has placeholder
# Identity Name/Publisher values - replace them with the ones reserved for this app in Partner
# Center (App management > App identity), then rerun this script.
#
# The MSIX packaging tools (makeappx.exe) are pulled from the Microsoft.Windows.SDK.BuildTools
# NuGet package rather than requiring a full Windows SDK install.

$ErrorActionPreference = "Stop"

$installerDir = $PSScriptRoot
$repoRoot = Split-Path $installerDir -Parent
$csprojPath = Join-Path $repoRoot "KanbanApp.csproj"
$manifestTemplatePath = Join-Path $installerDir "msix\AppxManifest.xml"

[xml]$csproj = Get-Content $csprojPath
$version = $csproj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
if (-not $version) {
    throw "Could not read <Version> from $csprojPath"
}
$msixVersion = "$version.0"
Write-Output "Version: $version (MSIX: $msixVersion)"

# --- Locate makeappx.exe, restoring the packaging tools via NuGet if needed ---
$toolsProj = Join-Path $installerDir "msix-tools\msix-tools.csproj"
& dotnet restore $toolsProj | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Failed to restore MSIX packaging tools" }

$nugetPackagesDir = ((& dotnet nuget locals global-packages --list) -replace "^global-packages:\s*", "").Trim()
$buildToolsVersionDir = Get-ChildItem (Join-Path $nugetPackagesDir "microsoft.windows.sdk.buildtools") -Directory |
    Sort-Object Name -Descending | Select-Object -First 1
if (-not $buildToolsVersionDir) { throw "Microsoft.Windows.SDK.BuildTools package not found after restore" }

$makeAppx = Get-ChildItem $buildToolsVersionDir.FullName -Recurse -Filter "makeappx.exe" |
    Where-Object { $_.FullName -match "\\x64\\" } | Select-Object -First 1 -ExpandProperty FullName
if (-not $makeAppx) { throw "makeappx.exe not found under $($buildToolsVersionDir.FullName)" }
Write-Output "makeappx: $makeAppx"

# --- Publish a self-contained build. Not single-file: MSIX already handles packaging and
# deployment, so a plain folder publish avoids layering .NET's self-extraction on top of AppX's
# own file virtualization. ---
$publishDir = Join-Path $repoRoot "publish\MSIX"
Write-Output "`n=== Publishing Production (MSIX) to $publishDir ==="
& dotnet publish $csprojPath `
    -c Release -r win-x64 --self-contained true `
    -p:AppChannel=Production `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

# --- Assemble the package layout: published app + manifest + tile assets ---
$layoutDir = Join-Path $repoRoot "publish\MSIX-Layout"
if (Test-Path $layoutDir) { Remove-Item $layoutDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $layoutDir | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $layoutDir "Assets") | Out-Null

Copy-Item (Join-Path $publishDir "*") $layoutDir -Recurse -Force
Copy-Item (Join-Path $repoRoot "Assets\MsixIcons\*.png") (Join-Path $layoutDir "Assets") -Force

$manifestContent = (Get-Content $manifestTemplatePath -Raw) -replace '\{VERSION\}', $msixVersion
Set-Content -Path (Join-Path $layoutDir "AppxManifest.xml") -Value $manifestContent -Encoding UTF8

if ($manifestContent -match "PLACEHOLDER-ReplaceWith") {
    Write-Warning "AppxManifest.xml still has placeholder Identity Name/Publisher - this package is NOT ready for Store submission (see installer\msix\AppxManifest.xml)."
}

# --- Pack ---
New-Item -ItemType Directory -Force -Path (Join-Path $installerDir "Output") | Out-Null
$msixOutput = Join-Path $installerDir "Output\KanbanTaskBoard-$version.msix"

& $makeAppx pack /d $layoutDir /p $msixOutput /o
if ($LASTEXITCODE -ne 0) { throw "makeappx pack failed" }

Write-Output "`nDone. MSIX package: $msixOutput"
Write-Output ""
Write-Output "This package is unsigned. To sideload-test it locally before Store submission, sign it"
Write-Output "with a self-signed cert trusted on this machine (see comments in this script for the"
Write-Output "commands), or use Add-AppxPackage with a dev-mode-enabled machine. The Store re-signs"
Write-Output "packages during certification, so signing isn't required for the actual upload."

<#
To sign for local sideload testing (not needed for Store upload):

  $cert = New-SelfSignedCertificate -Type Custom -Subject "CN=PLACEHOLDER-ReplaceWithPartnerCenterPublisherId" `
      -KeyUsage DigitalSignature -FriendlyName "Kanban Task Board Dev Cert" `
      -CertStoreLocation "Cert:\CurrentUser\My" `
      -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")
  Export-Certificate -Cert $cert -FilePath "$installerDir\Output\KanbanTaskBoard-dev.cer"
  Import-Certificate -FilePath "$installerDir\Output\KanbanTaskBoard-dev.cer" -CertStoreLocation "Cert:\LocalMachine\TrustedPeople"
  & $signtool sign /fd SHA256 /a /s My /n "PLACEHOLDER-ReplaceWithPartnerCenterPublisherId" $msixOutput
  Add-AppxPackage -Path $msixOutput
#>
