# Keeps only the newest installers in installer\Output (5 by default, counted per channel) and sends the
# older ones to the Recycle Bin, so one can still be fetched back. Called at the end of
# build-installers.ps1, so every build, including the scheduled ones, leaves the folder tidy.
#
# Run by hand:  powershell -File installer\Trim-Installers.ps1 [-Keep 5]
# -Path is for testing the script against a scratch folder.
param([int]$Keep = 5, [string]$Path = (Join-Path $PSScriptRoot "Output"))

if (-not (Test-Path $Path)) { return }
Add-Type -AssemblyName Microsoft.VisualBasic

# "Kanban Task Board-Setup-0.149.0.exe" -> channel "Kanban Task Board", version 0.149.0. The Test channel's
# "Kanban Task Board (Test)" is counted separately. Sorted by version, not by date.
$installers = Get-ChildItem -Path $Path -Filter "*-Setup-*.exe" -File | ForEach-Object {
    if ($_.Name -match '^(?<channel>.+)-Setup-(?<version>\d+(\.\d+)*)\.exe$') {
        [pscustomobject]@{ File = $_; Channel = $Matches.channel; Version = [version]$Matches.version }
    }
}

$removed = 0
foreach ($group in ($installers | Group-Object Channel)) {
    $old = $group.Group | Sort-Object Version -Descending | Select-Object -Skip $Keep
    foreach ($item in $old) {
        [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteFile($item.File.FullName,
            [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs,
            [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin)
        Write-Output "Sent older installer to the Recycle Bin: $($item.File.Name)"
        $removed++
    }
}
if ($removed -gt 0) { Write-Output "Kept the newest $Keep per channel. $removed sent to the Recycle Bin." }
