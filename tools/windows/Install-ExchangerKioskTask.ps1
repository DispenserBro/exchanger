[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [string]$TaskName = 'Exchanger Kiosk',

    [ValidateRange(1, 300)]
    [int]$RestartDelaySeconds = 5
)

$ErrorActionPreference = 'Stop'
$resolvedExecutable = [System.IO.Path]::GetFullPath($ExecutablePath)
if (-not (Test-Path -LiteralPath $resolvedExecutable -PathType Leaf)) {
    throw 'Не найден exe-файл Exchanger.'
}

$watchdogPath = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'Exchanger.Watchdog.ps1'))
if (-not (Test-Path -LiteralPath $watchdogPath -PathType Leaf)) {
    throw 'Не найден Exchanger.Watchdog.ps1.'
}

$quotedWatchdog = '"{0}"' -f $watchdogPath.Replace('"', '""')
$quotedExecutable = '"{0}"' -f $resolvedExecutable.Replace('"', '""')
$arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File $quotedWatchdog -ExecutablePath $quotedExecutable -RestartDelaySeconds $RestartDelaySeconds"
$action = New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arguments
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:USERNAME
$settings = New-ScheduledTaskSettingsSet `
    -ExecutionTimeLimit ([TimeSpan]::Zero) `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 1) `
    -AllowStartIfOnBatteries `
    -DontStopIfGoingOnBatteries `
    -MultipleInstances IgnoreNew
$principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive -RunLevel Limited

if ($PSCmdlet.ShouldProcess($TaskName, 'Создать задачу автозапуска Exchanger')) {
    Register-ScheduledTask `
        -TaskName $TaskName `
        -Action $action `
        -Trigger $trigger `
        -Settings $settings `
        -Principal $principal `
        -Description 'Автозапуск и перезапуск киоск-приложения Exchanger.' `
        -Force | Out-Null
    Write-Output "Задача '$TaskName' установлена для пользователя $env:USERNAME."
}
