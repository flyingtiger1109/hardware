param(
    [string]$Root = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = "Stop"

$defPath = Join-Path $Root "HZCYKJTHardWare.def"
$loggerPath = Join-Path $Root "src\logger.cpp"
$defText = Get-Content -LiteralPath $defPath -Raw
$loggerText = Get-Content -LiteralPath $loggerPath -Raw

$expected = @(
    @{ Operation = "HZCYKJTHardWare_InitSdk"; Module = "SDK生命周期" },
    @{ Operation = "HZCYKJTHardWare_ReleaseSdk"; Module = "SDK生命周期" },
    @{ Operation = "HZCYKJTHardWare_SwitchTerminal"; Module = "终端切换" },
    @{ Operation = "HZCYKJTHardWare_StartProcess"; Module = "流程控制" },
    @{ Operation = "HZCYKJTHardWare_EndProcess"; Module = "流程控制" },
    @{ Operation = "HZCYKJTHardWare_StartCameraPreview"; Module = "预览" },
    @{ Operation = "HZCYKJTHardWare_StopCameraPreview"; Module = "预览" },
    @{ Operation = "HZCYKJTHardWare_StartFingerprintPreview"; Module = "预览" },
    @{ Operation = "HZCYKJTHardWare_StopFingerprintPreview"; Module = "预览" },
    @{ Operation = "HZCYKJTHardWare_StartIrisPreview"; Module = "预览" },
    @{ Operation = "HZCYKJTHardWare_StopIrisPreview"; Module = "预览" },
    @{ Operation = "HZCYKJTHardWare_StartPlatePreviewCJ"; Module = "预览" },
    @{ Operation = "HZCYKJTHardWare_StopPlatePreviewCJ"; Module = "预览" },
    @{ Operation = "HZCYKJTHardWare_StartPlatePreviewRJ2"; Module = "预览" },
    @{ Operation = "HZCYKJTHardWare_StopPlatePreviewRJ2"; Module = "预览" },
    @{ Operation = "HZCYKJTHardWare_StartPlatePreviewRJ3"; Module = "预览" },
    @{ Operation = "HZCYKJTHardWare_StopPlatePreviewRJ3"; Module = "预览" },
    @{ Operation = "HZCYKJTHardWare_SaveLatestPlateFrame"; Module = "车牌抓帧" },
    @{ Operation = "HZCYKJTHardWare_CaptureCameraImage"; Module = "人脸抓拍" },
    @{ Operation = "HZCYKJTHardWare_CaptureFingerprintImage"; Module = "指纹抓拍" },
    @{ Operation = "HZCYKJTHardWare_CaptureIrisImage"; Module = "虹膜抓拍" },
    @{ Operation = "HZCYKJTHardWare_RequestOCR"; Module = "证件识别" },
    @{ Operation = "HZCYKJTHardWare_RequestNfcCard"; Module = "NFC读卡" },
    @{ Operation = "HZCYKJTHardWare_RequestAuthorize"; Module = "授权" },
    @{ Operation = "HZCYKJTHardWare_RegisterEventCallback"; Module = "终端回调" }
)

$defOperations = [regex]::Matches(
    $defText, '(?m)^\s+(HZCYKJTHardWare_[A-Za-z0-9]+)\s*$') |
    ForEach-Object { $_.Groups[1].Value }
$expectedOperations = $expected | ForEach-Object { $_.Operation }

$missingFromDef = $expectedOperations | Where-Object { $_ -notin $defOperations }
$unexpectedInDef = $defOperations | Where-Object { $_ -notin $expectedOperations }
if ($missingFromDef -or $unexpectedInDef) {
    throw "导出表与测试映射不一致：缺少=$($missingFromDef -join ',') 多余=$($unexpectedInDef -join ',')"
}

foreach ($item in $expected) {
    $operation = [regex]::Escape($item.Operation)
    $module = [regex]::Escape($item.Module)
    $pattern = '\{\s*"' + $operation + '"\s*,\s*"' + $module + '"\s*\}'
    if ($loggerText -notmatch $pattern) {
        throw "Native Operation 映射缺失或模块不匹配：$($item.Operation)"
    }
}

Write-Output "OperationModuleMap: PASS ($($expected.Count) exports)"
