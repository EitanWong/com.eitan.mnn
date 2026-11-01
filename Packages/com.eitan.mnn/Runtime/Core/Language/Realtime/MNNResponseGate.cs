using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>One atomic word encodes both the output generation and its enabled state.</summary>
    internal sealed class MNNResponseGate
    {
        private static long _nextSession;
        private readonly long _session = Interlocked.Increment(ref _nextSession);
        private long _state;
        internal MNNResponseId Current => new MNNResponseId(_session, Interlocked.Read(ref _state));

        internal MNNResponseId Begin()
        {
            while (true)
            {
                long before = Interlocked.Read(ref _state);
                long after = checked(before + ((before & 1) == 0 ? 1 : 2));
                if (Interlocked.CompareExchange(ref _state, after, before) == before)
                    return new MNNResponseId(_session, after);
            }
        }

        internal void Silence()
        {
            while (true)
            {
                long before = Interlocked.Read(ref _state);
                if ((before & 1) == 0)
                    return;
                if (Interlocked.CompareExchange(ref _state, checked(before + 1), before) == before)
                    return;
            }
        }

        internal bool Accepts(MNNResponseId response) => response.IsValid &&
            response.Session == _session && response.Epoch == Interlocked.Read(ref _state);
    }
}
