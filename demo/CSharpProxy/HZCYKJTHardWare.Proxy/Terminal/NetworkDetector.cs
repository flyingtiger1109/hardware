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
                        Logger.Info($"已检测到局域网IP：{ip}（网段：{subnetPrefix}）");
                        return ip.ToString();
                    }
                }
            }
            catch (SocketException ex)
            {
                Logger.Error("检测局域网IP失败", ex);
            }

            Logger.Warn($"未找到匹配网段的IP：{subnetPrefix}");
            return "";
        }
    }
}
