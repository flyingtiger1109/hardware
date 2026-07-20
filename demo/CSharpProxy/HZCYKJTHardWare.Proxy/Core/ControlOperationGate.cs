using System;
using System.Threading;

namespace HZCYKJTHardWare.Proxy.Core
{
    /// <summary>
    /// 对改变生命周期的控制操作进行串行化，不创建等待队列。
    /// StartProcess、EndProcess 和终端切换必须立即获得执行权，否则向调用方返回忙状态。
    /// </summary>
    internal sealed class ControlOperationGate
    {
        private int _held;
        private long _leaseSequence;

        internal Lease TryEnter(string operation)
        {
            if (Interlocked.CompareExchange(ref _held, 1, 0) != 0)
                return null;

            return new Lease(this, operation,
                Interlocked.Increment(ref _leaseSequence));
        }

        private void Exit()
        {
            Volatile.Write(ref _held, 0);
        }

        internal sealed class Lease : IDisposable
        {
            private ControlOperationGate _owner;

            internal Lease(ControlOperationGate owner, string operation, long sequence)
            {
                _owner = owner;
                Operation = operation ?? "";
                Sequence = sequence;
            }

            internal string Operation { get; }
            internal long Sequence { get; }

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                owner?.Exit();
            }
        }
    }
}
