# Code signing for the installer build, with Azure Artifact Signing. Dot-sourced by build-installers.ps1.
# The same file is in both the Kanban and Personal Finance repos; keep the two copies identical.
#
# Signing is off until installer\signing.json exists. That file names the signing account and the
# certificate profile, and holds no secrets (the sign-in is the build PC's own "az login"). The full
# setup is in "Code Signing Setup.html" in the Personal Finance Program folder.
#
# Once it exists, every Production build signs the program, the installer and its uninstaller, checks
# each signature, and stops rather than produce an unsigned installer. Test builds are never signed.
#
# To point at a SignTool or plug-in that is not found on its own, set SIGNTOOL_PATH or
# ARTIFACT_SIGNING_DLIB to the full path of signtool.exe or Azure.CodeSigning.Dlib.dll.

$TimestampUrl = "http://timestamp.acs.microsoft.com"

# SignTool older than this cannot load the Artifact Signing plug-in (the SDK 20348 build fails).
$MinimumSignToolVersion = [version]"10.0.22621.0"

# The signing setup for this PC, or $null when signing is switched off (no installer\signing.json).
# Throws, with what to do, when it is switched on but a tool or the Azure sign-in is missing.
function Get-SigningSetup {
    param([Parameter(Mandatory)][string]$InstallerDir)

    $metadata = Join-Path $InstallerDir "signing.json"
    if (-not (Test-Path $metadata)) { return $null }

    try { $null = Get-Content $metadata -Raw | ConvertFrom-Json }
    catch { throw "installer\signing.json is not valid JSON: $($_.Exception.Message)" }

    $dlib = Find-SigningPlugin
    if (-not $dlib) {
        throw "Signing is on (installer\signing.json exists) but the Artifact Signing plug-in was not found. Install it with: winget install -e --id Microsoft.Azure.ArtifactSigningClientTools"
    }
    $signtool = Find-SignTool $dlib
    if (-not $signtool) {
        throw "Signing is on but signtool.exe $MinimumSignToolVersion or later was not found. Reinstall the client tools with: winget install -e --id Microsoft.Azure.ArtifactSigningClientTools"
    }
    if (-not (Test-AzureSignIn)) {
        throw "Signing is on but this PC is not signed in to Azure. Run: az login, then build again."
    }

    [pscustomobject]@{ SignTool = $signtool; Dlib = $dlib; Metadata = (Resolve-Path $metadata).Path }
}

function Find-SigningPlugin {
    if ($env:ARTIFACT_SIGNING_DLIB -and (Test-Path $env:ARTIFACT_SIGNING_DLIB)) { return $env:ARTIFACT_SIGNING_DLIB }
    $roots = @("$env:LOCALAPPDATA\Microsoft", "$env:ProgramFiles\Microsoft", "${env:ProgramFiles(x86)}\Microsoft", $env:ProgramFiles, ${env:ProgramFiles(x86)}) |
        Where-Object { $_ -and (Test-Path $_) }
    $found = foreach ($root in $roots) {
        Get-ChildItem $root -Directory -Filter "*Signing*" -ErrorAction SilentlyContinue |
            ForEach-Object { Get-ChildItem $_.FullName -Recurse -Filter "Azure.CodeSigning.Dlib.dll" -ErrorAction SilentlyContinue }
    }
    # The x64 build, to match the x64 SignTool, and the newest if there are several.
    ($found | Sort-Object @{ e = { $_.FullName -match '\\x64\\' }; Descending = $true }, @{ e = { $_.LastWriteTime }; Descending = $true } |
        Select-Object -First 1).FullName
}

function Find-SignTool {
    param([string]$Dlib)
    if ($env:SIGNTOOL_PATH -and (Test-Path $env:SIGNTOOL_PATH)) { return $env:SIGNTOOL_PATH }
    $candidates = @()
    # The client tools package ships its own SignTool; prefer it, then the newest Windows SDK.
    if ($Dlib) { $candidates += Get-ChildItem (Split-Path (Split-Path $Dlib -Parent) -Parent) -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue }
    $kits = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $kits) { $candidates += Get-ChildItem $kits -Recurse -Filter "signtool.exe" -ErrorAction SilentlyContinue | Where-Object { $_.FullName -match '\\x64\\' } }
    $onPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($onPath) { $candidates += Get-Item $onPath.Source }

    $best = $candidates |
        Select-Object @{ n = "Path"; e = { $_.FullName } }, @{ n = "Version"; e = { Get-FileVersion $_.FullName } } |
        Where-Object { $_.Version -and $_.Version -ge $MinimumSignToolVersion } |
        Sort-Object Version -Descending | Select-Object -First 1
    $best.Path
}

function Get-FileVersion {
    param([string]$Path)
    $info = (Get-Item $Path).VersionInfo
    try { [version]("{0}.{1}.{2}.{3}" -f $info.FileMajorPart, $info.FileMinorPart, $info.FileBuildPart, $info.FilePrivatePart) } catch { $null }
}

function Test-AzureSignIn {
    if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
        throw "Signing is on but the Azure CLI is not installed. Install it with: winget install -e --id Microsoft.AzureCLI, then run: az login"
    }
    # cmd keeps az's error text out of PowerShell, where Windows PowerShell 5.1 would stop on it.
    cmd /c "az account show --only-show-errors >nul 2>&1"
    $LASTEXITCODE -eq 0
}

# Signs each file, SHA-256 with Microsoft's timestamp. The timestamp keeps a signature valid long after
# the 3-day certificate that made it has expired.
function Invoke-CodeSigning {
    param([Parameter(Mandatory)]$Setup, [Parameter(Mandatory)][string[]]$Files)
    foreach ($file in $Files) {
        Write-Output "Signing $file"
        & $Setup.SignTool sign /v /fd SHA256 /tr $TimestampUrl /td SHA256 /dlib $Setup.Dlib /dmdf $Setup.Metadata $file
        if ($LASTEXITCODE -ne 0) {
            throw "Signing failed for $file. If it says 403 Forbidden, the Endpoint in installer\signing.json does not match the account's region. If it says not authorised or no credential, run: az login"
        }
    }
}

# Checks each file carries a valid, trusted signature.
function Test-CodeSignatures {
    param([Parameter(Mandatory)]$Setup, [Parameter(Mandatory)][string[]]$Files)
    foreach ($file in $Files) {
        & $Setup.SignTool verify /pa $file | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "The signature on $file did not verify. Do not publish this installer." }
        Write-Output "Signature verified: $(Split-Path $file -Leaf)"
    }
}

# The ISCC switch that defines the "azuresign" tool Inno Setup uses for the installer and uninstaller.
# $q is Inno's quote and $f the file it is signing, so the switch needs no quotes of its own.
function Get-InnoSignSwitch {
    param([Parameter(Mandatory)]$Setup)
    '/Sazuresign=$q' + $Setup.SignTool + '$q sign /fd SHA256 /tr ' + $TimestampUrl + ' /td SHA256 /dlib $q' + $Setup.Dlib + '$q /dmdf $q' + $Setup.Metadata + '$q $f'
}
