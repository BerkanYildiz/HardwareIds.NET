# Runs the test suite and reports every failed test as an error annotation, so failures are readable from the run
# summary (and the public API) without opening the logs. The script exits with the exit code of dotnet test.

param(
    [string] $Configuration = 'Release'
)

function ConvertTo-AnnotationText([string] $InText, [switch] $InProperty)
{
    $Result = $InText.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')

    if ($InProperty)
    {
        $Result = $Result.Replace(':', '%3A').Replace(',', '%2C')
    }

    return $Result
}

$OutputFile = Join-Path ([System.IO.Path]::GetTempPath()) 'dotnet-test-output.txt'

dotnet test --configuration $Configuration --no-build | Tee-Object -FilePath $OutputFile
$ExitCode = $LASTEXITCODE

if ($ExitCode -ne 0)
{
    $Lines = Get-Content $OutputFile
    $Reported = 0

    for ($I = 0; $I -lt $Lines.Count; $I++)
    {
        if ($Lines[$I] -notmatch '^\s*failed (.+?)(\s+\([\d.]+m?s\))?\s*$')
        {
            continue
        }

        $Name = $Matches[1]
        $Runtime = ''
        $Details = @()

        for ($J = $I + 1; $J -lt $Lines.Count -and $Details.Count -lt 12; $J++)
        {
            $Line = $Lines[$J].Trim()

            if ($Line -eq '' -or $Line -match '^(failed|passed|skipped) ' -or $Line -match '^(Stack Trace:|at )')
            {
                break
            }

            if ($Line -match '^from .*\((net[^|)]*)')
            {
                $Runtime = " [$($Matches[1])]"
                continue
            }

            $Details += $Line
        }

        Write-Host "::error title=$(ConvertTo-AnnotationText "$Name$Runtime" -InProperty)::$(ConvertTo-AnnotationText ($Details -join "`n"))"
        $Reported++
    }

    if ($Reported -eq 0)
    {
        Write-Host "::error title=dotnet test::The test run failed without a failed test (exit code $ExitCode); see the log."
    }
}

exit $ExitCode
