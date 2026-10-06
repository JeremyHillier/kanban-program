# Renders CHANGELOG.md into the plain-text list the installer shows on an update.
#
# The installer shows what changed between the version already on the machine and the one being
# installed, so it carries the whole changelog, read the way the app's own What's New screen reads it
# (Services/ReleaseNotes.cs): for each version, its "> - " summary lines; a version written before
# summaries existed (before 0.116.5) has none, so its "- " lines are shown instead.
#
# Output format, one version after another (the installer's [Code] reads this, nothing else does):
#   #0.127.1                      a marker line: the version that follows
#   0.127.1  (2026-10-04)         the heading as shown
#   • bullet                      one line per bullet
#                                 a blank line between versions
# Newest first, as in the changelog; the installer relies on that order (see WhatsNewSince).
# Written as UTF-8 with a BOM, which is how Inno Setup's LoadStringsFromFile recognises UTF-8.

param(
    [Parameter(Mandatory = $true)][string]$ChangelogPath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = "Stop"

function Clean-Markdown([string]$text) {
    # **bold**, `code`, and a single * wrapping a word: none are used today, but a stray one would
    # otherwise show up literally in Setup.
    $t = $text.Replace('**', '').Replace([string][char]0x60, '')
    $t = [regex]::Replace($t, '(?<=\S)\*|\*(?=\S)', '')
    return $t.Trim()
}

$headingPattern = '^##\s+(?<version>\S+)\s*[' + [char]0x2014 + [char]0x2013 + '-]\s*(?<date>.+?)\s*$'
$bullet = [string][char]0x2022

$lines = [IO.File]::ReadAllLines($ChangelogPath, [Text.Encoding]::UTF8)
$out = New-Object System.Collections.Generic.List[string]
$summary = New-Object System.Collections.Generic.List[string]
$detail = New-Object System.Collections.Generic.List[string]
$inVersion = $false

function Finish-Version {
    $items = if ($summary.Count -gt 0) { $summary } else { $detail }
    foreach ($item in $items) { $out.Add("$bullet $(Clean-Markdown $item)") }
    $out.Add("")
    $summary.Clear()
    $detail.Clear()
}

foreach ($raw in $lines) {
    $line = $raw.Trim()

    $heading = [regex]::Match($line, $headingPattern)
    if ($heading.Success) {
        if ($inVersion) { Finish-Version }
        $inVersion = $true
        $version = $heading.Groups["version"].Value
        $out.Add("#" + $version)
        $out.Add("$version  ($($heading.Groups['date'].Value))")
        continue
    }

    # Bullets before the first heading belong to the file's introduction, not a release.
    if (-not $inVersion) { continue }
    if ($line.StartsWith("> - ")) { $summary.Add($line.Substring(4).Trim()) }
    elseif ($line.StartsWith("- ")) { $detail.Add($line.Substring(2).Trim()) }
}
if ($inVersion) { Finish-Version }

$directory = Split-Path $OutputPath -Parent
if ($directory) { New-Item -ItemType Directory -Force -Path $directory | Out-Null }
[IO.File]::WriteAllLines($OutputPath, $out, (New-Object System.Text.UTF8Encoding $true))
Write-Output "What's new: $($out.Where({ $_.StartsWith('#') }).Count) versions rendered to $OutputPath"
