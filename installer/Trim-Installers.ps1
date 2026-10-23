# Keeps only the newest installers in installer\Output (5 by default, counted per channel) and MOVES the
# older ones to a folder under %TEMP%, outside OneDrive. Nothing is deleted. Called at the end of
# build-installers.ps1, so every build, including the scheduled ones, leaves the folder tidy.
#
# Run by hand:  powershell -File installer\Trim-Installers.ps1 [-Keep 5]
param([int]$Keep = 5)

$output = Join-Path $PSScriptRoot "Output"
if (-not (Test-Path $output)) { return }
$holding = Join-Path $env:TEMP "KanbanInstallers-old"

# "Kanban Task Board-Setup-0.149.0.exe" -> channel "Kanban Task Board", version 0.149.0. The Test channel's
# "Kanban Task Board (Test)" is counted separately. Sorted by version, not by date.
$installers = Get-ChildItem -Path $output -Filter "*-Setup-*.exe" -File | ForEach-Object {
    if ($_.Name -match '^(?<channel>.+)-Setup-(?<version>\d+(\.\d+)*)\.exe$') {
        [pscustomobject]@{ File = $_; Channel = $Matches.channel; Version = [version]$Matches.version }
    }
}

$moved = 0
foreach ($group in ($installers | Group-Object Channel)) {
    $old = $group.Group | Sort-Object Version -Descending | Select-Object -Skip $Keep
    foreach ($item in $old) {
        New-Item -ItemType Directory -Force -Path $holding | Out-Null
        $target = Join-Path $holding $item.File.Name
        if (Test-Path $target) { $target = Join-Path $holding ("{0:yyyyMMdd-HHmmss}-{1}" -f (Get-Date), $item.File.Name) }
        Move-Item -LiteralPath $item.File.FullName -Destination $target
        Write-Output "Moved older installer out of Output: $($item.File.Name)"
        $moved++
    }
}
if ($moved -gt 0) { Write-Output "Kept the newest $Keep per channel. $moved moved to $holding" }
