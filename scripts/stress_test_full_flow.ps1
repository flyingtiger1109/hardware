param(
    [string]$DllPath = (Join-Path $PSScriptRoot "..\demo\CSharpProxy\HZCYKJTHardWare.Proxy\bin\x86\Release\net46\HZCYKJTHardWare.dll"),
    [string]$ProxyExePath = (Join-Path $PSScriptRoot "..\demo\CSharpProxy\HZCYKJTHardWare.Proxy\bin\x64\Release\net46\HZCYKJTHardWare.Proxy.exe"),
    [int]$DurationMinutes = 60,
    [ValidateSet("Alternate", "Terminal1", "Terminal2")]
    [string]$TerminalMode = "Alternate",
    [ValidateSet("FullFlow", "Idle", "SwitchOnly", "CaptureOnly")]
    [string]$WorkloadMode = "FullFlow",
    [ValidateRange(1, 2)]
    [int]$InitialTerminal = 1,
    [ValidateRange(1, 3600)]
    [int]$CycleMinSeconds = 15,
    [ValidateRange(1, 3600)]
    [int]$CycleMaxSeconds = 30,
    [ValidateRange(1, 3600)]
    [int]$CaptureMinSeconds = 15,
    [ValidateRange(1, 3600)]
    [int]$CaptureMaxSeconds = 20,
    [ValidateRange(1, 3600)]
    [int]$SwitchIntervalSeconds = 15,
    [ValidateRange(0, 60000)]
    [int]$CaptureGapMs = 200,
    [ValidateRange(50, 60000)]
    [int]$LoopMinIntervalMs = 300,
    [ValidateRange(1, 3600)]
    [int]$MetricsIntervalSeconds = 60,
    [ValidateRange(0, 60000)]
    [int]$PreEndDrainMs = 500,
    [ValidateRange(0, 60000)]
    [int]$PostEndObserveMs = 1000,
    [ValidateRange(0, 10000)]
    [int]$PostEndGraceMs = 250,
    [ValidateRange(1, 10000)]
    [int]$CsvFlushBatchSize = 1000,
    [ValidateRange(1, 60000)]
    [int]$UiBlockWarningMs = 500,
    [ValidateRange(1, 100000)]
    [int]$AsyncRequestEveryNCycles = 1,
    [ValidateRange(0, 100000)]
    [int]$PostEndCaptureEveryNCycles = 1,
    [ValidateRange(1, 65535)]
    [int]$DllCallbackPort = 39091,
    [string]$SaveDir = (Join-Path $PSScriptRoot "full_flow_captures"),
    [string]$ResultsDir = (Join-Path $PSScriptRoot "stress_results"),
    [switch]$SkipPreview,
    [switch]$EnableOcr,
    [switch]$EnableNfc,
    [switch]$EnableIris,
    [switch]$EnableAuthorize,
    [switch]$ContinueCaptureOnStartFailure,
    [switch]$SaveRawCallbackJson,
    [switch]$RestartProxy,
    [switch]$VerboseCalls,
    [switch]$VerboseCallbacks,
    [switch]$ValidateOnly,
    [string]$AuthorizeIdNo = "TEST000001",
    [string]$AuthorizeDocType = "01",
    [string]$AuthorizeNationality = "CHN",
    [string]$AuthorizeName = "TEST",
    [string]$AuthorizeSex = "1",
    [string]$AuthorizeBirthday = "20000101",
    [string]$AuthorizeCardNo = "TEST"
)

$ErrorActionPreference = "Stop"
$scriptBoundParameters = @{}
foreach ($entry in $PSBoundParameters.GetEnumerator()) {
    $scriptBoundParameters[$entry.Key] = $entry.Value
}

function Restart-InX86PowerShell([hashtable]$BoundParameters) {
    if (-not [Environment]::Is64BitProcess) {
        return
    }

    $x86PowerShell = Join-Path $env:WINDIR "SysWOW64\WindowsPowerShell\v1.0\powershell.exe"
    if (-not (Test-Path -LiteralPath $x86PowerShell -PathType Leaf)) {
        throw "x86 PowerShell was not found: $x86PowerShell"
    }

    $forwardArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-STA", "-File", $PSCommandPath)
    foreach ($entry in $BoundParameters.GetEnumerator()) {
        $value = $entry.Value
        if ($value -is [System.Management.Automation.SwitchParameter]) {
            if ($value.IsPresent) {
                $forwardArgs += "-$($entry.Key)"
            }
            continue
        }
        $forwardArgs += "-$($entry.Key)"
        $forwardArgs += [string]$value
    }

    Write-Host "Restarting in x86 STA PowerShell to load the Win32 DLL..."
    & $x86PowerShell @forwardArgs
    exit $LASTEXITCODE
}

Restart-InX86PowerShell $scriptBoundParameters

if ([Environment]::Is64BitProcess) {
    throw "The test host must be an x86 process."
}
if ([Threading.Thread]::CurrentThread.ApartmentState -ne [Threading.ApartmentState]::STA) {
    throw "The test host must run in an STA thread."
}
if ($CycleMinSeconds -gt $CycleMaxSeconds) {
    throw "CycleMinSeconds must not exceed CycleMaxSeconds."
}
if ($CaptureMinSeconds -gt $CaptureMaxSeconds) {
    throw "CaptureMinSeconds must not exceed CaptureMaxSeconds."
}
if ($DurationMinutes -le 0 -and -not $ValidateOnly) {
    throw "DurationMinutes must be greater than zero."
}

function Resolve-ExistingFile([string]$Path, [string]$Description) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Description does not exist: $Path"
    }
    return (Resolve-Path -LiteralPath $Path).Path
}

function Get-PeMachine([string]$Path) {
    $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
    try {
        $reader = New-Object IO.BinaryReader($stream)
        try {
            if ($reader.ReadUInt16() -ne 0x5A4D) {
                throw "The file is not a valid PE image: $Path"
            }
            $stream.Position = 0x3C
            $peOffset = $reader.ReadInt32()
            $stream.Position = $peOffset
            if ($reader.ReadUInt32() -ne 0x00004550) {
                throw "The file is not a valid PE image: $Path"
            }
            return $reader.ReadUInt16()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

$DllPath = Resolve-ExistingFile $DllPath "x86 DLL"
$ProxyExePath = Resolve-ExistingFile $ProxyExePath "x64 Proxy"
$script:validatedProxyExePath = $ProxyExePath
$dllMachine = Get-PeMachine $DllPath
$proxyMachine = Get-PeMachine $ProxyExePath
if ($dllMachine -ne 0x014C) {
    throw ("The DLL is not x86 (Machine=0x{0:X4}): {1}" -f $dllMachine, $DllPath)
}
if ($proxyMachine -ne 0x8664) {
    throw ("The Proxy is not x64 (Machine=0x{0:X4}): {1}" -f $proxyMachine, $ProxyExePath)
}

$escapedDllPath = $DllPath.Replace('"', '""')
$nativeSource = @'
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public sealed class HzcyFullFlowCallbackRecord
{
    public long ReceivedAtUtcTicks;
    public int ThreadId;
    public string Json;
}

public static class HzcyFullFlowNative
{
    private const string DllName = @"__DLL_PATH__";

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate void NativeEventCallback(IntPtr eventJson);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_InitSdk();
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_ReleaseSdk();
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_RegisterEventCallback(NativeEventCallback callback);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_SwitchTerminal(int terminalIndex);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_StartProcess(IntPtr saveDir);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_EndProcess();
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_StartCameraPreview(IntPtr parentHwnd);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_StopCameraPreview();
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_StartFingerprintPreview(IntPtr parentHwnd);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_StopFingerprintPreview();
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_CaptureCameraImage(IntPtr savePath);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_CaptureFingerprintImage(IntPtr savePath, IntPtr savePathHk);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_CaptureIrisImage(IntPtr saveDir);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_RequestOCR(IntPtr saveDir);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_RequestNfcCard(IntPtr saveDir);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    private static extern int HZCYKJTHardWare_RequestAuthorize(IntPtr idNo, IntPtr docType,
        IntPtr nationality, IntPtr name, IntPtr sex, IntPtr birthday, IntPtr cardNo);

    private static readonly ConcurrentQueue<HzcyFullFlowCallbackRecord> CallbackQueue =
        new ConcurrentQueue<HzcyFullFlowCallbackRecord>();
    private static readonly NativeEventCallback CallbackRoot = OnEvent;

    private static IntPtr Utf8(string value)
    {
        if (value == null) return IntPtr.Zero;
        byte[] bytes = Encoding.UTF8.GetBytes(value + "\0");
        IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return ptr;
    }

    private static string ReadUtf8(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return String.Empty;
        int length = 0;
        while (Marshal.ReadByte(ptr, length) != 0) length++;
        byte[] bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);
        return Encoding.UTF8.GetString(bytes);
    }

    private static int WithUtf8(string value, Func<IntPtr, int> action)
    {
        IntPtr ptr = Utf8(value);
        try { return action(ptr); }
        finally { if (ptr != IntPtr.Zero) Marshal.FreeHGlobal(ptr); }
    }

    private static void OnEvent(IntPtr eventJson)
    {
        CallbackQueue.Enqueue(new HzcyFullFlowCallbackRecord {
            ReceivedAtUtcTicks = DateTime.UtcNow.Ticks,
            ThreadId = Thread.CurrentThread.ManagedThreadId,
            Json = ReadUtf8(eventJson)
        });
    }

    public static int InitSdk() { return HZCYKJTHardWare_InitSdk(); }
    public static int ReleaseSdk() { return HZCYKJTHardWare_ReleaseSdk(); }
    public static int RegisterCallback() { return HZCYKJTHardWare_RegisterEventCallback(CallbackRoot); }
    public static int SwitchTerminal(int terminalIndex) { return HZCYKJTHardWare_SwitchTerminal(terminalIndex); }
    public static int StartProcess(string saveDir) { return WithUtf8(saveDir, HZCYKJTHardWare_StartProcess); }
    public static int EndProcess() { return HZCYKJTHardWare_EndProcess(); }
    public static int StartCameraPreview(IntPtr hwnd) { return HZCYKJTHardWare_StartCameraPreview(hwnd); }
    public static int StopCameraPreview() { return HZCYKJTHardWare_StopCameraPreview(); }
    public static int StartFingerprintPreview(IntPtr hwnd) { return HZCYKJTHardWare_StartFingerprintPreview(hwnd); }
    public static int StopFingerprintPreview() { return HZCYKJTHardWare_StopFingerprintPreview(); }
    public static int CaptureCameraImage(string path) { return WithUtf8(path, HZCYKJTHardWare_CaptureCameraImage); }
    public static int CaptureFingerprintImage(string path, string pathHk)
    {
        IntPtr p1 = Utf8(path);
        IntPtr p2 = Utf8(pathHk);
        try { return HZCYKJTHardWare_CaptureFingerprintImage(p1, p2); }
        finally {
            if (p2 != IntPtr.Zero) Marshal.FreeHGlobal(p2);
            if (p1 != IntPtr.Zero) Marshal.FreeHGlobal(p1);
        }
    }
    public static int CaptureIrisImage(string saveDir) { return WithUtf8(saveDir, HZCYKJTHardWare_CaptureIrisImage); }
    public static int RequestOCR(string saveDir) { return WithUtf8(saveDir, HZCYKJTHardWare_RequestOCR); }
    public static int RequestNfcCard(string saveDir) { return WithUtf8(saveDir, HZCYKJTHardWare_RequestNfcCard); }
    public static int RequestAuthorize(string idNo, string docType, string nationality,
        string name, string sex, string birthday, string cardNo)
    {
        IntPtr[] p = new IntPtr[7];
        try {
            p[0] = Utf8(idNo); p[1] = Utf8(docType); p[2] = Utf8(nationality);
            p[3] = Utf8(name); p[4] = Utf8(sex); p[5] = Utf8(birthday); p[6] = Utf8(cardNo);
            return HZCYKJTHardWare_RequestAuthorize(p[0], p[1], p[2], p[3], p[4], p[5], p[6]);
        }
        finally {
            for (int i = p.Length - 1; i >= 0; i--) if (p[i] != IntPtr.Zero) Marshal.FreeHGlobal(p[i]);
        }
    }

    public static HzcyFullFlowCallbackRecord[] DrainCallbacks()
    {
        var result = new List<HzcyFullFlowCallbackRecord>();
        HzcyFullFlowCallbackRecord item;
        while (CallbackQueue.TryDequeue(out item)) result.Add(item);
        return result.ToArray();
    }
}
'@
$nativeSource = $nativeSource.Replace("__DLL_PATH__", $escapedDllPath)
Add-Type -TypeDefinition $nativeSource -Language CSharp

$dllHash = (Get-FileHash -LiteralPath $DllPath -Algorithm SHA256).Hash
$proxyHash = (Get-FileHash -LiteralPath $ProxyExePath -Algorithm SHA256).Hash

if ($ValidateOnly) {
    Write-Host "Validation passed: test host=x86/STA, DLL=x86, Proxy=x64, all P/Invoke declarations compiled." -ForegroundColor Green
    Write-Host "DLL:   $DllPath"
    Write-Host "Proxy: $ProxyExePath"
    exit 0
}

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$SaveDir = [IO.Path]::GetFullPath($SaveDir)
$ResultsDir = [IO.Path]::GetFullPath($ResultsDir)
[IO.Directory]::CreateDirectory($SaveDir) | Out-Null
[IO.Directory]::CreateDirectory($ResultsDir) | Out-Null
$faceSavePath = Join-Path $SaveDir "face.jpg"
$fingerprintSavePath = Join-Path $SaveDir "fingerprint.bmp"
$fingerprintHkSavePath = Join-Path $SaveDir "fingerprint_hk.bmp"

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$resultPrefix = if ($WorkloadMode -eq "FullFlow") { "full_flow" } else { "handle_$($WorkloadMode.ToLowerInvariant())" }
$callsCsv = Join-Path $ResultsDir "${resultPrefix}_calls_$stamp.csv"
$cyclesCsv = Join-Path $ResultsDir "${resultPrefix}_cycles_$stamp.csv"
$callbacksCsv = Join-Path $ResultsDir "${resultPrefix}_callbacks_$stamp.csv"
$metricsCsv = Join-Path $ResultsDir "${resultPrefix}_metrics_$stamp.csv"
$summaryCsv = Join-Path $ResultsDir "${resultPrefix}_summary_$stamp.csv"

$script:buffers = @{
    Calls = New-Object 'System.Collections.Generic.List[object]'
    Cycles = New-Object 'System.Collections.Generic.List[object]'
    Callbacks = New-Object 'System.Collections.Generic.List[object]'
    Metrics = New-Object 'System.Collections.Generic.List[object]'
}
$script:csvPaths = @{ Calls=$callsCsv; Cycles=$cyclesCsv; Callbacks=$callbacksCsv; Metrics=$metricsCsv }

function Flush-CsvBuffer([string]$Name) {
    $buffer = $script:buffers[$Name]
    if ($null -eq $buffer -or $buffer.Count -eq 0) { return }
    $rows = @($buffer.ToArray())
    $path = $script:csvPaths[$Name]
    if (Test-Path -LiteralPath $path) {
        $rows | Export-Csv -LiteralPath $path -NoTypeInformation -Encoding UTF8 -Append
    }
    else {
        $rows | Export-Csv -LiteralPath $path -NoTypeInformation -Encoding UTF8
    }
    $buffer.Clear()
}

function Add-CsvRow([string]$Name, [object]$Row) {
    $script:buffers[$Name].Add($Row)
    $flushLimit = switch ($Name) {
        "Calls" { $CsvFlushBatchSize }
        "Cycles" { 10 }
        "Callbacks" { 50 }
        "Metrics" { 1 }
        default { $CsvFlushBatchSize }
    }
    if ($script:buffers[$Name].Count -ge $flushLimit) {
        Flush-CsvBuffer $Name
    }
}

function Flush-AllCsvBuffers {
    foreach ($name in @("Calls", "Cycles", "Callbacks", "Metrics")) {
        Flush-CsvBuffer $name
    }
}

function New-DurationList {
    return New-Object 'System.Collections.Generic.List[int]'
}

$script:durationLists = @{
    FaceAll = New-DurationList; FingerAll = New-DurationList
    FaceT1 = New-DurationList; FaceT2 = New-DurationList
    FingerT1 = New-DurationList; FingerT2 = New-DurationList
}
$script:stats = @{
    CallTotal=0; CallFailures=0; UiBlockWarnings=0
    CycleTotal=0; CycleFailures=0
    FaceTotal=0; FaceOk=0; FaceFail=0
    FingerTotal=0; FingerOk=0; FingerFail=0
    AsyncTotal=0; AsyncAccepted=0; AsyncRejected=0
    CallbackTotal=0; CallbackErrors=0; PostEndPushCallbacks=0
}
$script:callSequence = 0
$script:callbackSequence = 0
$script:currentCycleId = 0
$script:currentTerminal = 0
$script:currentPhase = "Setup"
$script:lastEndCompletedUtcTicks = 0L
$script:stopRequested = $false
$script:sdkInitialized = $false
$script:callbackRegistered = $false
$script:cameraPreviewStarted = $false
$script:fingerprintPreviewStarted = $false
$script:cycleNeedsEnd = $false
$script:fatalError = ""
$script:proxyProcess = $null
$script:actualProxyPath = $ProxyExePath
$script:actualProxyHash = $proxyHash
$script:cpuState = @{}
$script:nextMetricsAt = [DateTime]::MinValue

function Invoke-DllCall {
    param(
        [Parameter(Mandatory=$true)][string]$Operation,
        [Parameter(Mandatory=$true)][scriptblock]$Action
    )
    $startedAt = Get-Date
    $timer = [Diagnostics.Stopwatch]::StartNew()
    $returnCode = -99999
    $exceptionText = ""
    try {
        $returnCode = [int](& $Action)
    }
    catch {
        $exceptionText = $_.Exception.ToString()
    }
    finally {
        $timer.Stop()
    }

    $script:callSequence++
    $success = ($exceptionText.Length -eq 0 -and $returnCode -eq 1)
    $uiWarning = ($timer.ElapsedMilliseconds -ge $UiBlockWarningMs)
    $script:stats.CallTotal++
    if (-not $success) { $script:stats.CallFailures++ }
    if ($uiWarning) { $script:stats.UiBlockWarnings++ }

    $row = [pscustomobject]@{
        Sequence = $script:callSequence
        CycleId = $script:currentCycleId
        TerminalIndex = $script:currentTerminal
        Phase = $script:currentPhase
        StartedAt = $startedAt.ToString("o")
        Operation = $Operation
        ReturnCode = $returnCode
        Success = $success
        DurationMs = $timer.ElapsedMilliseconds
        UiBlockWarning = $uiWarning
        Exception = $exceptionText
        ManagedThreadId = [Threading.Thread]::CurrentThread.ManagedThreadId
        ApartmentState = [Threading.Thread]::CurrentThread.ApartmentState.ToString()
    }
    Add-CsvRow "Calls" $row

    if ($VerboseCalls -or -not $success -or $uiWarning -or $Operation -notmatch '^Capture') {
        $color = if ($success) { "Gray" } else { "Red" }
        Write-Host ("[{0:HH:mm:ss.fff}] C{1} T{2} {3}: rc={4}, {5}ms" -f $startedAt,
            $script:currentCycleId, $script:currentTerminal, $Operation, $returnCode, $timer.ElapsedMilliseconds) -ForegroundColor $color
    }
    return $row
}

function Get-JsonValue($Object, [string]$Name, $DefaultValue = "") {
    if ($null -eq $Object) { return $DefaultValue }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property -or $null -eq $property.Value) { return $DefaultValue }
    return $property.Value
}

function Drain-CallbackEvents {
    $items = [HzcyFullFlowNative]::DrainCallbacks()
    foreach ($item in $items) {
        $payload = $null
        $parseError = ""
        try { $payload = $item.Json | ConvertFrom-Json }
        catch { $parseError = $_.Exception.Message }

        $eventType = [int](Get-JsonValue $payload "event_type" 0)
        $status = [int](Get-JsonValue $payload "status" 0)
        $resourceType = [string](Get-JsonValue $payload "resource_type" "")
        $requestId = [string](Get-JsonValue $payload "request_id" "")
        $errorCode = [string](Get-JsonValue $payload "error_code" "")
        $isFailureEvent = $eventType -in @(1002,1003,1102,1203,1303,1402,1502,1602,1701,1803,1805,1807,1903,1999,2002)
        $isProcessPushEvent = $eventType -in @(1401,1402,1501,1502,1601,1602,1804,1805,1806,1807,1901,1902,1903,2001,2002) -or
            $resourceType -in @("face_image", "fingerprint_image", "ocr_document", "iris_image", "nfc_card", "plate_image", "authorization")
        $afterEndThreshold = $script:lastEndCompletedUtcTicks + ([long]$PostEndGraceMs * [TimeSpan]::TicksPerMillisecond)
        $isAfterEnd = $isProcessPushEvent -and $script:lastEndCompletedUtcTicks -gt 0 -and
            $item.ReceivedAtUtcTicks -ge $afterEndThreshold

        $script:callbackSequence++
        $script:stats.CallbackTotal++
        if ($isFailureEvent -or $parseError.Length -gt 0) { $script:stats.CallbackErrors++ }
        if ($isAfterEnd) { $script:stats.PostEndPushCallbacks++ }

        $receivedAtUtc = New-Object DateTime -ArgumentList ([long]$item.ReceivedAtUtcTicks), ([DateTimeKind]::Utc)
        $receivedAt = $receivedAtUtc.ToLocalTime()
        $rawJson = if ($SaveRawCallbackJson) { $item.Json } else { "" }
        Add-CsvRow "Callbacks" ([pscustomobject]@{
            Sequence = $script:callbackSequence
            ReceivedAt = $receivedAt.ToString("o")
            ObservedAt = (Get-Date).ToString("o")
            CycleId = $script:currentCycleId
            TerminalIndex = $script:currentTerminal
            Phase = $script:currentPhase
            EventType = $eventType
            Status = $status
            ResourceType = $resourceType
            RequestId = $requestId
            ErrorCode = $errorCode
            IsFailureEvent = $isFailureEvent
            IsProcessPushAfterEnd = $isAfterEnd
            CallbackThreadId = $item.ThreadId
            ParseError = $parseError
            RawJson = $rawJson
        })
        if ($VerboseCallbacks -or $isFailureEvent -or $isAfterEnd -or $parseError.Length -gt 0) {
            $color = if ($isFailureEvent -or $isAfterEnd -or $parseError.Length -gt 0) { "Yellow" } else { "DarkGray" }
            Write-Host ("[{0:HH:mm:ss.fff}] callback event={1}, resource={2}, request={3}, afterEnd={4}" -f
                $receivedAt, $eventType, $resourceType, $requestId, $isAfterEnd) -ForegroundColor $color
        }
    }
}

function Get-CpuPercent([Diagnostics.Process]$Process, [string]$Key) {
    if ($null -eq $Process -or $Process.HasExited) { return $null }
    $now = [DateTime]::UtcNow
    $cpuMs = $Process.TotalProcessorTime.TotalMilliseconds
    $previous = $script:cpuState[$Key]
    $script:cpuState[$Key] = [pscustomobject]@{ At=$now; CpuMs=$cpuMs }
    if ($null -eq $previous) { return $null }
    $wallMs = ($now - $previous.At).TotalMilliseconds
    if ($wallMs -le 0) { return $null }
    return [Math]::Round((($cpuMs - $previous.CpuMs) / $wallMs / [Environment]::ProcessorCount) * 100.0, 2)
}

function Add-ProcessMetric([string]$Role, [Diagnostics.Process]$Process, [string]$Path, [string]$Machine) {
    $timestamp = (Get-Date).ToString("o")
    if ($null -eq $Process -or $Process.HasExited) {
        Add-CsvRow "Metrics" ([pscustomobject]@{
            Timestamp=$timestamp; Role=$Role; ProcessId=""; Architecture=$Machine; Path=$Path; CpuPercent=""
            WorkingSetMB=""; PrivateMemoryMB=""; ThreadCount=""; HandleCount=""; GcTotalMemoryMB=""; DiskFreeGB=""
        })
        return
    }
    $Process.Refresh()
    $gcMemory = if ($Role -eq "TestHost") { [Math]::Round([GC]::GetTotalMemory($false) / 1MB, 2) } else { "" }
    $diskRoot = [IO.Path]::GetPathRoot($SaveDir)
    $drive = New-Object IO.DriveInfo($diskRoot)
    Add-CsvRow "Metrics" ([pscustomobject]@{
        Timestamp=$timestamp; Role=$Role; ProcessId=$Process.Id; Architecture=$Machine; Path=$Path
        CpuPercent=(Get-CpuPercent $Process $Role)
        WorkingSetMB=[Math]::Round($Process.WorkingSet64 / 1MB, 2)
        PrivateMemoryMB=[Math]::Round($Process.PrivateMemorySize64 / 1MB, 2)
        ThreadCount=$Process.Threads.Count
        HandleCount=$Process.HandleCount
        GcTotalMemoryMB=$gcMemory
        DiskFreeGB=[Math]::Round($drive.AvailableFreeSpace / 1GB, 2)
    })
}

function Sample-MetricsIfDue([switch]$Force) {
    if (-not $Force -and [DateTime]::UtcNow -lt $script:nextMetricsAt) { return }
    $script:nextMetricsAt = [DateTime]::UtcNow.AddSeconds($MetricsIntervalSeconds)
    $proxy = $script:proxyProcess
    if ($null -eq $proxy -or $proxy.HasExited) {
        $proxy = Get-Process -Name "HZCYKJTHardWare.Proxy" -ErrorAction SilentlyContinue | Select-Object -First 1
        $script:proxyProcess = $proxy
    }
    Add-ProcessMetric "Proxy" $proxy $script:actualProxyPath "x64"
    Add-ProcessMetric "TestHost" ([Diagnostics.Process]::GetCurrentProcess()) $PSHOME "x86"
}

function Wait-WithPump([int]$Milliseconds) {
    if ($Milliseconds -le 0) {
        [Windows.Forms.Application]::DoEvents()
        Drain-CallbackEvents
        Sample-MetricsIfDue
        return
    }
    $timer = [Diagnostics.Stopwatch]::StartNew()
    while ($timer.ElapsedMilliseconds -lt $Milliseconds -and -not $script:stopRequested) {
        [Windows.Forms.Application]::DoEvents()
        Drain-CallbackEvents
        Sample-MetricsIfDue
        $remaining = $Milliseconds - $timer.ElapsedMilliseconds
        Start-Sleep -Milliseconds ([Math]::Max(1, [Math]::Min(20, $remaining)))
    }
}

function Ensure-ExclusiveCallbackPort {
    $listeners = [Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners()
    if (@($listeners | Where-Object { $_.Port -eq $DllCallbackPort }).Count -gt 0) {
        throw "DLL callback port $DllCallbackPort is already in use. Stop the third-party client or other DLL test host; this script requires exclusive access."
    }
}

function Ensure-X64Proxy {
    $selectedProxyPath = $script:validatedProxyExePath
    $startedByScript = $false
    $running = @(Get-Process -Name "HZCYKJTHardWare.Proxy" -ErrorAction SilentlyContinue)
    if ($RestartProxy -and $running.Count -gt 0) {
        foreach ($processToStop in $running) {
            Write-Host "Stopping Proxy PID=$($processToStop.Id) to establish a fresh diagnostic baseline..." -ForegroundColor Yellow
            Stop-Process -Id $processToStop.Id -ErrorAction Stop
            if (-not $processToStop.WaitForExit(10000)) {
                throw "Proxy PID=$($processToStop.Id) did not exit within 10 seconds."
            }
        }
        $running = @()
    }
    if ($running.Count -gt 1) {
        throw "Multiple Proxy processes were found. Keep exactly one x64 Proxy process."
    }
    if ($running.Count -eq 0) {
        Write-Host "Proxy is not running. Starting the selected x64 build: $selectedProxyPath"
        $running = @(Start-Process -FilePath $selectedProxyPath -WorkingDirectory (Split-Path -Parent $selectedProxyPath) -WindowStyle Hidden -PassThru)
        $startedByScript = $true
        Start-Sleep -Milliseconds 1500
        if ($running[0].HasExited) {
            throw "The x64 Proxy exited immediately after startup. ExitCode=$($running[0].ExitCode)."
        }
    }
    $process = $running[0]
    $actualPath = $null
    try { $actualPath = $process.Path } catch { $actualPath = $null }
    if ([string]::IsNullOrWhiteSpace($actualPath)) {
        try { $actualPath = $process.StartInfo.FileName } catch { $actualPath = $null }
    }
    if ([string]::IsNullOrWhiteSpace($actualPath)) {
        try {
            $wmiProcess = Get-CimInstance -ClassName Win32_Process -Filter ("ProcessId = {0}" -f $process.Id) -ErrorAction Stop
            $actualPath = $wmiProcess.ExecutablePath
        }
        catch {
            $actualPath = $null
        }
    }
    if ([string]::IsNullOrWhiteSpace($actualPath) -and ($startedByScript -or $RestartProxy)) {
        # 32 位 PowerShell 可能无法反查 64 位进程模块路径。
        # RestartProxy 已先停止所有同名进程，随后只能从前面已校验的路径启动新实例。
        $actualPath = $script:validatedProxyExePath
        Write-Host "Using the validated Proxy path for the x86-to-x64 process check: $actualPath" -ForegroundColor Yellow
    }
    if ([string]::IsNullOrWhiteSpace($actualPath)) {
        throw "The running Proxy path cannot be read, so its architecture cannot be verified."
    }
    $actualMachine = Get-PeMachine $actualPath
    if ($actualMachine -ne 0x8664) {
        throw ("The running Proxy is not x64 (PID={0}, Machine=0x{1:X4}, Path={2})." -f $process.Id, $actualMachine, $actualPath)
    }
    $script:proxyProcess = $process
    $script:actualProxyPath = $actualPath
    $script:actualProxyHash = (Get-FileHash -LiteralPath $actualPath -Algorithm SHA256).Hash
    Write-Host "Confirmed x64 Proxy: PID=$($process.Id), Path=$actualPath" -ForegroundColor Green
}

function Get-Percentile([System.Collections.Generic.List[int]]$Values, [double]$Percentile) {
    if ($null -eq $Values -or $Values.Count -eq 0) { return "" }
    $sorted = @($Values.ToArray() | Sort-Object)
    $index = [Math]::Ceiling($Percentile * $sorted.Count) - 1
    $index = [Math]::Max(0, [Math]::Min($sorted.Count - 1, $index))
    return $sorted[$index]
}

function Add-CaptureDuration([string]$Kind, [int]$Terminal, [long]$DurationMs) {
    $script:durationLists["${Kind}All"].Add([int]$DurationMs)
    $script:durationLists["${Kind}T$Terminal"].Add([int]$DurationMs)
}

function Get-CycleTerminal([int]$CycleId) {
    switch ($TerminalMode) {
        "Terminal1" { return 1 }
        "Terminal2" { return 2 }
        default {
            if ((($CycleId - 1) % 2) -eq 0) { return $InitialTerminal }
            return (3 - $InitialTerminal)
        }
    }
}

function Invoke-OptionalAsyncRequests([int]$CycleId) {
    $requested = 0
    $accepted = 0
    if (($CycleId % $AsyncRequestEveryNCycles) -ne 0) {
        return [pscustomobject]@{ Requested=0; Accepted=0 }
    }
    $operations = New-Object 'System.Collections.Generic.List[object]'
    if ($EnableOcr) { $operations.Add([pscustomobject]@{ Name="RequestOCR"; Action={ [HzcyFullFlowNative]::RequestOCR($SaveDir) } }) }
    if ($EnableNfc) { $operations.Add([pscustomobject]@{ Name="RequestNfcCard"; Action={ [HzcyFullFlowNative]::RequestNfcCard($SaveDir) } }) }
    if ($EnableIris) { $operations.Add([pscustomobject]@{ Name="CaptureIrisImage"; Action={ [HzcyFullFlowNative]::CaptureIrisImage($SaveDir) } }) }
    if ($EnableAuthorize) {
        $operations.Add([pscustomobject]@{ Name="RequestAuthorize"; Action={
            [HzcyFullFlowNative]::RequestAuthorize($AuthorizeIdNo, $AuthorizeDocType, $AuthorizeNationality,
                $AuthorizeName, $AuthorizeSex, $AuthorizeBirthday, $AuthorizeCardNo)
        } })
    }
    foreach ($operation in $operations) {
        $result = Invoke-DllCall $operation.Name $operation.Action
        $requested++
        if ($result.Success) { $accepted++ }
        Wait-WithPump 50
    }
    $script:stats.AsyncTotal += $requested
    $script:stats.AsyncAccepted += $accepted
    $script:stats.AsyncRejected += ($requested - $accepted)
    return [pscustomobject]@{ Requested=$requested; Accepted=$accepted }
}

function Invoke-CaptureBurst([DateTime]$CaptureDeadline, [DateTime]$TestDeadline) {
    $faceTotal = 0; $faceOk = 0; $fingerTotal = 0; $fingerOk = 0
    while ((Get-Date) -lt $CaptureDeadline -and (Get-Date) -lt $TestDeadline -and -not $script:stopRequested) {
        $loopTimer = [Diagnostics.Stopwatch]::StartNew()

        $face = Invoke-DllCall "CaptureCameraImage" { [HzcyFullFlowNative]::CaptureCameraImage($faceSavePath) }
        $faceTotal++; $script:stats.FaceTotal++
        Add-CaptureDuration "Face" $script:currentTerminal $face.DurationMs
        if ($face.Success) { $faceOk++; $script:stats.FaceOk++ }
        else { $script:stats.FaceFail++ }
        Wait-WithPump $CaptureGapMs
        if ($script:stopRequested -or (Get-Date) -ge $TestDeadline) { break }

        $finger = Invoke-DllCall "CaptureFingerprintImage" {
            [HzcyFullFlowNative]::CaptureFingerprintImage($fingerprintSavePath, $fingerprintHkSavePath)
        }
        $fingerTotal++; $script:stats.FingerTotal++
        Add-CaptureDuration "Finger" $script:currentTerminal $finger.DurationMs
        if ($finger.Success) { $fingerOk++; $script:stats.FingerOk++ }
        else { $script:stats.FingerFail++ }

        $loopTimer.Stop()
        $remainingLoopMs = $LoopMinIntervalMs - [int]$loopTimer.ElapsedMilliseconds
        if ($remainingLoopMs -gt 0) { Wait-WithPump $remainingLoopMs }
        else { Wait-WithPump 0 }
    }
    return [pscustomobject]@{
        FaceTotal=$faceTotal; FaceOk=$faceOk
        FingerTotal=$fingerTotal; FingerOk=$fingerOk
    }
}

$cancelHandler = [ConsoleCancelEventHandler]{
    param($sender, $eventArgs)
    $eventArgs.Cancel = $true
    $script:stopRequested = $true
    Write-Host "Stop requested. The current process will be ended and the DLL will be released." -ForegroundColor Yellow
}
[Console]::add_CancelKeyPress($cancelHandler)

$previewForm = $null
$testStartedAt = Get-Date
$deadline = $testStartedAt.AddMinutes($DurationMinutes)

try {
    Ensure-ExclusiveCallbackPort
    Ensure-X64Proxy
    Sample-MetricsIfDue -Force

    $script:currentPhase = "Setup"
    $init = Invoke-DllCall "InitSdk" { [HzcyFullFlowNative]::InitSdk() }
    if (-not $init.Success) { throw "InitSdk failed, rc=$($init.ReturnCode)." }
    $script:sdkInitialized = $true

    $register = Invoke-DllCall "RegisterEventCallback" { [HzcyFullFlowNative]::RegisterCallback() }
    if (-not $register.Success) { throw "RegisterEventCallback failed, rc=$($register.ReturnCode)." }
    $script:callbackRegistered = $true

    $setupTerminal = Get-CycleTerminal 1
    $script:currentTerminal = $setupTerminal
    $switchSetup = Invoke-DllCall "SwitchTerminal.Setup" { [HzcyFullFlowNative]::SwitchTerminal($setupTerminal) }
    if (-not $switchSetup.Success) { throw "Initial terminal switch failed, rc=$($switchSetup.ReturnCode)." }

    if (-not $SkipPreview) {
        $previewForm = New-Object Windows.Forms.Form
        $previewForm.Text = "HZCY full-flow endurance test (close to stop)"
        $previewForm.Width = 1040
        $previewForm.Height = 520
        $previewForm.StartPosition = [Windows.Forms.FormStartPosition]::CenterScreen
        $previewForm.Add_FormClosing({ $script:stopRequested = $true })
        $cameraPanel = New-Object Windows.Forms.Panel
        $cameraPanel.Dock = [Windows.Forms.DockStyle]::Left
        $cameraPanel.Width = 510
        $cameraPanel.BackColor = [Drawing.Color]::Black
        $fingerPanel = New-Object Windows.Forms.Panel
        $fingerPanel.Dock = [Windows.Forms.DockStyle]::Fill
        $fingerPanel.BackColor = [Drawing.Color]::DarkSlateGray
        $previewForm.Controls.Add($fingerPanel)
        $previewForm.Controls.Add($cameraPanel)
        $previewForm.Show()
        [Windows.Forms.Application]::DoEvents()

        $cameraPreview = Invoke-DllCall "StartCameraPreview" { [HzcyFullFlowNative]::StartCameraPreview($cameraPanel.Handle) }
        if (-not $cameraPreview.Success) { throw "Camera preview failed to start, rc=$($cameraPreview.ReturnCode)." }
        $script:cameraPreviewStarted = $true
        $fingerPreview = Invoke-DllCall "StartFingerprintPreview" { [HzcyFullFlowNative]::StartFingerprintPreview($fingerPanel.Handle) }
        if (-not $fingerPreview.Success) { throw "Fingerprint preview failed to start, rc=$($fingerPreview.ReturnCode)." }
        $script:fingerprintPreviewStarted = $true
    }

    Write-Host "Full-flow test started: x86 DLL + x64 Proxy. Ctrl+C performs a safe stop." -ForegroundColor Cyan
    Write-Host "Exclusive test: do not run the third-party client against this DLL/Proxy/terminal at the same time." -ForegroundColor Yellow

    if ($WorkloadMode -eq "Idle") {
        Write-Host "Handle isolation mode: initialized idle baseline." -ForegroundColor Cyan
        while ((Get-Date) -lt $deadline -and -not $script:stopRequested) {
            Wait-WithPump 250
        }
    }
    elseif ($WorkloadMode -eq "SwitchOnly") {
        Write-Host "Handle isolation mode: same/selected terminal Switch-only, interval=${SwitchIntervalSeconds}s." -ForegroundColor Cyan
        while ((Get-Date) -lt $deadline -and -not $script:stopRequested) {
            $script:currentCycleId++
            $script:stats.CycleTotal++
            $script:currentTerminal = Get-CycleTerminal $script:currentCycleId
            $cycleStartedAt = Get-Date
            $callbackBefore = $script:stats.CallbackTotal
            $callbackErrorsBefore = $script:stats.CallbackErrors

            $script:currentPhase = "Switch"
            $switchResult = Invoke-DllCall "SwitchTerminal" { [HzcyFullFlowNative]::SwitchTerminal($script:currentTerminal) }
            $cycleFailed = -not $switchResult.Success
            if ($cycleFailed) { $script:stats.CycleFailures++ }

            $elapsedMs = [int]((Get-Date) - $cycleStartedAt).TotalMilliseconds
            $targetMs = $SwitchIntervalSeconds * 1000
            if ($elapsedMs -lt $targetMs) {
                $script:currentPhase = "InterSwitchWait"
                Wait-WithPump ($targetMs - $elapsedMs)
            }
            Drain-CallbackEvents
            Sample-MetricsIfDue
            $cycleEndedAt = Get-Date
            Add-CsvRow "Cycles" ([pscustomobject]@{
                CycleId=$script:currentCycleId; TerminalIndex=$script:currentTerminal
                StartedAt=$cycleStartedAt.ToString("o"); EndedAt=$cycleEndedAt.ToString("o")
                TargetCycleSeconds=$SwitchIntervalSeconds; TargetCaptureSeconds=0
                ActualDurationMs=[int]($cycleEndedAt - $cycleStartedAt).TotalMilliseconds
                SwitchReturnCode=$switchResult.ReturnCode; SwitchDurationMs=$switchResult.DurationMs
                StartReturnCode=""; StartDurationMs=""; EndReturnCode=""; EndDurationMs=""
                FaceTotal=0; FaceOk=0; FaceFail=0; FingerTotal=0; FingerOk=0; FingerFail=0
                CallbackCount=($script:stats.CallbackTotal-$callbackBefore)
                CallbackErrors=($script:stats.CallbackErrors-$callbackErrorsBefore)
                Result=if ($cycleFailed) { "FAILED" } else { "PASSED" }
                Note="Switch-only diagnostic"
            })
        }
    }
    elseif ($WorkloadMode -eq "CaptureOnly") {
        Write-Host "Handle isolation mode: fixed-terminal Start/Capture/End without per-cycle Switch." -ForegroundColor Cyan
        while ((Get-Date) -lt $deadline -and -not $script:stopRequested) {
            $script:currentCycleId++
            $script:stats.CycleTotal++
            $cycleStartedAt = Get-Date
            $cycleTargetSeconds = Get-Random -Minimum $CycleMinSeconds -Maximum ($CycleMaxSeconds + 1)
            $captureTargetSeconds = Get-Random -Minimum $CaptureMinSeconds -Maximum ($CaptureMaxSeconds + 1)
            $callbackBefore = $script:stats.CallbackTotal
            $callbackErrorsBefore = $script:stats.CallbackErrors
            $postEndBefore = $script:stats.PostEndPushCallbacks
            $cycleFailed = $false
            $cycleNote = ""
            $script:lastEndCompletedUtcTicks = 0L

            $script:currentPhase = "Start"
            $startResult = Invoke-DllCall "StartProcess" { [HzcyFullFlowNative]::StartProcess($SaveDir) }
            $script:cycleNeedsEnd = $true
            if (-not $startResult.Success) { $cycleFailed = $true; $cycleNote += "StartProcess failed;" }

            $capture = [pscustomobject]@{ FaceTotal=0; FaceOk=0; FingerTotal=0; FingerOk=0 }
            if ($startResult.Success -or $ContinueCaptureOnStartFailure) {
                $script:currentPhase = if ($startResult.Success) { "Capture" } else { "CaptureWithoutStart" }
                $capture = Invoke-CaptureBurst ((Get-Date).AddSeconds($captureTargetSeconds)) $deadline
                if ($capture.FaceOk -ne $capture.FaceTotal -or $capture.FingerOk -ne $capture.FingerTotal) {
                    $cycleFailed = $true
                    $cycleNote += "Capture failed;"
                }
            }

            $script:currentPhase = "PreEndDrain"
            Wait-WithPump $PreEndDrainMs
            $script:currentPhase = "End"
            $endResult = Invoke-DllCall "EndProcess" { [HzcyFullFlowNative]::EndProcess() }
            $script:cycleNeedsEnd = $false
            $script:lastEndCompletedUtcTicks = [DateTime]::UtcNow.Ticks
            if (-not $endResult.Success) { $cycleFailed = $true; $cycleNote += "EndProcess failed;" }

            $script:currentPhase = "PostEndObserve"
            Wait-WithPump $PostEndObserveMs
            $elapsedMs = [int]((Get-Date) - $cycleStartedAt).TotalMilliseconds
            $targetMs = $cycleTargetSeconds * 1000
            if ($elapsedMs -lt $targetMs) {
                $script:currentPhase = "InterCycleWait"
                Wait-WithPump ($targetMs - $elapsedMs)
            }
            Drain-CallbackEvents
            Sample-MetricsIfDue

            $cycleCallbacks = $script:stats.CallbackTotal - $callbackBefore
            $cycleCallbackErrors = $script:stats.CallbackErrors - $callbackErrorsBefore
            $cyclePostEnd = $script:stats.PostEndPushCallbacks - $postEndBefore
            if ($cycleCallbackErrors -gt 0 -or $cyclePostEnd -gt 0) { $cycleFailed = $true }
            if ($cycleFailed) { $script:stats.CycleFailures++ }
            $cycleEndedAt = Get-Date
            Add-CsvRow "Cycles" ([pscustomobject]@{
                CycleId=$script:currentCycleId; TerminalIndex=$script:currentTerminal
                StartedAt=$cycleStartedAt.ToString("o"); EndedAt=$cycleEndedAt.ToString("o")
                TargetCycleSeconds=$cycleTargetSeconds; TargetCaptureSeconds=$captureTargetSeconds
                ActualDurationMs=[int]($cycleEndedAt - $cycleStartedAt).TotalMilliseconds
                SwitchReturnCode=""; SwitchDurationMs=""
                StartReturnCode=$startResult.ReturnCode; StartDurationMs=$startResult.DurationMs
                EndReturnCode=$endResult.ReturnCode; EndDurationMs=$endResult.DurationMs
                FaceTotal=$capture.FaceTotal; FaceOk=$capture.FaceOk; FaceFail=($capture.FaceTotal-$capture.FaceOk)
                FingerTotal=$capture.FingerTotal; FingerOk=$capture.FingerOk; FingerFail=($capture.FingerTotal-$capture.FingerOk)
                CallbackCount=$cycleCallbacks; CallbackErrors=$cycleCallbackErrors
                ProcessPushCallbacksAfterEnd=$cyclePostEnd
                Result=if ($cycleFailed) { "FAILED" } else { "PASSED" }
                Note=$cycleNote
            })
        }
    }
    else {
    while ((Get-Date) -lt $deadline -and -not $script:stopRequested) {
        $script:currentCycleId++
        $script:stats.CycleTotal++
        $script:currentTerminal = Get-CycleTerminal $script:currentCycleId
        $cycleStartedAt = Get-Date
        $cycleTargetSeconds = Get-Random -Minimum $CycleMinSeconds -Maximum ($CycleMaxSeconds + 1)
        $captureTargetSeconds = Get-Random -Minimum $CaptureMinSeconds -Maximum ($CaptureMaxSeconds + 1)
        $faceTotal = 0; $faceOk = 0; $fingerTotal = 0; $fingerOk = 0
        $asyncRequested = 0; $asyncAccepted = 0
        $callbackBefore = $script:stats.CallbackTotal
        $callbackErrorsBefore = $script:stats.CallbackErrors
        $postEndBefore = $script:stats.PostEndPushCallbacks
        $postEndFaceReturnCode = ""; $postEndFingerReturnCode = ""
        $postEndFaceSuccess = ""; $postEndFingerSuccess = ""
        $cycleFailed = $false
        $cycleNote = ""

        $script:currentPhase = "Switch"
        $switchResult = Invoke-DllCall "SwitchTerminal" { [HzcyFullFlowNative]::SwitchTerminal($script:currentTerminal) }
        if (-not $switchResult.Success) { $cycleFailed = $true; $cycleNote += "SwitchTerminal failed;" }
        Drain-CallbackEvents
        $script:lastEndCompletedUtcTicks = 0L

        $script:currentPhase = "Start"
        $startResult = Invoke-DllCall "StartProcess" { [HzcyFullFlowNative]::StartProcess($SaveDir) }
        $script:cycleNeedsEnd = $true
        if (-not $startResult.Success) { $cycleFailed = $true; $cycleNote += "StartProcess failed;" }

        if ($startResult.Success) {
            $script:currentPhase = "AsyncSubmit"
            $asyncResult = Invoke-OptionalAsyncRequests $script:currentCycleId
            $asyncRequested = $asyncResult.Requested
            $asyncAccepted = $asyncResult.Accepted
            if ($asyncAccepted -ne $asyncRequested) { $cycleFailed = $true; $cycleNote += "Async request rejected;" }
        }

        if ($startResult.Success -or $ContinueCaptureOnStartFailure) {
            $script:currentPhase = if ($startResult.Success) { "Capture" } else { "CaptureWithoutStart" }
            $captureDeadline = (Get-Date).AddSeconds($captureTargetSeconds)
            while ((Get-Date) -lt $captureDeadline -and (Get-Date) -lt $deadline -and -not $script:stopRequested) {
                $loopTimer = [Diagnostics.Stopwatch]::StartNew()

                $face = Invoke-DllCall "CaptureCameraImage" { [HzcyFullFlowNative]::CaptureCameraImage($faceSavePath) }
                $faceTotal++; $script:stats.FaceTotal++
                Add-CaptureDuration "Face" $script:currentTerminal $face.DurationMs
                if ($face.Success) { $faceOk++; $script:stats.FaceOk++ }
                else { $script:stats.FaceFail++; $cycleFailed = $true }
                Wait-WithPump $CaptureGapMs
                if ($script:stopRequested -or (Get-Date) -ge $deadline) { break }

                $finger = Invoke-DllCall "CaptureFingerprintImage" {
                    [HzcyFullFlowNative]::CaptureFingerprintImage($fingerprintSavePath, $fingerprintHkSavePath)
                }
                $fingerTotal++; $script:stats.FingerTotal++
                Add-CaptureDuration "Finger" $script:currentTerminal $finger.DurationMs
                if ($finger.Success) { $fingerOk++; $script:stats.FingerOk++ }
                else { $script:stats.FingerFail++; $cycleFailed = $true }

                $loopTimer.Stop()
                $remainingLoopMs = $LoopMinIntervalMs - [int]$loopTimer.ElapsedMilliseconds
                if ($remainingLoopMs -gt 0) { Wait-WithPump $remainingLoopMs }
                else { Wait-WithPump 0 }
            }
        }
        else {
            $cycleNote += "Captures skipped after Start failure;"
        }

        $script:currentPhase = "PreEndDrain"
        Wait-WithPump $PreEndDrainMs

        $script:currentPhase = "End"
        $endResult = Invoke-DllCall "EndProcess" { [HzcyFullFlowNative]::EndProcess() }
        $script:cycleNeedsEnd = $false
        $script:lastEndCompletedUtcTicks = [DateTime]::UtcNow.Ticks
        if (-not $endResult.Success) { $cycleFailed = $true; $cycleNote += "EndProcess failed;" }

        $script:currentPhase = "PostEndObserve"
        Wait-WithPump $PostEndGraceMs
        if ($PostEndCaptureEveryNCycles -gt 0 -and
            ($script:currentCycleId % $PostEndCaptureEveryNCycles) -eq 0 -and
            -not $script:stopRequested -and (Get-Date) -lt $deadline) {
            $script:currentPhase = "PostEndCapture"
            $postEndFace = Invoke-DllCall "CaptureCameraImage.PostEnd" { [HzcyFullFlowNative]::CaptureCameraImage($faceSavePath) }
            $postEndFaceReturnCode = $postEndFace.ReturnCode
            $postEndFaceSuccess = $postEndFace.Success
            $faceTotal++; $script:stats.FaceTotal++
            Add-CaptureDuration "Face" $script:currentTerminal $postEndFace.DurationMs
            if ($postEndFace.Success) { $faceOk++; $script:stats.FaceOk++ }
            else { $script:stats.FaceFail++; $cycleFailed = $true; $cycleNote += "Post-End face capture failed;" }
            Wait-WithPump $CaptureGapMs

            if (-not $script:stopRequested -and (Get-Date) -lt $deadline) {
                $postEndFinger = Invoke-DllCall "CaptureFingerprintImage.PostEnd" {
                    [HzcyFullFlowNative]::CaptureFingerprintImage($fingerprintSavePath, $fingerprintHkSavePath)
                }
                $postEndFingerReturnCode = $postEndFinger.ReturnCode
                $postEndFingerSuccess = $postEndFinger.Success
                $fingerTotal++; $script:stats.FingerTotal++
                Add-CaptureDuration "Finger" $script:currentTerminal $postEndFinger.DurationMs
                if ($postEndFinger.Success) { $fingerOk++; $script:stats.FingerOk++ }
                else { $script:stats.FingerFail++; $cycleFailed = $true; $cycleNote += "Post-End fingerprint capture failed;" }
            }
        }
        $script:currentPhase = "PostEndObserve"
        Wait-WithPump $PostEndObserveMs

        $elapsedMs = [int]((Get-Date) - $cycleStartedAt).TotalMilliseconds
        $targetMs = $cycleTargetSeconds * 1000
        if ($elapsedMs -lt $targetMs) {
            $script:currentPhase = "InterCycleWait"
            Wait-WithPump ($targetMs - $elapsedMs)
        }
        Drain-CallbackEvents
        Sample-MetricsIfDue

        $cycleCallbacks = $script:stats.CallbackTotal - $callbackBefore
        $cycleCallbackErrors = $script:stats.CallbackErrors - $callbackErrorsBefore
        $cyclePostEnd = $script:stats.PostEndPushCallbacks - $postEndBefore
        if ($cycleCallbackErrors -gt 0) { $cycleFailed = $true; $cycleNote += "Failure callback received;" }
        if ($cyclePostEnd -gt 0) { $cycleFailed = $true; $cycleNote += "Process push received after End;" }
        if ($cycleFailed) { $script:stats.CycleFailures++ }
        $cycleEndedAt = Get-Date
        Add-CsvRow "Cycles" ([pscustomobject]@{
            CycleId=$script:currentCycleId; TerminalIndex=$script:currentTerminal
            StartedAt=$cycleStartedAt.ToString("o"); EndedAt=$cycleEndedAt.ToString("o")
            TargetCycleSeconds=$cycleTargetSeconds; TargetCaptureSeconds=$captureTargetSeconds
            ActualDurationMs=[int]($cycleEndedAt - $cycleStartedAt).TotalMilliseconds
            SwitchReturnCode=$switchResult.ReturnCode; SwitchDurationMs=$switchResult.DurationMs
            StartReturnCode=$startResult.ReturnCode; StartDurationMs=$startResult.DurationMs
            EndReturnCode=$endResult.ReturnCode; EndDurationMs=$endResult.DurationMs
            PostEndFaceReturnCode=$postEndFaceReturnCode; PostEndFaceSuccess=$postEndFaceSuccess
            PostEndFingerReturnCode=$postEndFingerReturnCode; PostEndFingerSuccess=$postEndFingerSuccess
            FaceTotal=$faceTotal; FaceOk=$faceOk; FaceFail=($faceTotal-$faceOk)
            FingerTotal=$fingerTotal; FingerOk=$fingerOk; FingerFail=($fingerTotal-$fingerOk)
            AsyncRequested=$asyncRequested; AsyncAccepted=$asyncAccepted
            CallbackCount=$cycleCallbacks; CallbackErrors=$cycleCallbackErrors
            ProcessPushCallbacksAfterEnd=$cyclePostEnd
            Result=if ($cycleFailed) { "FAILED" } elseif ($script:stopRequested) { "STOPPED" } else { "PASSED" }
            Note=$cycleNote
        })
        Write-Host ("Cycle {0} complete: T{1}, face {2}/{3}, finger {4}/{5}, callback={6}, afterEnd={7}, result={8}" -f
            $script:currentCycleId, $script:currentTerminal, $faceOk, $faceTotal, $fingerOk, $fingerTotal,
            $cycleCallbacks, $cyclePostEnd, $(if ($cycleFailed) { "FAILED" } else { "PASSED" })) -ForegroundColor $(if ($cycleFailed) { "Yellow" } else { "Green" })
    }
    }
}
catch {
    $script:fatalError = $_.Exception.ToString()
    Write-Host $script:fatalError -ForegroundColor Red
}
finally {
    $script:currentPhase = "Cleanup"
    try {
        if ($script:cycleNeedsEnd -and $script:sdkInitialized) {
            Invoke-DllCall "EndProcess.Cleanup" { [HzcyFullFlowNative]::EndProcess() } | Out-Null
            $script:cycleNeedsEnd = $false
        }
        if ($script:fingerprintPreviewStarted) {
            Invoke-DllCall "StopFingerprintPreview" { [HzcyFullFlowNative]::StopFingerprintPreview() } | Out-Null
            $script:fingerprintPreviewStarted = $false
        }
        if ($script:cameraPreviewStarted) {
            Invoke-DllCall "StopCameraPreview" { [HzcyFullFlowNative]::StopCameraPreview() } | Out-Null
            $script:cameraPreviewStarted = $false
        }
        Drain-CallbackEvents
        if ($script:sdkInitialized) {
            Invoke-DllCall "ReleaseSdk" { [HzcyFullFlowNative]::ReleaseSdk() } | Out-Null
            $script:sdkInitialized = $false
        }
        Sample-MetricsIfDue -Force
    }
    catch {
        if ($script:fatalError.Length -eq 0) { $script:fatalError = $_.Exception.ToString() }
        else { $script:fatalError += "`r`nCleanup error: " + $_.Exception.ToString() }
        Write-Host ("Cleanup error: " + $_.Exception.Message) -ForegroundColor Red
    }
    finally {
        if ($null -ne $previewForm) { $previewForm.Dispose() }
        Flush-AllCsvBuffers
        [Console]::remove_CancelKeyPress($cancelHandler)
    }
}

$testEndedAt = Get-Date
$overallResult = if ($script:fatalError.Length -eq 0 -and $script:stats.CallFailures -eq 0 -and
    $script:stats.CallbackErrors -eq 0 -and $script:stats.PostEndPushCallbacks -eq 0) { "PASSED" } else { "FAILED" }
$summary = [pscustomobject]@{
    Result=$overallResult
    StartedAt=$testStartedAt.ToString("o")
    EndedAt=$testEndedAt.ToString("o")
    ActualDurationMinutes=[Math]::Round(($testEndedAt-$testStartedAt).TotalMinutes, 2)
    TestHostArchitecture="x86"
    TestHostApartment="STA"
    DllPath=$DllPath
    DllSha256=$dllHash
    SelectedProxyPath=$ProxyExePath
    RunningProxyPath=$script:actualProxyPath
    ProxySha256=$script:actualProxyHash
    ProxyArchitecture="x64"
    ProxyProcessId=if ($null -ne $script:proxyProcess) { $script:proxyProcess.Id } else { "" }
    WorkloadMode=$WorkloadMode
    TerminalMode=$TerminalMode
    PreviewEnabled=(-not $SkipPreview)
    RestartedProxy=[bool]$RestartProxy
    MetricsIntervalSeconds=$MetricsIntervalSeconds
    Cycles=$script:stats.CycleTotal
    FailedCycles=$script:stats.CycleFailures
    Calls=$script:stats.CallTotal
    FailedCalls=$script:stats.CallFailures
    UiBlockWarnings=$script:stats.UiBlockWarnings
    FaceTotal=$script:stats.FaceTotal
    FaceOk=$script:stats.FaceOk
    FaceFail=$script:stats.FaceFail
    FaceP50Ms=(Get-Percentile $script:durationLists.FaceAll 0.50)
    FaceP95Ms=(Get-Percentile $script:durationLists.FaceAll 0.95)
    FaceP99Ms=(Get-Percentile $script:durationLists.FaceAll 0.99)
    FaceT1P95Ms=(Get-Percentile $script:durationLists.FaceT1 0.95)
    FaceT2P95Ms=(Get-Percentile $script:durationLists.FaceT2 0.95)
    FingerTotal=$script:stats.FingerTotal
    FingerOk=$script:stats.FingerOk
    FingerFail=$script:stats.FingerFail
    FingerP50Ms=(Get-Percentile $script:durationLists.FingerAll 0.50)
    FingerP95Ms=(Get-Percentile $script:durationLists.FingerAll 0.95)
    FingerP99Ms=(Get-Percentile $script:durationLists.FingerAll 0.99)
    FingerT1P95Ms=(Get-Percentile $script:durationLists.FingerT1 0.95)
    FingerT2P95Ms=(Get-Percentile $script:durationLists.FingerT2 0.95)
    AsyncRequested=$script:stats.AsyncTotal
    AsyncAccepted=$script:stats.AsyncAccepted
    AsyncRejected=$script:stats.AsyncRejected
    Callbacks=$script:stats.CallbackTotal
    CallbackErrors=$script:stats.CallbackErrors
    ProcessPushCallbacksAfterEnd=$script:stats.PostEndPushCallbacks
    FatalError=$script:fatalError
    CallsCsv=$callsCsv
    CyclesCsv=$cyclesCsv
    CallbacksCsv=$callbacksCsv
    MetricsCsv=$metricsCsv
}
$summary | Export-Csv -LiteralPath $summaryCsv -NoTypeInformation -Encoding UTF8

Write-Host ""
Write-Host "Test result: $overallResult" -ForegroundColor $(if ($overallResult -eq "PASSED") { "Green" } else { "Red" })
Write-Host "Cycles: $($script:stats.CycleTotal), failed: $($script:stats.CycleFailures)"
Write-Host "Face: $($script:stats.FaceOk)/$($script:stats.FaceTotal), P95=$(Get-Percentile $script:durationLists.FaceAll 0.95)ms"
Write-Host "Fingerprint: $($script:stats.FingerOk)/$($script:stats.FingerTotal), P95=$(Get-Percentile $script:durationLists.FingerAll 0.95)ms"
Write-Host "Callbacks: $($script:stats.CallbackTotal), failure callbacks: $($script:stats.CallbackErrors), process pushes after End: $($script:stats.PostEndPushCallbacks)"
Write-Host "Summary: $summaryCsv"

if ($overallResult -eq "PASSED") { exit 0 } else { exit 1 }
