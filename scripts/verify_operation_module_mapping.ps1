[CmdletBinding()]
param(
    [string]$Root = "",
    [string]$DumpbinPath = "",
    [string]$DllPath = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($Root)) {
    $scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
    $Root = (Resolve-Path (Join-Path $scriptDirectory "..")).Path
}

$defPath = Join-Path $Root "HZCYKJTHardWare.def"
$loggerPath = Join-Path $Root "src\logger.cpp"

$moduleSdkLifecycle = "SDK" + [char]0x751F + [char]0x547D + [char]0x5468 + [char]0x671F
$moduleTerminalSwitch = [char]0x7EC8 + [char]0x7AEF + [char]0x5207 + [char]0x6362
$moduleProcessControl = [char]0x6D41 + [char]0x7A0B + [char]0x63A7 + [char]0x5236
$modulePreview = [char]0x9884 + [char]0x89C8
$modulePlateCapture = [char]0x8F66 + [char]0x724C + [char]0x6293 + [char]0x5E27
$moduleFaceCapture = [char]0x4EBA + [char]0x8138 + [char]0x6293 + [char]0x62CD
$moduleFingerprintCapture = [char]0x6307 + [char]0x7EB9 + [char]0x6293 + [char]0x62CD
$moduleIrisCapture = [char]0x8679 + [char]0x819C + [char]0x6293 + [char]0x62CD
$moduleDocument = [char]0x8BC1 + [char]0x4EF6 + [char]0x8BC6 + [char]0x522B
$moduleNfc = "NFC" + [char]0x8BFB + [char]0x5361
$moduleAuthorization = [char]0x6388 + [char]0x6743
$moduleTerminalCallback = [char]0x7EC8 + [char]0x7AEF + [char]0x56DE + [char]0x8C03

$expected = @(
    @{ Operation = "HZCYKJTHardWare_InitSdk"; Module = $moduleSdkLifecycle },
    @{ Operation = "HZCYKJTHardWare_ReleaseSdk"; Module = $moduleSdkLifecycle },
    @{ Operation = "HZCYKJTHardWare_SwitchTerminal"; Module = $moduleTerminalSwitch },
    @{ Operation = "HZCYKJTHardWare_StartProcess"; Module = $moduleProcessControl },
    @{ Operation = "HZCYKJTHardWare_EndProcess"; Module = $moduleProcessControl },
    @{ Operation = "HZCYKJTHardWare_StartCameraPreview"; Module = $modulePreview },
    @{ Operation = "HZCYKJTHardWare_StopCameraPreview"; Module = $modulePreview },
    @{ Operation = "HZCYKJTHardWare_StartFingerprintPreview"; Module = $modulePreview },
    @{ Operation = "HZCYKJTHardWare_StopFingerprintPreview"; Module = $modulePreview },
    @{ Operation = "HZCYKJTHardWare_StartIrisPreview"; Module = $modulePreview },
    @{ Operation = "HZCYKJTHardWare_StopIrisPreview"; Module = $modulePreview },
    @{ Operation = "HZCYKJTHardWare_StartPlatePreviewCJ"; Module = $modulePreview },
    @{ Operation = "HZCYKJTHardWare_StopPlatePreviewCJ"; Module = $modulePreview },
    @{ Operation = "HZCYKJTHardWare_StartPlatePreviewRJ2"; Module = $modulePreview },
    @{ Operation = "HZCYKJTHardWare_StopPlatePreviewRJ2"; Module = $modulePreview },
    @{ Operation = "HZCYKJTHardWare_StartPlatePreviewRJ3"; Module = $modulePreview },
    @{ Operation = "HZCYKJTHardWare_StopPlatePreviewRJ3"; Module = $modulePreview },
    @{ Operation = "HZCYKJTHardWare_SaveLatestPlateFrame"; Module = $modulePlateCapture },
    @{ Operation = "HZCYKJTHardWare_CaptureCameraImage"; Module = $moduleFaceCapture },
    @{ Operation = "HZCYKJTHardWare_CaptureFingerprintImage"; Module = $moduleFingerprintCapture },
    @{ Operation = "HZCYKJTHardWare_CaptureIrisImage"; Module = $moduleIrisCapture },
    @{ Operation = "HZCYKJTHardWare_RequestOCR"; Module = $moduleDocument },
    @{ Operation = "HZCYKJTHardWare_RequestNfcCard"; Module = $moduleNfc },
    @{ Operation = "HZCYKJTHardWare_RequestAuthorize"; Module = $moduleAuthorization },
    @{ Operation = "HZCYKJTHardWare_RegisterEventCallback"; Module = $moduleTerminalCallback }
)

function Assert-Equal([string]$Actual, [string]$Expected, [string]$Message) {
    if ($Actual -ne $Expected) {
        throw "$($Message): actual=$Actual, expected=$Expected"
    }
}

if (-not (Test-Path -LiteralPath $defPath)) { throw "Missing .def: $defPath" }
if (-not (Test-Path -LiteralPath $loggerPath)) { throw "Missing Native logger.cpp: $loggerPath" }

$defOperations = @(
    Get-Content -Encoding UTF8 -LiteralPath $defPath | ForEach-Object {
        if ($_ -match '^\s+(HZCYKJTHardWare_[A-Za-z0-9]+)\s*$') { $Matches[1] }
    }
)
$sourceEntries = @(
    Get-Content -Encoding UTF8 -LiteralPath $loggerPath | ForEach-Object {
        if ($_ -match '^\s*\{"(HZCYKJTHardWare_[A-Za-z0-9]+)",\s*"([^"]+)"\},?\s*$') {
            [PSCustomObject]@{ Operation = $Matches[1]; Module = $Matches[2] }
        }
    }
)

Assert-Equal $defOperations.Count $expected.Count ".def export count mismatch"
Assert-Equal $sourceEntries.Count $expected.Count "Native mapping count mismatch"

$expectedByOperation = @{}
foreach ($entry in $expected) {
    if ($expectedByOperation.ContainsKey($entry.Operation)) {
        throw "Duplicate operation in table-driven expectation: $($entry.Operation)"
    }
    $expectedByOperation[$entry.Operation] = $entry.Module
}

$defSet = @{}
foreach ($operation in $defOperations) {
    if ($defSet.ContainsKey($operation)) { throw "Duplicate export in .def: $operation" }
    $defSet[$operation] = $true
    if (-not $expectedByOperation.ContainsKey($operation)) {
        throw "Export is not covered by the expected mapping: $operation"
    }
}

$sourceByOperation = @{}
foreach ($entry in $sourceEntries) {
    if ($sourceByOperation.ContainsKey($entry.Operation)) {
        throw "Duplicate operation in Native mapping: $($entry.Operation)"
    }
    $sourceByOperation[$entry.Operation] = $entry.Module
    if (-not $defSet.ContainsKey($entry.Operation)) {
        throw "Native mapping contains operation not present in .def: $($entry.Operation)"
    }
    Assert-Equal $entry.Module $expectedByOperation[$entry.Operation] `
        "Operation mapping mismatch: $($entry.Operation)"
}

foreach ($operation in $defOperations) {
    if (-not $sourceByOperation.ContainsKey($operation)) {
        throw "Native mapping is missing .def export: $operation"
    }
}

if (($DumpbinPath -and -not $DllPath) -or ($DllPath -and -not $DumpbinPath)) {
    throw "DumpbinPath and DllPath must be provided together"
}

if ($DumpbinPath) {
    if (-not (Test-Path -LiteralPath $DumpbinPath)) { throw "Missing dumpbin: $DumpbinPath" }
    if (-not (Test-Path -LiteralPath $DllPath)) { throw "Missing DLL: $DllPath" }

    $dumpOutput = (& $DumpbinPath /exports $DllPath 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw "dumpbin failed: $DllPath" }
    $dumpOperations = @(
        $dumpOutput -split "`r?`n" | ForEach-Object {
            if ($_ -match '^\s*[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+[0-9A-Fa-f]+\s+(\S+)(?:\s*=.*)?\s*$') {
                $symbol = $Matches[1] -replace '^_', '' -replace '@\d+$', ''
                if ($symbol -match '^HZCYKJTHardWare_') { $symbol }
            }
        } | Sort-Object -Unique
    )
    Assert-Equal $dumpOperations.Count $defOperations.Count "dumpbin export count mismatch"
    foreach ($operation in $defOperations) {
        if ($dumpOperations -notcontains $operation) {
            throw "dumpbin is missing export: $operation"
        }
    }
}

$dumpbinNote = ""
if ($DumpbinPath) { $dumpbinNote = ", dumpbin export set checked" }
Write-Output ("Operation->Module mapping passed: " + $expected.Count + " exports" + $dumpbinNote)
