[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [string[]]$ApplicationArguments = @(),

    [ValidateRange(0.0167, 168)]
    [double]$DurationHours = 24,

    [ValidateRange(5, 3600)]
    [int]$SampleIntervalSeconds = 60,

    [ValidateRange(128, 16384)]
    [int]$MaximumWorkingSetMb = 2048,

    [string]$OutputDirectory = ""
)

$ErrorActionPreference = 'Stop'
$resolvedExecutable = [System.IO.Path]::GetFullPath($ExecutablePath)
if (-not (Test-Path -LiteralPath $resolvedExecutable -PathType Leaf)) {
    throw 'Не найден исполняемый файл для soak-теста.'
}

$resolvedOutputDirectory = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path (Get-Location) 'artifacts\soak'
} else {
    [System.IO.Path]::GetFullPath($OutputDirectory)
}
New-Item -ItemType Directory -Path $resolvedOutputDirectory -Force | Out-Null
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$csvPath = Join-Path $resolvedOutputDirectory "runtime-soak-$timestamp.csv"
$summaryPath = Join-Path $resolvedOutputDirectory "runtime-soak-$timestamp.txt"
$deadline = [DateTimeOffset]::Now.AddHours($DurationHours)
$process = $null
$peakWorkingSetMb = 0.0
$samples = 0

try {
    $workingDirectory = [System.IO.Path]::GetDirectoryName($resolvedExecutable)
    $process = Start-Process `
        -FilePath $resolvedExecutable `
        -ArgumentList $ApplicationArguments `
        -WorkingDirectory $workingDirectory `
        -WindowStyle Hidden `
        -PassThru

    'Timestamp,WorkingSetMb,PrivateMemoryMb,Responding' | Set-Content -LiteralPath $csvPath -Encoding UTF8
    while ([DateTimeOffset]::Now -lt $deadline) {
        Start-Sleep -Seconds $SampleIntervalSeconds
        $process.Refresh()
        if ($process.HasExited) {
            throw "Процесс завершился до окончания soak-теста: exit_code=$($process.ExitCode)."
        }

        $workingSetMb = [Math]::Round($process.WorkingSet64 / 1MB, 2)
        $privateMemoryMb = [Math]::Round($process.PrivateMemorySize64 / 1MB, 2)
        $peakWorkingSetMb = [Math]::Max($peakWorkingSetMb, $workingSetMb)
        $samples++
        [string]::Format(
            [Globalization.CultureInfo]::InvariantCulture,
            '{0:o},{1:F2},{2:F2},{3}',
            [DateTimeOffset]::Now,
            $workingSetMb,
            $privateMemoryMb,
            $process.Responding) |
            Add-Content -LiteralPath $csvPath -Encoding UTF8

        if ($workingSetMb -gt $MaximumWorkingSetMb) {
            throw "Working set превысил безопасный предел $MaximumWorkingSetMb МБ."
        }
    }

    @(
        'Result=Passed'
        "DurationHours=$DurationHours"
        "Samples=$samples"
        'PeakWorkingSetMb=' + $peakWorkingSetMb.ToString('F2', [Globalization.CultureInfo]::InvariantCulture)
        "ProcessId=$($process.Id)"
    ) | Set-Content -LiteralPath $summaryPath -Encoding UTF8
} catch {
    @(
        'Result=Failed'
        "DurationHours=$DurationHours"
        "Samples=$samples"
        'PeakWorkingSetMb=' + $peakWorkingSetMb.ToString('F2', [Globalization.CultureInfo]::InvariantCulture)
        "ErrorType=$($_.Exception.GetType().Name)"
    ) | Set-Content -LiteralPath $summaryPath -Encoding UTF8
    throw
} finally {
    if ($null -ne $process -and -not $process.HasExited) {
        $null = $process.CloseMainWindow()
        if (-not $process.WaitForExit(10000)) {
            Stop-Process -Id $process.Id -Force
        }
    }
}

Write-Output "Soak-тест завершён. Сводка: $summaryPath"
