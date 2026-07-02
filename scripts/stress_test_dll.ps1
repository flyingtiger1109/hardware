<#
.SYNOPSIS
    HZCYKJTHardWare real third-party chain stress test through the x86 DLL.
.DESCRIPTION
    Calls the exported stdcall APIs from a 32-bit STA PowerShell process:
    PowerShell UI thread -> HZCYKJTHardWare.dll -> HTTP -> C# Proxy -> terminal.

    Face and fingerprint captures overwrite face.jpg and fingerprint.jpg.
    A summary CSV and a per-call timing CSV are generated for every run.
    DLL calls execute on the WinForms UI thread so their elapsed time represents
    the time a synchronous third-party UI call would be blocked.

    The script changes real device state and switches terminals. Press Ctrl+C or
    close the preview window to stop.
#>

param(
    [string]$DllPath = (Join-Path $PSScriptRoot "..\demo\CSharpProxy\HZCYKJTHardWare.Proxy\bin\x86\Release\net46\HZCYKJTHardWare.dll"),
    [double]$DurationMin = 5,
    [int]$SwitchIntervalSec = 30,
    [int]$CaptureGapMs = 200,
    [int]$LoopMinIntervalMs = 300,
    [int]$UiBlockWarningMs = 500,
    [string]$SaveDir = (Join-Path $PSScriptRoot "stress_captures"),
    [switch]$SkipPreview,
    [switch]$VerboseCallbacks,
    [switch]$ValidateOnly
)

$ErrorActionPreference = "Stop"

function Forward-ToX86PowerShell {
    $x86PowerShell = Join-Path $env:WINDIR "SysWOW64\WindowsPowerShell\v1.0\powershell.exe"
    if (-not [Environment]::Is64BitOperatingSystem -or -not (Test-Path -LiteralPath $x86PowerShell)) {
        throw "The DLL is x86, but 32-bit Windows PowerShell is unavailable."
    }

    $forwardArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-STA",
        "-File", $PSCommandPath,
        "-DllPath", $DllPath,
        "-DurationMin", ([string]$DurationMin),
        "-SwitchIntervalSec", ([string]$SwitchIntervalSec),
        "-CaptureGapMs", ([string]$CaptureGapMs),
        "-LoopMinIntervalMs", ([string]$LoopMinIntervalMs),
        "-UiBlockWarningMs", ([string]$UiBlockWarningMs),
        "-SaveDir", $SaveDir
    )
    if ($SkipPreview) { $forwardArgs += "-SkipPreview" }
    if ($VerboseCallbacks) { $forwardArgs += "-VerboseCallbacks" }
    if ($ValidateOnly) { $forwardArgs += "-ValidateOnly" }

    Write-Host "Restarting with 32-bit STA Windows PowerShell..." -ForegroundColor Yellow
    & $x86PowerShell @forwardArgs
    exit $LASTEXITCODE
}

if ([IntPtr]::Size -ne 4) {
    Forward-ToX86PowerShell
}

if ([Threading.Thread]::CurrentThread.GetApartmentState() -ne [Threading.ApartmentState]::STA) {
    throw "The test must run in an STA PowerShell process. Use powershell.exe -STA -File."
}
if ($DurationMin -le 0) { throw "DurationMin must be greater than zero." }
if ($SwitchIntervalSec -le 0) { throw "SwitchIntervalSec must be greater than zero." }
if ($CaptureGapMs -lt 0) { throw "CaptureGapMs cannot be negative." }
if ($LoopMinIntervalMs -lt 0) { throw "LoopMinIntervalMs cannot be negative." }
if ($UiBlockWarningMs -lt 0) { throw "UiBlockWarningMs cannot be negative." }

$resolvedDll = (Resolve-Path -LiteralPath $DllPath).Path
$resolvedDllDirectory = Split-Path -Parent $resolvedDll
$coLocatedProxyExe = Join-Path $resolvedDllDirectory "HZCYKJTHardWare.Proxy.exe"

function Get-PeMachine {
    param([string]$Path)

    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        $reader = New-Object IO.BinaryReader($stream)
        try {
            if ($stream.Length -lt 64) { throw "File is too small to be a PE image." }
            $stream.Position = 0x3C
            $peOffset = $reader.ReadInt32()
            if ($peOffset -lt 0 -or ($peOffset + 6) -gt $stream.Length) {
                throw "Invalid PE header offset."
            }
            $stream.Position = $peOffset
            if ($reader.ReadUInt32() -ne 0x00004550) { throw "Invalid PE signature." }
            return $reader.ReadUInt16()
        } finally {
            $reader.Dispose()
        }
    } finally {
        $stream.Dispose()
    }
}

$peMachine = Get-PeMachine -Path $resolvedDll
if ($peMachine -ne 0x014C) {
    throw ("Expected an x86 DLL (PE machine 0x014C), actual machine is 0x{0:X4}." -f $peMachine)
}

if (-not (Test-Path -LiteralPath $SaveDir)) {
    New-Item -ItemType Directory -Path $SaveDir -Force | Out-Null
}
$saveDirFull = (Resolve-Path -LiteralPath $SaveDir).Path
$faceSavePath = Join-Path $saveDirFull "face.jpg"
$fingerprintSavePath = Join-Path $saveDirFull "fingerprint.jpg"
$testStartedAt = Get-Date
$runStamp = $testStartedAt.ToString("yyyyMMdd_HHmmss")
$summaryCsvPath = Join-Path $saveDirFull "stress_dll_summary_${runStamp}.csv"
$callsCsvPath = Join-Path $saveDirFull "stress_dll_calls_${runStamp}.csv"

# DllImport uses the absolute DLL path. All char* parameters are explicitly
# allocated as UTF-8 so Chinese paths behave the same as the supported demos.
$escapedDll = $resolvedDll.Replace('"', '""')
$nativeSource = @"
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public static class HzcyDllStressNative
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void EventCallback(IntPtr eventJson);

    private static readonly EventCallback CallbackRoot = OnEvent;
    private static readonly ConcurrentQueue<string> CallbackEvents = new ConcurrentQueue<string>();
    private static long callbackCount;

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern int HZCYKJTHardWare_InitSdk();

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern int HZCYKJTHardWare_ReleaseSdk();

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern int HZCYKJTHardWare_RegisterEventCallback(EventCallback callback);

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern int HZCYKJTHardWare_StartProcess(IntPtr saveDir);

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern int HZCYKJTHardWare_EndProcess();

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern int HZCYKJTHardWare_StartCameraPreview(IntPtr hwnd);

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern int HZCYKJTHardWare_StopCameraPreview();

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern int HZCYKJTHardWare_StartFingerprintPreview(IntPtr hwnd);

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern int HZCYKJTHardWare_StopFingerprintPreview();

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern int HZCYKJTHardWare_CaptureCameraImage(IntPtr saveDir);

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern int HZCYKJTHardWare_CaptureFingerprintImage(IntPtr saveDir);

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
    private static extern int HZCYKJTHardWare_SwitchTerminal(int terminalIndex);

    public static int InitSdk() { return HZCYKJTHardWare_InitSdk(); }
    public static int ReleaseSdk() { return HZCYKJTHardWare_ReleaseSdk(); }
    public static int RegisterCallback() { return HZCYKJTHardWare_RegisterEventCallback(CallbackRoot); }
    public static int EndProcess() { return HZCYKJTHardWare_EndProcess(); }
    public static int StartCameraPreview(IntPtr hwnd) { return HZCYKJTHardWare_StartCameraPreview(hwnd); }
    public static int StopCameraPreview() { return HZCYKJTHardWare_StopCameraPreview(); }
    public static int StartFingerprintPreview(IntPtr hwnd) { return HZCYKJTHardWare_StartFingerprintPreview(hwnd); }
    public static int StopFingerprintPreview() { return HZCYKJTHardWare_StopFingerprintPreview(); }
    public static int SwitchTerminal(int terminalIndex) { return HZCYKJTHardWare_SwitchTerminal(terminalIndex); }
    public static long CallbackCount { get { return Interlocked.Read(ref callbackCount); } }

    public static int StartProcess(string saveDir)
    {
        return InvokeWithUtf8(saveDir, HZCYKJTHardWare_StartProcess);
    }

    public static int CaptureCameraImage(string savePath)
    {
        return InvokeWithUtf8(savePath, HZCYKJTHardWare_CaptureCameraImage);
    }

    public static int CaptureFingerprintImage(string savePath)
    {
        return InvokeWithUtf8(savePath, HZCYKJTHardWare_CaptureFingerprintImage);
    }

    public static string[] DrainCallbackEvents()
    {
        var events = new List<string>();
        string item;
        while (CallbackEvents.TryDequeue(out item))
        {
            events.Add(item);
        }
        return events.ToArray();
    }

    private static int InvokeWithUtf8(string value, Func<IntPtr, int> action)
    {
        IntPtr pointer = AllocUtf8(value);
        try
        {
            return action(pointer);
        }
        finally
        {
            if (pointer != IntPtr.Zero) Marshal.FreeHGlobal(pointer);
        }
    }

    private static IntPtr AllocUtf8(string value)
    {
        if (value == null) return IntPtr.Zero;
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        IntPtr pointer = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, pointer, bytes.Length);
        Marshal.WriteByte(pointer, bytes.Length, 0);
        return pointer;
    }

    private static string PtrToUtf8(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero) return String.Empty;
        int length = 0;
        while (Marshal.ReadByte(pointer, length) != 0) length++;
        if (length == 0) return String.Empty;
        byte[] bytes = new byte[length];
        Marshal.Copy(pointer, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }

    private static void OnEvent(IntPtr eventJson)
    {
        try
        {
            CallbackEvents.Enqueue(PtrToUtf8(eventJson));
            Interlocked.Increment(ref callbackCount);
        }
        catch
        {
            // Native callback boundaries must never propagate managed exceptions.
        }
    }
}
"@

if ("HzcyDllStressNative" -as [type]) {
    throw "HzcyDllStressNative is already loaded. Run the script in a new PowerShell process."
}
Add-Type -TypeDefinition $nativeSource -Language CSharp
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

if ($ValidateOnly) {
    Write-Host "VALIDATION_OK: x86 process, x86 PE image, and P/Invoke declarations compiled." -ForegroundColor Green
    exit 0
}

$script:stopFlag = $false
$script:callRecords = New-Object 'System.Collections.Generic.List[object]'
$script:callbackEventsObserved = 0
$script:callbackErrorEvents = 0
$stats = @{
    FaceOk = 0; FaceFail = 0
    FingerOk = 0; FingerFail = 0
    SwitchOk = 0; SwitchFail = 0
}

function Invoke-DllCall {
    param(
        [string]$Name,
        [string]$Stage,
        [scriptblock]$Action
    )

    $startedAt = Get-Date
    $watch = [Diagnostics.Stopwatch]::StartNew()
    $returnCode = $null
    $exceptionMessage = ""
    try {
        $returnCode = [int](& $Action)
    } catch {
        $exceptionMessage = $_.Exception.GetBaseException().Message
    } finally {
        $watch.Stop()
    }

    $success = ($exceptionMessage.Length -eq 0 -and $returnCode -eq 1)
    $record = [pscustomobject]@{
        Sequence = $script:callRecords.Count + 1
        StartedAt = $startedAt.ToString("yyyy-MM-dd HH:mm:ss.fff")
        Stage = $Stage
        Operation = $Name
        ReturnCode = $returnCode
        Success = $success
        DurationMs = $watch.ElapsedMilliseconds
        UiBlockWarning = ($watch.ElapsedMilliseconds -ge $UiBlockWarningMs)
        Exception = $exceptionMessage
        ThreadId = [Threading.Thread]::CurrentThread.ManagedThreadId
        ApartmentState = [Threading.Thread]::CurrentThread.GetApartmentState().ToString()
    }
    [void]$script:callRecords.Add($record)

    $color = if ($success) {
        if ($record.UiBlockWarning) { "Yellow" } else { "Green" }
    } else {
        "Red"
    }
    $suffix = if ($exceptionMessage.Length -gt 0) { " exception=$exceptionMessage" } else { "" }
    Write-Host ("  {0}: ret={1}, elapsed={2}ms{3}" -f $Name, $returnCode, $record.DurationMs, $suffix) -ForegroundColor $color
    return $record
}

function Wait-WithUiPump {
    param([int]$Milliseconds)

    if ($Milliseconds -le 0) {
        [Windows.Forms.Application]::DoEvents()
        return
    }

    $deadline = [DateTime]::UtcNow.AddMilliseconds($Milliseconds)
    while (-not $script:stopFlag -and [DateTime]::UtcNow -lt $deadline) {
        [Windows.Forms.Application]::DoEvents()
        $remaining = [int][Math]::Ceiling(($deadline - [DateTime]::UtcNow).TotalMilliseconds)
        if ($remaining -gt 0) {
            Start-Sleep -Milliseconds ([Math]::Min(20, $remaining))
        }
    }
}

function Drain-CallbackEvents {
    $events = [HzcyDllStressNative]::DrainCallbackEvents()
    foreach ($eventJson in $events) {
        $script:callbackEventsObserved++
        if ($eventJson -match '"event_type"\s*:\s*1999' -or
            $eventJson -match '"event_type"\s*:\s*(1203|1303|1402|1502)') {
            $script:callbackErrorEvents++
        }
        if ($VerboseCallbacks) {
            Write-Host "  CALLBACK: $eventJson" -ForegroundColor DarkCyan
        }
    }
}

function Get-Average {
    param([object[]]$Values)
    if ($Values.Count -eq 0) { return 0 }
    return [Math]::Round(($Values | Measure-Object -Average).Average, 3)
}

function Get-Maximum {
    param([object[]]$Values)
    if ($Values.Count -eq 0) { return 0 }
    return [long](($Values | Measure-Object -Maximum).Maximum)
}

function Get-Percentile95 {
    param([object[]]$Values)
    if ($Values.Count -eq 0) { return 0 }
    $sorted = @($Values | Sort-Object)
    $index = [Math]::Max(0, [Math]::Ceiling($sorted.Count * 0.95) - 1)
    return [long]$sorted[$index]
}

$form = $null
$cameraPanel = $null
$fingerprintPanel = $null
$initialized = $false
$processStarted = $false
$cameraPreviewStarted = $false
$fingerprintPreviewStarted = $false
$fatalMessage = ""
$loopCount = 0
$loopStartedAt = $null
$loopEndedAt = $null

$consoleHandler = {
    param($sender, $eventArgs)
    $eventArgs.Cancel = $true
    $script:stopFlag = $true
}
try { [Console]::CancelKeyPress += $consoleHandler } catch { }

Write-Host ""
Write-Host ("=" * 72) -ForegroundColor Cyan
Write-Host "  HZCYKJTHardWare DLL Real-Chain Stress Test" -ForegroundColor Cyan
Write-Host ("=" * 72) -ForegroundColor Cyan
Write-Host "  Process bitness : x86"
Write-Host "  DLL             : $resolvedDll"
Write-Host "  DLL directory   : $resolvedDllDirectory"
Write-Host "  Duration        : $DurationMin min"
Write-Host "  Switch interval : $SwitchIntervalSec sec"
Write-Host "  UI warning      : >= $UiBlockWarningMs ms"
Write-Host "  Face file       : $faceSavePath (overwrite)"
Write-Host "  Finger file     : $fingerprintSavePath (overwrite)"
Write-Host "  Summary CSV     : $summaryCsvPath"
Write-Host "  Calls CSV       : $callsCsvPath"
Write-Host ""
if (-not (Test-Path -LiteralPath $coLocatedProxyExe)) {
    Write-Host "  WARNING: Proxy EXE is not beside the DLL; start Proxy before this test." -ForegroundColor Yellow
}

try {
    $record = Invoke-DllCall -Name "InitSdk" -Stage "Setup" -Action { [HzcyDllStressNative]::InitSdk() }
    if (-not $record.Success) { throw "InitSdk failed. Check DLL logs and Proxy availability." }
    $initialized = $true

    $record = Invoke-DllCall -Name "RegisterEventCallback" -Stage "Setup" -Action { [HzcyDllStressNative]::RegisterCallback() }
    if (-not $record.Success) { throw "RegisterEventCallback failed." }

    $record = Invoke-DllCall -Name "StartProcess" -Stage "Setup" -Action { [HzcyDllStressNative]::StartProcess($saveDirFull) }
    if (-not $record.Success) { throw "StartProcess failed. Check Proxy and terminal connectivity." }
    $processStarted = $true

    if (-not $SkipPreview) {
        [Windows.Forms.Application]::EnableVisualStyles()
        $form = New-Object Windows.Forms.Form
        $form.Text = "HZCYKJTHardWare DLL Stress Preview - close to stop"
        $form.StartPosition = [Windows.Forms.FormStartPosition]::CenterScreen
        $form.ClientSize = New-Object Drawing.Size(960, 360)
        $form.BackColor = [Drawing.Color]::FromArgb(35, 35, 35)

        $cameraPanel = New-Object Windows.Forms.Panel
        $cameraPanel.Location = New-Object Drawing.Point(10, 10)
        $cameraPanel.Size = New-Object Drawing.Size(570, 340)
        $cameraPanel.BackColor = [Drawing.Color]::Black

        $fingerprintPanel = New-Object Windows.Forms.Panel
        $fingerprintPanel.Location = New-Object Drawing.Point(590, 10)
        $fingerprintPanel.Size = New-Object Drawing.Size(360, 340)
        $fingerprintPanel.BackColor = [Drawing.Color]::FromArgb(20, 20, 20)

        [void]$form.Controls.Add($cameraPanel)
        [void]$form.Controls.Add($fingerprintPanel)
        $form.add_FormClosing({
            param($sender, $eventArgs)
            $eventArgs.Cancel = $true
            $script:stopFlag = $true
            $sender.Hide()
        })
        $form.Show()
        [Windows.Forms.Application]::DoEvents()

        $cameraHwnd = $cameraPanel.Handle
        $fingerprintHwnd = $fingerprintPanel.Handle
        $record = Invoke-DllCall -Name "StartCameraPreview" -Stage "Setup" -Action { [HzcyDllStressNative]::StartCameraPreview($cameraHwnd) }
        $cameraPreviewStarted = $record.Success
        $record = Invoke-DllCall -Name "StartFingerprintPreview" -Stage "Setup" -Action { [HzcyDllStressNative]::StartFingerprintPreview($fingerprintHwnd) }
        $fingerprintPreviewStarted = $record.Success
    }

    $loopStartedAt = Get-Date
    $deadline = $loopStartedAt.AddMinutes($DurationMin)
    $lastSwitchTime = $loopStartedAt
    $switchTarget = 2

    Write-Host ""
    Write-Host "Stress loop started. DLL calls run on UI thread; press Ctrl+C to stop." -ForegroundColor Yellow

    while (-not $script:stopFlag -and (Get-Date) -lt $deadline) {
        $loopCount++
        $loopWatch = [Diagnostics.Stopwatch]::StartNew()

        $record = Invoke-DllCall -Name "CaptureCameraImage" -Stage "Stress" -Action {
            [HzcyDllStressNative]::CaptureCameraImage($faceSavePath)
        }
        if ($record.Success) { $stats.FaceOk++ } else { $stats.FaceFail++ }
        Drain-CallbackEvents
        Wait-WithUiPump -Milliseconds $CaptureGapMs
        if ($script:stopFlag) { break }

        $record = Invoke-DllCall -Name "CaptureFingerprintImage" -Stage "Stress" -Action {
            [HzcyDllStressNative]::CaptureFingerprintImage($fingerprintSavePath)
        }
        if ($record.Success) { $stats.FingerOk++ } else { $stats.FingerFail++ }
        Drain-CallbackEvents

        if (((Get-Date) - $lastSwitchTime).TotalSeconds -ge $SwitchIntervalSec) {
            $targetForCall = $switchTarget
            $record = Invoke-DllCall -Name "SwitchTerminal:$targetForCall" -Stage "Stress" -Action {
                [HzcyDllStressNative]::SwitchTerminal($targetForCall)
            }
            if ($record.Success) { $stats.SwitchOk++ } else { $stats.SwitchFail++ }
            $switchTarget = if ($switchTarget -eq 2) { 1 } else { 2 }
            $lastSwitchTime = Get-Date
            Drain-CallbackEvents
        }

        Write-Host ("  LOOP {0}: face={1}/{2}, finger={3}/{4}, switch={5}/{6}" -f
            $loopCount, $stats.FaceOk, $stats.FaceFail,
            $stats.FingerOk, $stats.FingerFail,
            $stats.SwitchOk, $stats.SwitchFail) -ForegroundColor Gray

        $loopWatch.Stop()
        $remainingPaceMs = [Math]::Max(0, $LoopMinIntervalMs - $loopWatch.ElapsedMilliseconds)
        Wait-WithUiPump -Milliseconds $remainingPaceMs
    }
    $loopEndedAt = Get-Date
} catch {
    $fatalMessage = $_.Exception.GetBaseException().Message
    Write-Host "  FATAL: $fatalMessage" -ForegroundColor Red
} finally {
    if ($null -eq $loopEndedAt -and $null -ne $loopStartedAt) { $loopEndedAt = Get-Date }

    Write-Host ""
    Write-Host "Cleanup..." -ForegroundColor DarkGray
    if ($cameraPreviewStarted) {
        $record = Invoke-DllCall -Name "StopCameraPreview" -Stage "Cleanup" -Action { [HzcyDllStressNative]::StopCameraPreview() }
    }
    if ($fingerprintPreviewStarted) {
        $record = Invoke-DllCall -Name "StopFingerprintPreview" -Stage "Cleanup" -Action { [HzcyDllStressNative]::StopFingerprintPreview() }
    }
    if ($processStarted) {
        $record = Invoke-DllCall -Name "EndProcess" -Stage "Cleanup" -Action { [HzcyDllStressNative]::EndProcess() }
    }
    if ($initialized) {
        $record = Invoke-DllCall -Name "ReleaseSdk" -Stage "Cleanup" -Action { [HzcyDllStressNative]::ReleaseSdk() }
    }

    Drain-CallbackEvents
    if ($null -ne $form) {
        $form.Dispose()
    }
    try { [Console]::CancelKeyPress -= $consoleHandler } catch { }
}

$testEndedAt = Get-Date
$faceDurations = @($script:callRecords | Where-Object { $_.Operation -eq "CaptureCameraImage" } | ForEach-Object { $_.DurationMs })
$fingerDurations = @($script:callRecords | Where-Object { $_.Operation -eq "CaptureFingerprintImage" } | ForEach-Object { $_.DurationMs })
$switchDurations = @($script:callRecords | Where-Object { $_.Operation -like "SwitchTerminal:*" } | ForEach-Object { $_.DurationMs })
$stressDurations = @($script:callRecords | Where-Object { $_.Stage -eq "Stress" } | ForEach-Object { $_.DurationMs })
$setupFailures = @($script:callRecords | Where-Object { $_.Stage -eq "Setup" -and -not $_.Success }).Count
$cleanupFailures = @($script:callRecords | Where-Object { $_.Stage -eq "Cleanup" -and -not $_.Success }).Count
$uiBlockWarnings = @($script:callRecords | Where-Object { $_.UiBlockWarning }).Count
$captureSuccess = $stats.FaceOk + $stats.FingerOk
$captureFailure = $stats.FaceFail + $stats.FingerFail
$terminalSwitchTotal = $stats.SwitchOk + $stats.SwitchFail
$totalBusinessRequests = $captureSuccess + $captureFailure + $terminalSwitchTotal
$totalBusinessFailures = $captureFailure + $stats.SwitchFail
$totalFailureCount = $totalBusinessFailures + $setupFailures + $cleanupFailures
if ($fatalMessage.Length -gt 0 -and $totalFailureCount -eq 0) { $totalFailureCount = 1 }
$failureRate = if ($totalBusinessRequests -gt 0) {
    [Math]::Round($totalBusinessFailures / $totalBusinessRequests * 100, 3)
} else {
    0
}
$resultText = if ($totalFailureCount -eq 0) { "PASSED" } else { "FAILED" }
$loopDurationSeconds = if ($null -ne $loopStartedAt -and $null -ne $loopEndedAt) {
    [Math]::Round(($loopEndedAt - $loopStartedAt).TotalSeconds, 3)
} else {
    0
}

$summary = [pscustomobject]@{
    RunId = $runStamp
    Result = $resultText
    FatalMessage = $fatalMessage
    StartedAt = $testStartedAt.ToString("yyyy-MM-dd HH:mm:ss.fff")
    EndedAt = $testEndedAt.ToString("yyyy-MM-dd HH:mm:ss.fff")
    ConfiguredDurationMinutes = $DurationMin
    TotalDurationSeconds = [Math]::Round(($testEndedAt - $testStartedAt).TotalSeconds, 3)
    StressLoopDurationSeconds = $loopDurationSeconds
    LoopCount = $loopCount
    DllPath = $resolvedDll
    DllPeMachine = ("0x{0:X4}" -f $peMachine)
    ProcessBitness = "x86"
    UiThreadExecution = $true
    UiThreadManagedId = if ($script:callRecords.Count -gt 0) { $script:callRecords[0].ThreadId } else { "" }
    FaceCaptureSuccess = $stats.FaceOk
    FaceCaptureFailure = $stats.FaceFail
    FaceCaptureSuccessDefinition = "HZCYKJTHardWare_CaptureCameraImage returned 1"
    FaceAverageMs = Get-Average $faceDurations
    FaceP95Ms = Get-Percentile95 $faceDurations
    FaceMaxMs = Get-Maximum $faceDurations
    FingerprintCaptureSuccess = $stats.FingerOk
    FingerprintCaptureFailure = $stats.FingerFail
    FingerprintCaptureSuccessDefinition = "HZCYKJTHardWare_CaptureFingerprintImage returned 1"
    FingerprintAverageMs = Get-Average $fingerDurations
    FingerprintP95Ms = Get-Percentile95 $fingerDurations
    FingerprintMaxMs = Get-Maximum $fingerDurations
    CaptureSuccessTotal = $captureSuccess
    CaptureFailureTotal = $captureFailure
    TerminalSwitchSuccess = $stats.SwitchOk
    TerminalSwitchFailure = $stats.SwitchFail
    TerminalSwitchTotal = $terminalSwitchTotal
    TerminalSwitchSuccessDefinition = "HZCYKJTHardWare_SwitchTerminal returned 1"
    SwitchAverageMs = Get-Average $switchDurations
    SwitchP95Ms = Get-Percentile95 $switchDurations
    SwitchMaxMs = Get-Maximum $switchDurations
    TotalBusinessRequests = $totalBusinessRequests
    TotalBusinessFailures = $totalBusinessFailures
    BusinessFailureRatePercent = $failureRate
    SetupFailures = $setupFailures
    CleanupFailures = $cleanupFailures
    UiBlockWarningThresholdMs = $UiBlockWarningMs
    UiBlockWarningCallCount = $uiBlockWarnings
    StressCallP95Ms = Get-Percentile95 $stressDurations
    StressCallMaxMs = Get-Maximum $stressDurations
    NativeCallbackCount = [HzcyDllStressNative]::CallbackCount
    CallbackEventsObserved = $script:callbackEventsObserved
    CallbackErrorEvents = $script:callbackErrorEvents
    PreviewEnabled = (-not $SkipPreview)
    FaceFile = $faceSavePath
    FingerprintFile = $fingerprintSavePath
    CallDetailsCsv = $callsCsvPath
}

try {
    $script:callRecords | Export-Csv -LiteralPath $callsCsvPath -NoTypeInformation -Encoding UTF8
    $summary | Export-Csv -LiteralPath $summaryCsvPath -NoTypeInformation -Encoding UTF8
} catch {
    Write-Host "CSV write failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 2
}

Write-Host ""
Write-Host ("=" * 72) -ForegroundColor Cyan
Write-Host "  RESULT: $resultText" -ForegroundColor $(if ($resultText -eq "PASSED") { "Green" } else { "Red" })
Write-Host ("=" * 72) -ForegroundColor Cyan
Write-Host "  Face OK/FAIL       : $($stats.FaceOk) / $($stats.FaceFail)"
Write-Host "  Finger OK/FAIL     : $($stats.FingerOk) / $($stats.FingerFail)"
Write-Host "  Switch OK/FAIL     : $($stats.SwitchOk) / $($stats.SwitchFail)"
Write-Host "  UI warning calls   : $uiBlockWarnings (>= ${UiBlockWarningMs}ms)"
Write-Host "  Stress P95/MAX     : $(Get-Percentile95 $stressDurations) / $(Get-Maximum $stressDurations) ms"
Write-Host "  Native callbacks   : $([HzcyDllStressNative]::CallbackCount)"
Write-Host "  Summary CSV        : $summaryCsvPath"
Write-Host "  Call details CSV   : $callsCsvPath"

if ($resultText -eq "PASSED") { exit 0 }
exit 1
