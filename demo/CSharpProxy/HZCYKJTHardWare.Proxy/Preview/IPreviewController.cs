using System;
using System.Threading.Tasks;

namespace HZCYKJTHardWare.Proxy.Preview
{
    public interface IPreviewController : IDisposable
    {
        bool IsRunning { get; }

        Task DisposeAsync(int timeoutMs);
    }
}
