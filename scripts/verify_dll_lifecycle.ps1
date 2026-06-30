param(
    [string]$DllPath = (Join-Path $PSScriptRoot "..\Release\HZCYKJTHardWare.dll"),
    [int]$Iterations = 3,
    [int]$ReleaseLimitMs = 5000
)

$ErrorActionPreference = "Stop"
$resolvedDll = (Resolve-Path -LiteralPath $DllPath).Path
$escapedDll = $resolvedDll.Replace('"', '\"')

$source = @"
using System.Runtime.InteropServices;

public static class HzcyLifecycleNative
{
    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall)]
    public static extern int HZCYKJTHardWare_InitSdk();

    [DllImport(@"$escapedDll", CallingConvention = CallingConvention.StdCall)]
    public static extern int HZCYKJTHardWare_ReleaseSdk();
}
"@

Add-Type -TypeDefinition $source -Language CSharp

$failed = $false
for ($i = 1; $i -le $Iterations; $i++) {
    $initWatch = [System.Diagnostics.Stopwatch]::StartNew()
    $initResult = [HzcyLifecycleNative]::HZCYKJTHardWare_InitSdk()
    $initWatch.Stop()

    $releaseWatch = [System.Diagnostics.Stopwatch]::StartNew()
    $releaseResult = [HzcyLifecycleNative]::HZCYKJTHardWare_ReleaseSdk()
    $releaseWatch.Stop()

    Write-Output ("iteration={0} init={1} init_ms={2} release={3} release_ms={4}" -f `
        $i, $initResult, $initWatch.ElapsedMilliseconds,
        $releaseResult, $releaseWatch.ElapsedMilliseconds)

    if ($initResult -ne 1 -or $releaseResult -ne 1 -or
        $releaseWatch.ElapsedMilliseconds -gt $ReleaseLimitMs) {
        $failed = $true
    }
}

if ($failed) { exit 1 }
exit 0
