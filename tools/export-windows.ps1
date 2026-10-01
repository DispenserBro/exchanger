param(
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [string]$GodotPath = "D:\Applications\Godot\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64.exe"
)

$ErrorActionPreference = "Stop"

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$stagingRoot = Join-Path $projectRoot "artifacts\export-staging"
$windowsRoot = Join-Path $projectRoot "artifacts\windows"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$stagePath = Join-Path $stagingRoot "run-$timestamp-$PID"
$isRelease = $Configuration -eq "Release"
$presetName = if ($isRelease) { "Windows x64 Release" } else { "Windows x64 Debug" }
$exportFlag = if ($isRelease) { "--export-release" } else { "--export-debug" }
$outputDirectory = Join-Path $windowsRoot "Exchanger-0.6.0-$Configuration-$timestamp"
$outputFileName = if ($isRelease) { "Exchanger.exe" } else { "Exchanger.Debug.exe" }
$outputExe = Join-Path $outputDirectory $outputFileName

function Assert-SafeWorkspacePath([string]$Path, [string]$ExpectedLeaf) {
    $absolute = [System.IO.Path]::GetFullPath($Path)
    $workspacePrefix = $projectRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $absolute.StartsWith($workspacePrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path $absolute -Leaf) -ne $ExpectedLeaf) {
        throw "Unsafe staging path: $absolute"
    }
}

function Assert-SafeStagingRunPath([string]$Path) {
    $absolute = [System.IO.Path]::GetFullPath($Path)
    $stagingPrefix = [System.IO.Path]::GetFullPath($stagingRoot).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $absolute.StartsWith($stagingPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        (Split-Path $absolute -Leaf) -notlike "run-*") {
        throw "Unsafe staging run path: $absolute"
    }
}

if (-not (Test-Path -LiteralPath $GodotPath -PathType Leaf)) {
    throw "Godot 4.7.1 Mono was not found: $GodotPath"
}

if ((Split-Path $GodotPath -Leaf) -like "godot-ai*") {
    throw "The official Godot Mono executable is required for export."
}

Assert-SafeWorkspacePath $stagingRoot "export-staging"
Assert-SafeStagingRunPath $stagePath
New-Item -ItemType Directory -Path $stagePath | Out-Null
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
$exportSucceeded = $false

try {
    $excludedDirectories = @(
        ".git", ".godot", ".agents", ".vscode", "addons", "artifacts",
        "docs", "references", "tests", "tools"
    )
    $robocopyArguments = @($projectRoot, $stagePath, "/E", "/NFL", "/NDL", "/NJH", "/NJS", "/NP", "/XD") +
        ($excludedDirectories | ForEach-Object { Join-Path $projectRoot $_ })
    & robocopy @robocopyArguments | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "Could not prepare the staging copy (robocopy code $LASTEXITCODE)."
    }

    # Keep the production copy free of editor-only addons, but explicitly bring
    # the Native Video runtime extension back. Excluding the whole addons
    # directory without this copy makes MP4/MOV/M4V unavailable in the export.
    $nativeVideoSource = Join-Path $projectRoot "addons\native_video"
    $nativeVideoStageRoot = Join-Path $stagePath "addons"
    if (-not (Test-Path -LiteralPath $nativeVideoSource -PathType Container)) {
        throw "Native Video runtime addon is missing: $nativeVideoSource"
    }

    New-Item -ItemType Directory -Path $nativeVideoStageRoot -Force | Out-Null
    # Godot can leave hot-reload backups such as ~native_video...dll~*.TMP
    # beside the real extension. They are not runtime assets and can make a
    # headless staging import try to load a transient, locked library.
    & robocopy $nativeVideoSource (Join-Path $nativeVideoStageRoot "native_video") "/E" "/NFL" "/NDL" "/NJH" "/NJS" "/NP" "/XF" "~*" | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "Could not copy the Native Video runtime addon (robocopy code $LASTEXITCODE)."
    }

    $nativeVideoStageDirectory = Join-Path $nativeVideoStageRoot "native_video"
    $nativeVideoLibrary = if ($isRelease) {
        "native_video.windows.release.x86_64.dll"
    }
    else {
        "native_video.windows.debug.x86_64.dll"
    }
    $requiredStagedNativeVideoFiles = @(
        (Join-Path $nativeVideoStageDirectory "native_video.gdextension"),
        (Join-Path $nativeVideoStageDirectory $nativeVideoLibrary)
    )
    $missingStagedNativeVideoFiles = @($requiredStagedNativeVideoFiles | Where-Object {
        -not (Test-Path -LiteralPath $_ -PathType Leaf) -or (Get-Item -LiteralPath $_).Length -le 0
    })
    if ($missingStagedNativeVideoFiles.Count -gt 0) {
        throw "Native Video runtime addon is incomplete in staging: $($missingStagedNativeVideoFiles -join ', ')"
    }

    $stagedProject = Join-Path $stagePath "project.godot"
    $projectText = Get-Content -LiteralPath $stagedProject -Raw
    $projectText = $projectText -replace '(?m)^GodotxToast=.*\r?\n', ''
    $projectText = $projectText -replace '(?m)^_mcp_game_helper=.*\r?\n', ''
    $projectText = $projectText -replace '(?ms)\r?\n\[editor_plugins\].*?(?=\r?\n\[)', ''
    Set-Content -LiteralPath $stagedProject -Value $projectText -Encoding UTF8

    $importProcess = Start-Process -FilePath $GodotPath -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
        "--headless", "--path", $stagePath, "--editor", "--quit-after", "30"
    )
    if ($importProcess.ExitCode -ne 0) {
        throw "Godot could not import the production staging project."
    }

    $exportProcess = Start-Process -FilePath $GodotPath -WindowStyle Hidden -Wait -PassThru -ArgumentList @(
        "--headless", "--path", $stagePath, $exportFlag, ('"' + $presetName + '"'), $outputExe
    )
    if ($exportProcess.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $outputExe)) {
        throw "Windows $Configuration export was not created. Check the Godot 4.7.1 Mono export templates."
    }

    # Godot embeds .gdextension into the PCK but extracts its selected native
    # library next to the executable. Verify both source inputs before export
    # and the actual selected library in the resulting Windows package.
    $nativeVideoOutputLibrary = Join-Path $outputDirectory $nativeVideoLibrary
    if (-not (Test-Path -LiteralPath $nativeVideoOutputLibrary -PathType Leaf) -or
        (Get-Item -LiteralPath $nativeVideoOutputLibrary).Length -le 0) {
        throw "Native Video runtime library was not exported: $nativeVideoOutputLibrary"
    }

    $exportSucceeded = $true
    Write-Host "Windows x64 $Configuration export: $outputDirectory"
}
finally {
    Assert-SafeStagingRunPath $stagePath
    if (Test-Path -LiteralPath $stagePath) {
        Remove-Item -LiteralPath $stagePath -Recurse -Force
    }

    if (-not $exportSucceeded -and (Test-Path -LiteralPath $outputDirectory)) {
        Assert-SafeWorkspacePath $outputDirectory (Split-Path $outputDirectory -Leaf)
        Remove-Item -LiteralPath $outputDirectory -Recurse -Force
    }
}
