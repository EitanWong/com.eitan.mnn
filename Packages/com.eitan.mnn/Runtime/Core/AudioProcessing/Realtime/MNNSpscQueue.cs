using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Bounded single-producer/single-consumer transport. No Clear: only the consumer advances the reader.</summary>
    internal sealed class MNNSpscQueue<T> where T : struct
    {
        private readonly T[] _items;
        private int _read;
        private int _write;

        internal bool HasItems => Volatile.Read(ref _read) != Volatile.Read(ref _write);

        internal MNNSpscQueue(int capacity)
        {
            if (capacity < 1 || capacity > 65536)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            _items = new T[capacity + 1];
        }

        internal bool TryWrite(T item)
        {
            int next = Next(_write);
            if (next == Volatile.Read(ref _read))
                return false;
            _items[_write] = item;
            Volatile.Write(ref _write, next);
            return true;
        }

        internal bool TryRead(out T item)
        {
            int read = _read;
            if (read == Volatile.Read(ref _write))
            {
                item = default;
                return false;
            }
            item = _items[read];
            _items[read] = default;
            Volatile.Write(ref _read, Next(read));
            return true;
        }

        private int Next(int value) => value + 1 == _items.Length ? 0 : value + 1;
    }
}
