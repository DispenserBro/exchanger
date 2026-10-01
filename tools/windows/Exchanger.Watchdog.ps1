[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [string]$WorkingDirectory = "",

    [ValidateRange(1, 300)]
    [int]$RestartDelaySeconds = 5,

    [switch]$Once
)

$ErrorActionPreference = 'Stop'
$resolvedExecutable = [System.IO.Path]::GetFullPath($ExecutablePath)
if (-not [System.IO.Path]::IsPathFullyQualified($resolvedExecutable) -or -not (Test-Path -LiteralPath $resolvedExecutable -PathType Leaf)) {
    throw 'ExecutablePath должен указывать на существующий абсолютный exe-файл Exchanger.'
}

$resolvedWorkingDirectory = if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
    [System.IO.Path]::GetDirectoryName($resolvedExecutable)
} else {
    [System.IO.Path]::GetFullPath($WorkingDirectory)
}
if (-not (Test-Path -LiteralPath $resolvedWorkingDirectory -PathType Container)) {
    throw 'Рабочий каталог Exchanger не существует.'
}

$watchdogDirectory = Join-Path $env:LOCALAPPDATA 'Exchanger\watchdog'
New-Item -ItemType Directory -Path $watchdogDirectory -Force | Out-Null
$watchdogLog = Join-Path $watchdogDirectory 'watchdog.log'
$mutexName = 'Local\ExchangerWatchdog'
$createdNew = $false
$mutex = [System.Threading.Mutex]::new($true, $mutexName, [ref]$createdNew)
if (-not $createdNew) {
    throw 'Watchdog Exchanger уже запущен в этой пользовательской сессии.'
}

function Write-WatchdogLog {
    param([string]$Message)

    if ((Test-Path -LiteralPath $watchdogLog) -and (Get-Item -LiteralPath $watchdogLog).Length -ge 1MB) {
        $archive = Join-Path $watchdogDirectory 'watchdog.previous.log'
        Move-Item -LiteralPath $watchdogLog -Destination $archive -Force
    }

    $line = '{0:o} {1}' -f [DateTimeOffset]::Now, $Message
    Add-Content -LiteralPath $watchdogLog -Value $line -Encoding UTF8
}

try {
    do {
        $startedAt = [DateTimeOffset]::Now
        Write-WatchdogLog 'Запуск процесса Exchanger.'
        try {
            $process = Start-Process -FilePath $resolvedExecutable -WorkingDirectory $resolvedWorkingDirectory -PassThru -Wait
            $runtimeSeconds = ([DateTimeOffset]::Now - $startedAt).TotalSeconds
            Write-WatchdogLog ("Процесс завершён: exit_code={0}, runtime_seconds={1:N0}." -f $process.ExitCode, $runtimeSeconds)
        } catch {
            Write-WatchdogLog ("Не удалось запустить процесс: {0}." -f $_.Exception.GetType().Name)
        }

        if (-not $Once) {
            Start-Sleep -Seconds $RestartDelaySeconds
        }
    } while (-not $Once)
} finally {
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
