using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: AssemblyTitle("HZCYJKTHardWare.Proxy")]
[assembly: AssemblyProduct("HZCYJKTHardWare.Proxy")]
[assembly: AssemblyVersion(HZCYKJTHardWare.Proxy.ProductVersionInfo.AssemblyVersion)]
[assembly: AssemblyFileVersion(HZCYKJTHardWare.Proxy.ProductVersionInfo.AssemblyVersion)]
[assembly: AssemblyInformationalVersion(HZCYKJTHardWare.Proxy.ProductVersionInfo.Version)]
[assembly: InternalsVisibleTo("HZCYKJTHardWare.Proxy.Tests")]

namespace HZCYKJTHardWare.Proxy
{
    internal static class ProductVersionInfo
    {
        internal const string Version = "1.2.9";
        internal const string AssemblyVersion = Version + ".0";
        internal const string DisplayVersion = "v" + Version;
    }
}
