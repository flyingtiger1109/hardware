using System.Net;
using System.Net.Sockets;
using HZCYKJTHardWare.Proxy.Infrastructure;

namespace HZCYKJTHardWare.Proxy.Terminal
{
    public static class NetworkDetector
    {
        public static string DetectLanIp(string subnetPrefix)
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork &&
                        ip.ToString().StartsWith(subnetPrefix))
                    {
                        Logger.Info($"检测到局域网IP: {ip}，子网: {subnetPrefix}");
                        return ip.ToString();
                    }
                }
            }
            catch (SocketException ex)
            {
                Logger.Error("Failed to detect LAN IP", ex);
            }

            Logger.Warn($"No IP found matching subnet prefix: {subnetPrefix}");
            return "";
        }
    }
}
