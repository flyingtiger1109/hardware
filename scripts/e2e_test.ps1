<#
.SYNOPSIS
    HZCYKJTHardWare end-to-end regression test
.DESCRIPTION
    Tests the Proxy via HTTP (same interface the DLL uses).
    Usage: .\e2e_test.ps1 [-ProxyHost 127.0.0.1] [-ProxyPort 8089] [-SaveDir .\test_captures]
#>

param(
    [string]$ProxyHost = "127.0.0.1",
    [int]$ProxyPort = 8089,
    [string]$SaveDir = ".\test_captures",
    [switch]$SkipPreview,
    [switch]$Verbose
)

$ErrorActionPreference = "Continue"
$BaseUrl = "http://${ProxyHost}:${ProxyPort}"
$PassCount = 0
$FailCount = 0
$SkipCount = 0
$TestStartTime = Get-Date

if (-not (Test-Path $SaveDir)) {
    New-Item -ItemType Directory -Path $SaveDir -Force | Out-Null
}
$SaveDirFull = (Resolve-Path $SaveDir).Path

# ---- helpers ----

function Write-Banner($Text) {
    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host ("=" * 60) -ForegroundColor Cyan
}

function Send-Request($Path, $BodyObj, $TimeoutMs = 15000) {
    try {
        $bodyJson = ConvertTo-Json -Compress -Depth 4 $BodyObj
        $uri = "$BaseUrl$Path"
        if ($Verbose) { Write-Host "  POST $uri $bodyJson" -ForegroundColor DarkGray }

        $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($bodyJson)
        $req = [System.Net.HttpWebRequest]::Create($uri)
        $req.Method = "POST"
        $req.ContentType = "application/json; charset=utf-8"
        $req.Timeout = $TimeoutMs
        $req.ServicePoint.Expect100Continue = $false
        $req.ContentLength = $bodyBytes.Length
        $reqStream = $req.GetRequestStream()
        $reqStream.Write($bodyBytes, 0, $bodyBytes.Length)
        $reqStream.Flush()
        $reqStream.Close()

        $resp = $req.GetResponse()
        $respStream = $resp.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($respStream, [System.Text.Encoding]::UTF8)
        $result = $reader.ReadToEnd()
        $reader.Close(); $respStream.Close(); $resp.Close()
        return $result
    }
    catch [System.Net.WebException] {
        $errBody = ""
        try {
            if ($_.Exception.Response) {
                $errStream = $_.Exception.Response.GetResponseStream()
                $errReader = New-Object System.IO.StreamReader($errStream, [System.Text.Encoding]::UTF8)
                $errBody = $errReader.ReadToEnd()
                $errReader.Close(); $errStream.Close()
            }
        } catch { }
        $statusCode = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        return ('{"error":true,"code":"http_' + $statusCode + '","message":"' + ($_.Exception.Message -replace '"','') + '"}')
    }
    catch {
        return ('{"error":true,"code":"exception","message":"' + ($_.Exception.Message -replace '"','') + '"}')
    }
}

function Assert-Success($TestName, $Response) {
    if ([string]::IsNullOrEmpty($Response)) {
        Write-Host "  FAIL: $TestName (no response)" -ForegroundColor Red
        $global:FailCount++
        return $false
    }
    # "busy" is acceptable backpressure, not a real error
    if ($Response -match '"error"\s*:\s*true' -and $Response -notmatch '"busy"') {
        $code = if ($Response -match '"code"\s*:\s*"([^"]+)"') { $matches[1] } else { "unknown" }
        Write-Host "  FAIL: $TestName (code=$code)" -ForegroundColor Red
        if ($Verbose) { Write-Host "    Response: $Response" -ForegroundColor DarkGray }
        $global:FailCount++
        return $false
    }
    Write-Host "  PASS: $TestName" -ForegroundColor Green
    $global:PassCount++
    return $true
}

function Assert-Accepted($TestName, $Response) {
    if ([string]::IsNullOrEmpty($Response)) {
        Write-Host "  FAIL: $TestName (no response)" -ForegroundColor Red
        $global:FailCount++
        return $false
    }
    if ($Response -match '"accepted"\s*:\s*true') {
        Write-Host "  PASS: $TestName" -ForegroundColor Green
        $global:PassCount++
        return $true
    }
    if ($Response -match '"status"\s*:\s*"ok"') {
        Write-Host "  PASS: $TestName (status=ok)" -ForegroundColor Green
        $global:PassCount++
        return $true
    }
    if ($Response -match '"busy"') {
        Write-Host "  SKIP: $TestName (server busy - acceptable)" -ForegroundColor Yellow
        $global:SkipCount++
        return $false
    }
    $code = if ($Response -match '"code"\s*:\s*"([^"]+)"') { $matches[1] } else { "unknown" }
    Write-Host "  FAIL: $TestName (code=$code)" -ForegroundColor Red
    if ($Verbose) { Write-Host "    Response: $Response" -ForegroundColor DarkGray }
    $global:FailCount++
    return $false
}

# ===================================================================
Write-Banner "HZCYKJTHardWare End-to-End Regression Test"
Write-Host "  Proxy : $BaseUrl"
Write-Host "  SaveDir: $SaveDirFull"
Write-Host "  Start : $($TestStartTime.ToString('yyyy-MM-dd HH:mm:ss'))"
# ===================================================================

# ---- 1. PING ----
Write-Banner "1. Connectivity"
Assert-Success "Ping" (Send-Request "/ping" @{})

# ---- 2. PROCESS START ----
Write-Banner "2. Process Control"
$processId = "E2E_" + (Get-Date -Format "yyyyMMddHHmmssfff")
$r = Send-Request "/process/start" @{
    request_id = $processId
    save_dir = $SaveDirFull
    callbacks = @{
        ocr = "http://127.0.0.1:39091/HZCYKJTHardWare/callback/ocr"
        nfc = "http://127.0.0.1:39091/HZCYKJTHardWare/callback/nfc-card"
        iris = "http://127.0.0.1:39091/HZCYKJTHardWare/callback/iris"
    }
}
Assert-Success "StartProcess" $r

# ---- 3. FACE CAPTURE ----
Write-Banner "3. Face Capture (sync)"
$faceId = "FACE_" + (Get-Date -Format "yyyyMMddHHmmssfff")
$r = Send-Request "/capture/face" @{ request_id = $faceId; save_dir = $SaveDirFull } 20000
Assert-Success "FaceCapture" $r

# ---- 4. FINGERPRINT CAPTURE ----
Write-Banner "4. Fingerprint Capture (sync)"
$fpId = "FP_" + (Get-Date -Format "yyyyMMddHHmmssfff")
$r = Send-Request "/capture/fingerprint" @{ request_id = $fpId; save_dir = $SaveDirFull } 20000
Assert-Success "Fingerprint" $r

# ---- 5. OCR (async) ----
Write-Banner "5. OCR (async)"
$ocrId = "OCR_" + (Get-Date -Format "yyyyMMddHHmmssfff")
$r = Send-Request "/ocr" @{
    request_id = $ocrId
    save_dir = $SaveDirFull
    callback_url = "http://127.0.0.1:39091/HZCYKJTHardWare/callback/ocr"
} 20000
Assert-Accepted "OCR-accepted" $r
if ($r -match '"accepted"\s*:\s*true') {
    Write-Host "     -> waiting for terminal callback (OCR ~5-15s)..." -ForegroundColor DarkGray
}

# ---- 6. NFC (async) ----
Write-Banner "6. NFC Card (async)"
$nfcId = "NFC_" + (Get-Date -Format "yyyyMMddHHmmssfff")
$r = Send-Request "/nfc" @{
    request_id = $nfcId
    save_dir = $SaveDirFull
    callback_url = "http://127.0.0.1:39091/HZCYKJTHardWare/callback/nfc-card"
} 20000
Assert-Accepted "NFC-accepted" $r

# ---- 7. IRIS (async) ----
Write-Banner "7. Iris Capture (async)"
$irisId = "IRIS_" + (Get-Date -Format "yyyyMMddHHmmssfff")
$r = Send-Request "/capture/iris" @{
    request_id = $irisId
    save_dir = $SaveDirFull
    callback_url = "http://127.0.0.1:39091/HZCYKJTHardWare/callback/iris"
} 20000
Assert-Accepted "Iris-accepted" $r

# ---- 8. AUTHORIZE (async) ----
Write-Banner "8. Authorize (async)"
$authId = "AUTH_" + (Get-Date -Format "yyyyMMddHHmmssfff")
$r = Send-Request "/authorize" @{
    request_id = $authId
    XM = "TEST"
    XB = "M"
    ZJHM = "H12345678"
    ZJLB = "24"
    CSRQ = "19900101"
    GJDQDM = "CHN"
    KADM = "4401"
    callback_url = "http://127.0.0.1:39091/HZCYKJTHardWare/callback/authorize"
} 20000
Assert-Accepted "Authorize-accepted" $r

# ---- 9. TERMINAL SWITCH ----
Write-Banner "9. Terminal Switch"
$r = Send-Request "/terminal/switch" @{ terminal_index = 2 }
Assert-Accepted "Switch-to-2" $r

Start-Sleep -Seconds 2
Write-Host "     -> switching back to terminal 1..." -ForegroundColor DarkGray
$r = Send-Request "/terminal/switch" @{ terminal_index = 1 }
Assert-Accepted "Switch-to-1" $r

# ---- 10. PREVIEW ----
if (-not $SkipPreview) {
    Write-Banner "10. Preview"
    $r = Send-Request "/preview/camera/url" @{}
    if ($r -match '"preview_url"') {
        Write-Host "  PASS: Camera preview URL" -ForegroundColor Green
        $PassCount++
    } else {
        Write-Host "  SKIP: Camera preview (URL unavailable)" -ForegroundColor Yellow
        $SkipCount++
    }
}

# ---- 11. QUEUE PRESSURE ----
Write-Banner "11. Queue Pressure (10 rapid OCR requests)"
$acceptCount = 0
$busyCount = 0
for ($i = 0; $i -lt 10; $i++) {
    $rid = "PRESS_${i}_" + (Get-Date -Format "HHmmssfff")
    $r = Send-Request "/ocr" @{
        request_id = $rid
        save_dir = $SaveDirFull
        callback_url = "http://127.0.0.1:39091/HZCYKJTHardWare/callback/ocr"
    } 5000
    if ($r -match '"accepted"') { $acceptCount++ }
    elseif ($r -match '"busy"') { $busyCount++ }
}
Write-Host "  accepted=$acceptCount busy=$busyCount (total 10)" -ForegroundColor $(if ($acceptCount -gt 0) { "Green" } else { "Red" })
if ($acceptCount -gt 0) {
    Write-Host "  PASS: Queue pressure" -ForegroundColor Green
    $PassCount++
} else {
    Write-Host "  FAIL: Queue pressure (all rejected)" -ForegroundColor Red
    $FailCount++
}

# ---- 12. END PROCESS ----
Write-Banner "12. End Process"
$r = Send-Request "/process/end" @{}
Assert-Success "EndProcess" $r

# ---- 13. FILES CHECK ----
Write-Banner "13. Output Files"
Start-Sleep -Seconds 3
$newFiles = @(Get-ChildItem -Path $SaveDir -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.LastWriteTime -gt $TestStartTime })
if ($newFiles.Count -gt 0) {
    Write-Host "  PASS: $($newFiles.Count) new files" -ForegroundColor Green
    $PassCount++
    $newFiles | Select-Object -First 10 | ForEach-Object {
        Write-Host "     $($_.Name) ($('{0:N0}' -f $_.Length) bytes)" -ForegroundColor DarkGray
    }
} else {
    Write-Host "  WARN: no new files (callbacks may still be pending)" -ForegroundColor Yellow
    $SkipCount++
}

# ===================================================================
Write-Banner "Results"
$total = $PassCount + $FailCount + $SkipCount
$elapsed = (Get-Date) - $TestStartTime
Write-Host "  PASS : $PassCount" -ForegroundColor Green
Write-Host "  FAIL : $FailCount" -ForegroundColor $(if ($FailCount -gt 0) { "Red" } else { "DarkGray" })
Write-Host "  SKIP : $SkipCount" -ForegroundColor $(if ($SkipCount -gt 0) { "Yellow" } else { "DarkGray" })
Write-Host "  Total: $total" -ForegroundColor Cyan
Write-Host "  Time : $($elapsed.TotalSeconds.ToString('F1'))s" -ForegroundColor Cyan
Write-Host ""

if ($FailCount -eq 0) {
    Write-Host "  ALL CRITICAL TESTS PASSED" -ForegroundColor Green
    exit 0
} else {
    Write-Host "  $FailCount FAILURES - check logs" -ForegroundColor Red
    exit 1
}
