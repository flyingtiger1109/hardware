using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyTitle("五合一车道硬件平台")]
[assembly: AssemblyProduct("五合一车道硬件平台")]
[assembly: AssemblyVersion(HZCYKJTHardWare.Proxy.ProductVersionInfo.AssemblyVersion)]
[assembly: AssemblyFileVersion(HZCYKJTHardWare.Proxy.ProductVersionInfo.AssemblyVersion)]
[assembly: AssemblyInformationalVersion(HZCYKJTHardWare.Proxy.ProductVersionInfo.Version)]
[assembly: InternalsVisibleTo("HZCYKJTHardWare.Proxy.Tests")]

namespace HZCYKJTHardWare.Proxy
{
    internal static class ProductVersionInfo
    {
        internal const string DisplayName = "五合一车道硬件平台";
        internal const string Version = "1.3.5";
        internal const string AssemblyVersion = Version + ".0";
        internal const string DisplayVersion = "v" + Version;
        internal const string WindowTitle = DisplayName;
    }
}
