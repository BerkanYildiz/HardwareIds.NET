# Extracts the section of CHANGELOG.md that describes a version (from its "## [2.4.0] - 2026-11-03" heading to the next
# "## " heading) into a file: the Publish workflow packs it as the release notes of the package and uses it as the body of
# the GitHub release. The script exits with 1 when the section is missing, empty or still marked Unreleased, so a release
# cannot go out undocumented.

param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $Output
)

$ErrorActionPreference = 'Stop'

$Changelog = Get-Content (Join-Path $PSScriptRoot '../../CHANGELOG.md') -Raw
$Heading = "## [$Version]"
$Match = [regex]::Match($Changelog, "(?ms)^$([regex]::Escape($Heading))([^\r\n]*)\r?\n(.*?)(?=^## |\z)")

if (-not $Match.Success)
{
    Write-Host "::error title=CHANGELOG.md has no section for $Version::Add a '$Heading - <date>' section to CHANGELOG.md before tagging v$Version."
    exit 1
}

if ($Match.Groups[1].Value -match 'unreleased')
{
    Write-Host "::error title=CHANGELOG.md still marks $Version as unreleased::Replace 'Unreleased' with the release date in the '$Heading' heading before tagging v$Version."
    exit 1
}

$Notes = $Match.Groups[2].Value.Trim()

if ($Notes -eq '')
{
    Write-Host "::error title=The CHANGELOG.md section for $Version is empty::Describe the release under '$Heading' before tagging v$Version."
    exit 1
}

if (-not [System.IO.Path]::IsPathRooted($Output))
{
    $Output = Join-Path (Get-Location).Path $Output
}

New-Item -ItemType Directory -Force (Split-Path $Output) | Out-Null
[System.IO.File]::WriteAllText($Output, $Notes + "`n")
Write-Host "Release notes of $Version ($($Notes.Split("`n").Count) lines) written to $Output."
exit 0
