param(
    [Parameter(Mandatory = $true)]
    [string]$Port,

    [ValidateSet("Safe", "Probe", "Interactive", "Payment", "Dispense", "Cable")]
    [string]$Group = "Safe",

    [int]$ReconnectCycles = 3,
    [int]$StockInput = 1,
    [bool]$StockLowWhenHigh = $false,
    [ValidateSet("", "Enough", "Low")]
    [string]$ExpectedStock = "",
    [int]$CoinRubles = 0,
    [int]$BillRubles = 0,
    [int]$CardRubles = 0,
    [int]$DispenseCount = 0,
    [int]$InteractionTimeoutSeconds = 60,
    [switch]$ConfirmInteractive,
    [switch]$ConfirmRealPayment,
    [switch]$ConfirmDispense,
    [switch]$ConfirmCable
)

$ErrorActionPreference = "Stop"
$env:EXCHANGER_BOARD_TESTS = "1"
$env:EXCHANGER_BOARD_PORT = $Port
$env:EXCHANGER_BOARD_RECONNECT_CYCLES = $ReconnectCycles.ToString()
$env:EXCHANGER_BOARD_STOCK_INPUT = $StockInput.ToString()
$env:EXCHANGER_BOARD_STOCK_LOW_WHEN_HIGH = $StockLowWhenHigh.ToString()
$env:EXCHANGER_BOARD_INTERACTION_TIMEOUT_SECONDS = $InteractionTimeoutSeconds.ToString()

if ($ExpectedStock) { $env:EXCHANGER_BOARD_EXPECT_STOCK = $ExpectedStock }
if ($CoinRubles -gt 0) { $env:EXCHANGER_BOARD_COIN_RUBLES = $CoinRubles.ToString() }
if ($BillRubles -gt 0) { $env:EXCHANGER_BOARD_BILL_RUBLES = $BillRubles.ToString() }
if ($CardRubles -gt 0) { $env:EXCHANGER_BOARD_CARD_RUBLES = $CardRubles.ToString() }
if ($DispenseCount -gt 0) { $env:EXCHANGER_BOARD_DISPENSE_COUNT = $DispenseCount.ToString() }
if ($ConfirmInteractive) { $env:EXCHANGER_BOARD_INTERACTIVE_CONFIRM = "I_AM_READY" }
if ($ConfirmRealPayment) { $env:EXCHANGER_BOARD_PAYMENT_CONFIRM = "I_ACCEPT_REAL_PAYMENT" }
if ($ConfirmDispense) { $env:EXCHANGER_BOARD_DISPENSE_CONFIRM = "I_ACCEPT_TOKEN_DISPENSE" }
if ($ConfirmCable) { $env:EXCHANGER_BOARD_CABLE_CONFIRM = "I_WILL_UNPLUG_USB" }

$category = "Hardware$Group"
$project = Join-Path $PSScriptRoot "Exchanger.HardwareTests.csproj"
$repositoryRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$resultsDirectory = Join-Path $repositoryRoot "artifacts\board-tests"
New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null
$reportName = "board-$($Group.ToLowerInvariant())-$(Get-Date -Format 'yyyyMMdd-HHmmss').trx"

dotnet test $project `
    --filter "TestCategory=$category" `
    --logger "trx;LogFileName=$reportName" `
    --results-directory $resultsDirectory

exit $LASTEXITCODE
