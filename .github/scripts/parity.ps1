# Checks that the C++ library and HardwareIds.NET describe this machine the same way: HardwareIdsSnapshot and the
# HardwareIds.NET.Testing --json mode must print the same JSON, byte for byte, in both the compact and the indented
# forms. Boot time and volatile TPM state are masked because separate processes observe different instants.
# Every difference is reported as an error annotation; the script exits with 1 when the outputs differ.

param(
    [Parameter(Mandatory)] [string] $Snapshot,
    [string] $Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'

$Testing = Join-Path $PSScriptRoot "../../HardwareIds.NET.Testing/bin/$Configuration/net10.0-windows/HardwareIds.NET.Testing.dll"

if (-not (Test-Path $Testing)) { throw "Build HardwareIds.NET.Testing first ($Testing is missing)." }
if (-not (Test-Path $Snapshot)) { throw "$Snapshot is missing." }

$Testing = (Resolve-Path $Testing).Path
$Snapshot = (Resolve-Path $Snapshot).Path

function Get-Json([string] $InFileName, [string[]] $InArguments)
{
    $Output = Join-Path ([System.IO.Path]::GetTempPath()) "parity-$([guid]::NewGuid()).json"

    try
    {
        $Parameters = @{ FilePath = $InFileName; NoNewWindow = $true; Wait = $true; PassThru = $true; RedirectStandardOutput = $Output }

        if ($InArguments.Count -gt 0)
        {
            $Parameters.ArgumentList = @($InArguments | ForEach-Object { if ($_ -match '\s') { "`"$_`"" } else { $_ } })
        }

        $Process = Start-Process @Parameters

        if ($Process.ExitCode -ne 0) { throw "$InFileName $InArguments exited with code $($Process.ExitCode)." }

        $Json = [System.IO.File]::ReadAllText($Output)
        $Json = [regex]::Replace($Json, '"last_boot_up_time": ?"[^"]*"', '"last_boot_up_time":"*"')
        $Json = [regex]::Replace($Json, '("property":\s*(\d+),\s*"value":\s*)\d+', {
            param($Match)
            if ([uint32] $Match.Groups[2].Value -ge 0x200 -and [uint32] $Match.Groups[2].Value -lt 0x300) { return $Match.Groups[1].Value + '0' }
            return $Match.Value
        })
        return [regex]::Replace($Json, '("(?:permanent_flags|startup_clear_flags|is_owner_auth_set|is_endorsement_auth_set|is_lockout_auth_set|is_in_lockout|lockout_counter|max_auth_fail|lockout_interval|lockout_recovery)":\s*)(?:\d+|true|false|null)', '${1}null')
    }
    finally
    {
        Remove-Item $Output -ErrorAction SilentlyContinue
    }
}

$Failed = $false

foreach ($Format in @('compact', 'indented'))
{
    $Extra = if ($Format -eq 'indented') { @('--indented') } else { @() }
    $Net = Get-Json 'dotnet' (@($Testing, '--json') + $Extra)
    $Cpp = Get-Json $Snapshot $Extra

    if ($Net -ceq $Cpp)
    {
        Write-Host "$Format JSON: identical ($($Net.Length) characters)."
        continue
    }

    $Failed = $true
    $Index = 0

    while ($Index -lt [Math]::Min($Net.Length, $Cpp.Length) -and $Net[$Index] -ceq $Cpp[$Index]) { $Index++ }

    $Start = [Math]::Max(0, $Index - 200)
    $NetContext = $Net.Substring($Start, [Math]::Min($Net.Length - $Start, 400))
    $CppContext = $Cpp.Substring($Start, [Math]::Min($Cpp.Length - $Start, 400))

    Write-Host "::error title=C++ and .NET snapshots differ ($Format)::The outputs differ at character $Index.%0A.NET: $($NetContext.Replace("`r", '').Replace("`n", ' '))%0AC++:  $($CppContext.Replace("`r", '').Replace("`n", ' '))"
    Write-Host ".NET ($($Net.Length) characters): $NetContext"
    Write-Host "C++  ($($Cpp.Length) characters): $CppContext"
}

if ($Failed) { exit 1 }
