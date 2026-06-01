using System.Text;

namespace HZCYKJTHardWare.Proxy.Infrastructure
{
    public static class EncodingHelper
    {
        public static string Utf8ToAnsi(string utf8String)
        {
            if (string.IsNullOrEmpty(utf8String)) return utf8String;
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(utf8String);
            return Encoding.GetEncoding(936).GetString(utf8Bytes);
        }

        public static string AnsiToUtf8(string ansiString)
        {
            if (string.IsNullOrEmpty(ansiString)) return ansiString;
            byte[] ansiBytes = Encoding.GetEncoding(936).GetBytes(ansiString);
            return Encoding.UTF8.GetString(ansiBytes);
        }
    }
}
