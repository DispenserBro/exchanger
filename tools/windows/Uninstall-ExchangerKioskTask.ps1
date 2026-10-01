[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$TaskName = 'Exchanger Kiosk'
)

$ErrorActionPreference = 'Stop'
$task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($null -eq $task) {
    Write-Output "Задача '$TaskName' не найдена."
    return
}

if ($PSCmdlet.ShouldProcess($TaskName, 'Удалить задачу автозапуска Exchanger')) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
    Write-Output "Задача '$TaskName' удалена."
}
