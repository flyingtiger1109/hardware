param(
    [string]$DllPath = (Join-Path $PSScriptRoot "..\Release\HZCYKJTHardWare.dll"),
    [int]$ProxyPort = 8089,
    [int]$WaitMs = 5000
)

$ErrorActionPreference = "Stop"
$resolvedDll = (Resolve-Path -LiteralPath $DllPath).Path
$escapedDll = $resolvedDll.Replace('"', '\"')

Add-Type -AssemblyName System.Windows.Forms

$source = @"
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public sealed class PreviewLeaseFakeProxy : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Thread _thread;
    private volatile bool _stopping;
    private string _instanceId;
    private int _available = 1;

    public int PingCount;
    public int CameraStartCount;
    public int CameraStopCount;

    public PreviewLeaseFakeProxy(int port, string instanceId)
    {
        _instanceId = instanceId;
        _listener = new TcpListener(IPAddress.Loopback, port);
        _listener.Start();
        _thread = new Thread(Run) { IsBackground = true, Name = "Preview Lease Fake Proxy" };
        _thread.Start();
    }

    public void SetInstanceId(string instanceId)
    {
        Interlocked.Exchange(ref _instanceId, instanceId);
    }

    public void SetAvailable(bool available)
    {
        Interlocked.Exchange(ref _available, available ? 1 : 0);
    }

    private void Run()
    {
        while (!_stopping)
        {
            TcpClient client = null;
            try
            {
                client = _listener.AcceptTcpClient();
                Handle(client);
            }
            catch (SocketException)
            {
                if (!_stopping) throw;
            }
            catch (ObjectDisposedException)
            {
                if (!_stopping) throw;
            }
            finally
            {
                if (client != null) client.Close();
            }
        }
    }

    private void Handle(TcpClient client)
    {
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true);
        var requestLine = reader.ReadLine() ?? "";
        string line;
        var contentLength = 0;
        while (!string.IsNullOrEmpty(line = reader.ReadLine()))
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                int.TryParse(line.Substring("Content-Length:".Length).Trim(), out contentLength);
        }
        if (contentLength > 0)
        {
            var body = new char[contentLength];
            reader.ReadBlock(body, 0, body.Length);
        }

        var parts = requestLine.Split(' ');
        var path = parts.Length > 1 ? parts[1] : "";
        string response;
        if (path == "/ping")
        {
            Interlocked.Increment(ref PingCount);
            if (Interlocked.CompareExchange(ref _available, 1, 1) == 0)
                return;
            response = "{\"status\":\"ok\",\"proxy_instance_id\":\"" + _instanceId + "\"}";
        }
        else if (path == "/preview/camera/start")
        {
            Interlocked.Increment(ref CameraStartCount);
            response = "{\"accepted\":true}";
        }
        else if (path == "/preview/camera/stop")
        {
            Interlocked.Increment(ref CameraStopCount);
            response = "{\"status\":\"ok\"}";
        }
        else
        {
            response = "{\"status\":\"ok\"}";
        }

        var bodyBytes = Encoding.UTF8.GetBytes(response);
        var header = "HTTP/1.1 200 OK\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: " +
                     bodyBytes.Length + "\r\nConnection: close\r\n\r\n";
        var headerBytes = Encoding.ASCII.GetBytes(header);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(bodyBytes, 0, bodyBytes.Length);
        stream.Flush();
    }

    public void Dispose()
    {
        _stopping = true;
        _listener.Stop();
        if (!_thread.Join(2000))
            throw new TimeoutException("Fake proxy thread did not stop.");
    }
}

public static class PreviewLeaseNative
{
    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall)]
    public static extern int HZCYKJTHardWare_InitSdk();

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall)]
    public static extern int HZCYKJTHardWare_ReleaseSdk();

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall)]
    public static extern int HZCYKJTHardWare_StartCameraPreview(IntPtr hwnd);
}
"@

Add-Type -TypeDefinition $source -Language CSharp

function Wait-Until([scriptblock]$Condition, [int]$TimeoutMs, [string]$FailureMessage) {
    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($watch.ElapsedMilliseconds -lt $TimeoutMs) {
        if (& $Condition) { return }
        Start-Sleep -Milliseconds 50
    }
    throw $FailureMessage
}

$server = $null
$form = $null
$initialized = $false
try {
    $server = New-Object PreviewLeaseFakeProxy($ProxyPort, "11111111111111111111111111111111")
    $form = New-Object System.Windows.Forms.Form
    $panel = New-Object System.Windows.Forms.Panel
    $form.Controls.Add($panel)
    [void]$form.Handle
    [void]$panel.Handle

    $initResult = [PreviewLeaseNative]::HZCYKJTHardWare_InitSdk()
    if ($initResult -ne 1) { throw "InitSdk failed: $initResult" }
    $initialized = $true

    $startResult = [PreviewLeaseNative]::HZCYKJTHardWare_StartCameraPreview($panel.Handle)
    if ($startResult -ne 1) { throw "StartCameraPreview failed: $startResult" }
    Wait-Until { $server.CameraStartCount -ge 1 } $WaitMs "Initial preview start was not received."

    $pingBeforeOutage = $server.PingCount
    $server.SetAvailable($false)
    Wait-Until { $server.PingCount -gt $pingBeforeOutage } $WaitMs "DLL did not detect the proxy outage."
    $server.SetInstanceId("22222222222222222222222222222222")
    $server.SetAvailable($true)
    Wait-Until { $server.CameraStartCount -ge 2 } $WaitMs "Preview was not restored after proxy instance change."

    $releaseResult = [PreviewLeaseNative]::HZCYKJTHardWare_ReleaseSdk()
    $initialized = $false
    if ($releaseResult -ne 1) { throw "ReleaseSdk failed: $releaseResult" }
    Wait-Until { $server.CameraStopCount -ge 1 } $WaitMs "ReleaseSdk did not stop the proxy preview."

    Write-Output ("PASS ping={0} camera_start={1} camera_stop={2}" -f `
        $server.PingCount, $server.CameraStartCount, $server.CameraStopCount)
}
finally {
    if ($initialized) {
        try { [void][PreviewLeaseNative]::HZCYKJTHardWare_ReleaseSdk() } catch { }
    }
    if ($form -ne $null) { $form.Dispose() }
    if ($server -ne $null) { $server.Dispose() }
}
