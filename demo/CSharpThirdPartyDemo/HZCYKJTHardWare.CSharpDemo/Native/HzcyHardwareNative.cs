using System;
using System.Runtime.InteropServices;

namespace HZCYKJTHardWare.CSharpDemo.Native
{
    internal static class HzcyHardwareNative
    {
        private const string DllName = "HZCYKJTHardWare.dll";

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void EventCallbackDelegate(IntPtr eventJson);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_InitSdk();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_ReleaseSdk();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_RegisterEventCallback(EventCallbackDelegate callback);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_SwitchTerminal(int terminalIndex);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_StartProcess(IntPtr saveDir);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_EndProcess();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_StartCameraPreview(IntPtr hwnd);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_StopCameraPreview();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_StartFingerprintPreview(IntPtr hwnd);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_StopFingerprintPreview();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_StartIrisPreview(IntPtr hwnd);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_StopIrisPreview();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_StartPlatePreview(IntPtr hwnd);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_StopPlatePreview();

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_CaptureCameraImage(IntPtr saveDir);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_CaptureFingerprintImage(IntPtr saveDir);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_CaptureIrisImage(IntPtr saveDir);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_RequestOCR(IntPtr saveDir);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_RequestNfcCard(IntPtr saveDir);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall, ExactSpelling = true)]
        internal static extern int HZCYKJTHardWare_RequestAuthorize(
            IntPtr zjhm,
            IntPtr zjlb,
            IntPtr gjdqdm,
            IntPtr xm,
            IntPtr xb,
            IntPtr csrq,
            IntPtr kadm);
    }
}
