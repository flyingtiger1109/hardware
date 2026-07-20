param(
    [Parameter(Mandatory=$true)]
    [int]$ProcessId
)

$ErrorActionPreference = "Stop"

$source = @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;

public sealed class NativeHandleTypeCount
{
    public ushort TypeIndex { get; set; }
    public string TypeName { get; set; }
    public int Count { get; set; }
}

public static class NativeHandleInspector
{
    private const int SystemExtendedHandleInformation = 64;
    private const int ObjectTypeInformation = 2;
    private const int ProcessDuplicateHandle = 0x0040;
    private const uint DuplicateSameAccess = 0x00000002;
    private const int StatusInfoLengthMismatch = unchecked((int)0xC0000004);

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    private sealed class HandleEntry
    {
        public ushort TypeIndex;
        public IntPtr HandleValue;
    }

    [DllImport("ntdll.dll")]
    private static extern int NtQuerySystemInformation(
        int systemInformationClass,
        IntPtr systemInformation,
        int systemInformationLength,
        out int returnLength);

    [DllImport("ntdll.dll")]
    private static extern int NtQueryObject(
        IntPtr handle,
        int objectInformationClass,
        IntPtr objectInformation,
        int objectInformationLength,
        out int returnLength);

    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern IntPtr OpenProcess(int desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern bool DuplicateHandle(
        IntPtr sourceProcessHandle,
        IntPtr sourceHandle,
        IntPtr targetProcessHandle,
        out IntPtr targetHandle,
        uint desiredAccess,
        bool inheritHandle,
        uint options);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern bool CloseHandle(IntPtr handle);

    public static NativeHandleTypeCount[] CountByType(int processId)
    {
        if (IntPtr.Size != 8)
            throw new PlatformNotSupportedException("Run this inspector from x64 PowerShell.");

        var entries = QueryEntries(processId);
        var counts = new Dictionary<ushort, int>();
        var samples = new Dictionary<ushort, List<IntPtr>>();
        foreach (var entry in entries)
        {
            int count;
            counts.TryGetValue(entry.TypeIndex, out count);
            counts[entry.TypeIndex] = count + 1;

            List<IntPtr> handles;
            if (!samples.TryGetValue(entry.TypeIndex, out handles))
            {
                handles = new List<IntPtr>();
                samples[entry.TypeIndex] = handles;
            }
            if (handles.Count < 16)
                handles.Add(entry.HandleValue);
        }

        var processHandle = OpenProcess(ProcessDuplicateHandle, false, processId);
        if (processHandle == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "OpenProcess(PROCESS_DUP_HANDLE) failed.");

        try
        {
            var result = new List<NativeHandleTypeCount>();
            foreach (var item in counts)
            {
                var typeName = ResolveTypeName(processHandle, samples[item.Key]);
                result.Add(new NativeHandleTypeCount {
                    TypeIndex = item.Key,
                    TypeName = typeName,
                    Count = item.Value
                });
            }
            result.Sort((left, right) => right.Count.CompareTo(left.Count));
            return result.ToArray();
        }
        finally
        {
            CloseHandle(processHandle);
        }
    }

    private static List<HandleEntry> QueryEntries(int processId)
    {
        var length = 1 << 20;
        while (true)
        {
            var buffer = Marshal.AllocHGlobal(length);
            try
            {
                int required;
                var status = NtQuerySystemInformation(
                    SystemExtendedHandleInformation, buffer, length, out required);
                if (status == StatusInfoLengthMismatch)
                {
                    length = Math.Max(length * 2, required + (1 << 16));
                    continue;
                }
                if (status < 0)
                    throw new InvalidOperationException("NtQuerySystemInformation failed: 0x" + status.ToString("X8"));

                var count = Marshal.ReadInt64(buffer, 0);
                var offset = 16L;
                const int entrySize = 40;
                var result = new List<HandleEntry>();
                for (long index = 0; index < count; index++, offset += entrySize)
                {
                    var entry = IntPtr.Add(buffer, checked((int)offset));
                    var ownerPid = Marshal.ReadInt64(entry, 8);
                    if (ownerPid != processId)
                        continue;
                    result.Add(new HandleEntry {
                        HandleValue = new IntPtr(Marshal.ReadInt64(entry, 16)),
                        TypeIndex = unchecked((ushort)Marshal.ReadInt16(entry, 30))
                    });
                }
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    private static string ResolveTypeName(IntPtr processHandle, List<IntPtr> handles)
    {
        foreach (var sourceHandle in handles)
        {
            IntPtr duplicate;
            if (!DuplicateHandle(processHandle, sourceHandle, GetCurrentProcess(), out duplicate,
                0, false, DuplicateSameAccess))
                continue;
            try
            {
                var length = 1024;
                var buffer = Marshal.AllocHGlobal(length);
                try
                {
                    int required;
                    var status = NtQueryObject(duplicate, ObjectTypeInformation, buffer, length, out required);
                    if (status == StatusInfoLengthMismatch && required > length)
                    {
                        Marshal.FreeHGlobal(buffer);
                        length = required;
                        buffer = Marshal.AllocHGlobal(length);
                        status = NtQueryObject(duplicate, ObjectTypeInformation, buffer, length, out required);
                    }
                    if (status >= 0)
                    {
                        var value = (UnicodeString)Marshal.PtrToStructure(buffer, typeof(UnicodeString));
                        if (value.Buffer != IntPtr.Zero && value.Length > 0)
                            return Marshal.PtrToStringUni(value.Buffer, value.Length / 2);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }
            finally
            {
                CloseHandle(duplicate);
            }
        }
        return "TypeIndex " + (handles.Count > 0 ? "(unresolved)" : "(no sample)");
    }
}
'@

Add-Type -TypeDefinition $source -Language CSharp
[NativeHandleInspector]::CountByType($ProcessId) |
    Select-Object TypeIndex, TypeName, Count
