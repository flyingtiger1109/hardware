using System;
using System.Runtime.InteropServices;
using System.Text;

namespace HZCYKJTHardWare.CSharpDemo.Native
{
    internal sealed class Utf8NativeString : IDisposable
    {
        public IntPtr Pointer { get; private set; }

        public Utf8NativeString(string value)
        {
            if (value == null)
            {
                Pointer = IntPtr.Zero;
                return;
            }

            var bytes = Encoding.UTF8.GetBytes(value);
            Pointer = Marshal.AllocHGlobal(bytes.Length + 1);
            Marshal.Copy(bytes, 0, Pointer, bytes.Length);
            Marshal.WriteByte(Pointer, bytes.Length, 0);
        }

        public void Dispose()
        {
            if (Pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(Pointer);
                Pointer = IntPtr.Zero;
            }
        }

        public static string FromPointer(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero)
            {
                return string.Empty;
            }

            var length = 0;
            while (Marshal.ReadByte(ptr, length) != 0)
            {
                length++;
            }

            if (length == 0)
            {
                return string.Empty;
            }

            var bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, length);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
