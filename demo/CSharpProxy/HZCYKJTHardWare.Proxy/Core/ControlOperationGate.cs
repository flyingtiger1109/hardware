using System;
using System.Threading;

namespace HZCYKJTHardWare.Proxy.Core
{
    /// <summary>
    /// Serializes lifecycle-changing control operations without creating a wait
    /// queue. StartProcess, EndProcess and terminal switching either acquire the
    /// gate immediately or report busy to the caller.
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
