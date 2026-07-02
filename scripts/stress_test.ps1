<#
.SYNOPSIS
    HZCYKJTHardWare stress test: preview + high-frequency capture + periodic switch
.DESCRIPTION
    Starts camera preview, then loops face+fingerprint capture while
    switching terminals every 30 seconds. Face and fingerprint images
    overwrite fixed files, and each run writes a timestamped CSV summary.
    Press Ctrl+C to stop.
    Usage: .\stress_test.ps1 [-ProxyHost 127.0.0.1] [-ProxyPort 18080] [-DurationMin 5]
#>

param(
    [string]$ProxyHost = "127.0.0.1",
    [int]$ProxyPort = 8089,
    [int]$DurationMin = 5,
    [int]$SwitchIntervalSec = 30,
    [string]$SaveDir = ".\stress_captures"
)

$ErrorActionPreference = "Continue"
$BaseUrl = "http://${ProxyHost}:${ProxyPort}"
$StopFlag = $false
$TestStartedAt = Get-Date
$RunStamp = $TestStartedAt.ToString("yyyyMMdd_HHmmss")
$Stats = @{
    FaceOk = 0; FaceFail = 0
    FingerOk = 0; FingerFail = 0
    SwitchOk = 0; SwitchFail = 0
    CaptureErrors = @()
}

if (-not (Test-Path $SaveDir)) {
    New-Item -ItemType Directory -Path $SaveDir -Force | Out-Null
}
$SaveDirFull = (Resolve-Path $SaveDir).Path
$FaceSavePath = Join-Path $SaveDirFull "face.jpg"
$FingerprintSavePath = Join-Path $SaveDirFull "fingerprint.jpg"
$SummaryCsvPath = Join-Path $SaveDirFull "stress_summary_${RunStamp}.csv"

# ---- helpers ----

function Send-Request($Path, $BodyObj, $TimeoutMs = 15000) {
    try {
        $bodyJson = ConvertTo-Json -Compress -Depth 4 $BodyObj
        $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes($bodyJson)
        $req = [System.Net.HttpWebRequest]::Create("$BaseUrl$Path")
        $req.Method = "POST"
        $req.ContentType = "application/json; charset=utf-8"
        $req.Timeout = $TimeoutMs
        $req.ServicePoint.Expect100Continue = $false
        $req.ContentLength = $bodyBytes.Length
        $reqStream = $req.GetRequestStream()
        $reqStream.Write($bodyBytes, 0, $bodyBytes.Length)
        $reqStream.Flush(); $reqStream.Close()
        $resp = $req.GetResponse()
        $reader = New-Object System.IO.StreamReader($resp.GetResponseStream(), [System.Text.Encoding]::UTF8)
        $result = $reader.ReadToEnd()
        $reader.Close(); $resp.Close()
        return $result
    } catch [System.Net.WebException] {
        $errBody = ""
        try {
            if ($_.Exception.Response) {
                $errStream = $_.Exception.Response.GetResponseStream()
                $errReader = New-Object System.IO.StreamReader($errStream, [System.Text.Encoding]::UTF8)
                $errBody = $errReader.ReadToEnd(); $errReader.Close(); $errStream.Close()
            }
        } catch { }
        return ('{"error":true,"code":"http_error","body":"' + ($errBody -replace '"','') + '"}')
    } catch {
        return '{"error":true,"code":"exception"}'
    }
}

function Start-Preview($Type) {
    Write-Host "  Starting $Type preview..." -ForegroundColor DarkGray
    $r = Send-Request "/preview/$Type/url" @{}
    if ($r -match '"preview_url"\s*:\s*"([^"]+)"') {
        $url = $matches[1]
        Write-Host "  $Type preview URL: $($url.Substring(0, [Math]::Min(60, $url.Length)))..." -ForegroundColor Green
        return $true
    }
    Write-Host "  $Type preview: URL unavailable" -ForegroundColor Yellow
    return $false
}

function Stop-Preview($Type) {
    Send-Request "/preview/$Type/stop" @{} | Out-Null
}

function Invoke-Capture($Type) {
    $id = "$($Type.ToUpper())_" + (Get-Date -Format "HHmmssfff")
    $path = if ($Type -eq "face") { "/capture/face" } else { "/capture/fingerprint" }
    $savePath = if ($Type -eq "face") { $FaceSavePath } else { $FingerprintSavePath }
    $r = Send-Request $path @{ request_id = $id; save_dir = $savePath } 10000
    $ok = ($r -notmatch '"error"\s*:\s*true')
    return @{ Ok = $ok; Id = $id; Response = $r }
}

function Invoke-Switch($Target) {
    $r = Send-Request "/terminal/switch" @{ terminal_index = $Target } 15000
    return ($r -match '"status"\s*:\s*"ok"')
}

# ---- Ctrl+C handler ----
$consoleHandler = {
    Write-Host "`n  Stopping..." -ForegroundColor Yellow
    $script:StopFlag = $true
}
[Console]::TreatControlCAsInput = $false
try { [Console]::CancelKeyPress += $consoleHandler } catch { }

# ===================================================================
Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "  HZCYKJTHardWare Stress Test" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "  Proxy    : $BaseUrl"
Write-Host "  Duration : $DurationMin min"
Write-Host "  Switch   : every ${SwitchIntervalSec}s"
Write-Host "  SaveDir  : $SaveDirFull"
Write-Host "  FaceFile : $FaceSavePath (overwrite)"
Write-Host "  FingerFile: $FingerprintSavePath (overwrite)"
Write-Host "  Summary  : $SummaryCsvPath"
Write-Host "  Start    : $($TestStartedAt.ToString('yyyy-MM-dd HH:mm:ss'))"
Write-Host ""

# ---- Step 1: Ping ----
Write-Host "[INIT] Checking connectivity..." -ForegroundColor DarkGray
$r = Send-Request "/ping" @{}
if ($r -notmatch '"status"\s*:\s*"ok"') {
    Write-Host "  FATAL: Proxy not reachable" -ForegroundColor Red
    exit 1
}
Write-Host "  Proxy OK" -ForegroundColor Green

# ---- Step 2: Start process ----
Write-Host "[INIT] Starting process..." -ForegroundColor DarkGray
$processId = "STRESS_" + (Get-Date -Format "yyyyMMddHHmmssfff")
$r = Send-Request "/process/start" @{
    request_id = $processId
    save_dir = $SaveDirFull
    callbacks = @{
        ocr = "http://127.0.0.1:39091/HZCYKJTHardWare/callback/ocr"
        nfc = "http://127.0.0.1:39091/HZCYKJTHardWare/callback/nfc-card"
        iris = "http://127.0.0.1:39091/HZCYKJTHardWare/callback/iris"
    }
}
if ($r -match '"status"\s*:\s*"ok"') {
    Write-Host "  Process started: $processId" -ForegroundColor Green
} else {
    Write-Host "  Process start failed: $r" -ForegroundColor Red
}

# ---- Step 3: Start previews ----
Write-Host "[INIT] Opening previews..." -ForegroundColor DarkGray
$camOk = Start-Preview "camera"
$fpOk  = Start-Preview "fingerprint"
$irisOk = Start-Preview "iris"

# ---- Main loop ----
$deadline = (Get-Date).AddMinutes($DurationMin)
$switchTarget = 1
$lastSwitchTime = Get-Date
$loopCount = 0
$LoopStartedAt = Get-Date

Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "  STRESS LOOP STARTED (Ctrl+C to stop)" -ForegroundColor Yellow
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host ("  Time".PadRight(12) + "Face     Finger   Switch   Total")
Write-Host ("  " + "-" * 52)

while (-not $StopFlag -and (Get-Date) -lt $deadline) {
    $loopCount++
    $loopStart = Get-Date

    # ---- Face capture ----
    $face = Invoke-Capture "face"
    if ($face.Ok) { $Stats.FaceOk++ } else { $Stats.FaceFail++ }

    # ---- Finger capture ----
    Start-Sleep -Milliseconds 200  # brief gap between requests
    $finger = Invoke-Capture "fingerprint"
    if ($finger.Ok) { $Stats.FingerOk++ } else { $Stats.FingerFail++ }

    # ---- Switch terminal every N seconds ----
    $elapsedSinceSwitch = ((Get-Date) - $lastSwitchTime).TotalSeconds
    if ($elapsedSinceSwitch -ge $SwitchIntervalSec) {
        $switchTarget = if ($switchTarget -eq 1) { 2 } else { 1 }
        $swOk = Invoke-Switch $switchTarget
        if ($swOk) { $Stats.SwitchOk++ } else { $Stats.SwitchFail++ }
        $lastSwitchTime = Get-Date
    }

    # ---- Status line ----
    $now = Get-Date -Format "HH:mm:ss"
    $total = $Stats.FaceOk + $Stats.FaceFail + $Stats.FingerOk + $Stats.FingerFail
    Write-Host ("  $now  " +
        "F:$($Stats.FaceOk)/$($Stats.FaceFail)  " +
        "P:$($Stats.FingerOk)/$($Stats.FingerFail)  " +
        "S:$($Stats.SwitchOk)/$($Stats.SwitchFail)  " +
        "N:$total")

    # Pace: ~3 requests/sec (face + finger + gap)
    # CPU-friendly: minimal sleep between loops
    $loopDuration = ((Get-Date) - $loopStart).TotalMilliseconds
    $sleepMs = [Math]::Max(50, 300 - $loopDuration)
    Start-Sleep -Milliseconds $sleepMs
}

# ---- Cleanup ----
Write-Host ""
Write-Host "[DONE] Stopping previews..." -ForegroundColor DarkGray
if ($camOk) { Stop-Preview "camera" }
if ($fpOk)  { Stop-Preview "fingerprint" }
if ($irisOk) { Stop-Preview "iris" }

Write-Host "[DONE] Ending process..." -ForegroundColor DarkGray
Send-Request "/process/end" @{} | Out-Null

# ---- Results ----
$TestEndedAt = Get-Date
$elapsedSeconds = [Math]::Round(($TestEndedAt - $TestStartedAt).TotalSeconds, 3)
$loopElapsedSeconds = [Math]::Round(($TestEndedAt - $LoopStartedAt).TotalSeconds, 3)
$totalFace = $Stats.FaceOk + $Stats.FaceFail
$totalFinger = $Stats.FingerOk + $Stats.FingerFail
$totalSwitches = $Stats.SwitchOk + $Stats.SwitchFail
$captureSuccess = $Stats.FaceOk + $Stats.FingerOk
$captureFailure = $Stats.FaceFail + $Stats.FingerFail
$totalRequests = $totalFace + $totalFinger + $totalSwitches
$totalFailures = $captureFailure + $Stats.SwitchFail
$failRate = if ($totalRequests -gt 0) {
    [math]::Round($totalFailures / $totalRequests * 100, 3)
} else {
    0
}
$resultText = if ($totalFailures -eq 0) { "PASSED" } else { "FAILED" }

$summary = [pscustomobject]@{
    RunId = $RunStamp
    Result = $resultText
    Proxy = $BaseUrl
    StartedAt = $TestStartedAt.ToString("yyyy-MM-dd HH:mm:ss.fff")
    EndedAt = $TestEndedAt.ToString("yyyy-MM-dd HH:mm:ss.fff")
    ConfiguredDurationMinutes = $DurationMin
    TotalDurationSeconds = $elapsedSeconds
    StressLoopDurationSeconds = $loopElapsedSeconds
    LoopCount = $loopCount
    FaceCaptureSuccess = $Stats.FaceOk
    FaceCaptureFailure = $Stats.FaceFail
    FingerprintCaptureSuccess = $Stats.FingerOk
    FingerprintCaptureFailure = $Stats.FingerFail
    CaptureSuccessTotal = $captureSuccess
    CaptureFailureTotal = $captureFailure
    TerminalSwitchSuccess = $Stats.SwitchOk
    TerminalSwitchFailure = $Stats.SwitchFail
    TerminalSwitchTotal = $totalSwitches
    TerminalSwitchSuccessDefinition = "POST /terminal/switch returned status=ok"
    TotalRequests = $totalRequests
    TotalFailures = $totalFailures
    FailureRatePercent = $failRate
    FaceFile = $FaceSavePath
    FingerprintFile = $FingerprintSavePath
}

try {
    $summary | Export-Csv -LiteralPath $SummaryCsvPath -NoTypeInformation -Encoding UTF8 -ErrorAction Stop
    Write-Host "  Summary CSV  : $SummaryCsvPath" -ForegroundColor Cyan
} catch {
    Write-Host "  Summary CSV write failed: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "  RESULTS" -ForegroundColor Cyan
Write-Host ("=" * 60) -ForegroundColor Cyan
Write-Host "  Loops        : $loopCount"
Write-Host "  Face   OK/FAIL: $($Stats.FaceOk) / $($Stats.FaceFail)" -ForegroundColor $(if ($Stats.FaceFail -eq 0) { "Green" } elseif ($Stats.FaceFail -gt $Stats.FaceOk) { "Red" } else { "Yellow" })
Write-Host "  Finger OK/FAIL: $($Stats.FingerOk) / $($Stats.FingerFail)" -ForegroundColor $(if ($Stats.FingerFail -eq 0) { "Green" } elseif ($Stats.FingerFail -gt $Stats.FingerOk) { "Red" } else { "Yellow" })
Write-Host "  Capture total OK/FAIL: $captureSuccess / $captureFailure"
Write-Host "  Switch OK/FAIL: $($Stats.SwitchOk) / $($Stats.SwitchFail)" -ForegroundColor $(if ($Stats.SwitchFail -eq 0) { "Green" } else { "Red" })
Write-Host "  Total requests: $totalRequests"
Write-Host "  Duration (sec): $elapsedSeconds"
Write-Host ""

if ($Stats.FaceFail -eq 0 -and $Stats.FingerFail -eq 0 -and $Stats.SwitchFail -eq 0) {
    Write-Host "  ALL PASSED - ZERO FAILURES" -ForegroundColor Green
    exit 0
} else {
    Write-Host "  Fail rate: ${failRate}%" -ForegroundColor $(if ($failRate -lt 5) { "Yellow" } else { "Red" })
    exit 1
}
