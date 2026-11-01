using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Fixed slots own their arrays for the lifetime of this transport; peek is consumer-only.</summary>
    internal sealed class MNNSpscPcmQueue<T> where T : struct
    {
        private readonly float[][] _pcm;
        private readonly T[] _metadata;
        private readonly int[] _lengths;
        private int _read;
        private int _write;
        internal int MaximumSamples { get; }
        internal int AvailableBlocks => (Volatile.Read(ref _read) - _write - 1 + _pcm.Length) % _pcm.Length;
        internal bool CanWrite => Next(_write) != Volatile.Read(ref _read);
        internal bool HasItems => Volatile.Read(ref _read) != Volatile.Read(ref _write);

        internal MNNSpscPcmQueue(int capacity, int maximumSamples)
        {
            if (capacity < 1 || capacity > 4096)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (maximumSamples < 1 || maximumSamples > 1536000 || (long)(capacity + 1) * maximumSamples > 16777216)
                throw new ArgumentOutOfRangeException(nameof(maximumSamples), "PCM transport may reserve at most 64 MiB.");
            MaximumSamples = maximumSamples;
            _pcm = new float[capacity + 1][];
            _metadata = new T[capacity + 1];
            _lengths = new int[capacity + 1];
            for (int i = 0; i < _pcm.Length; ++i)
                _pcm[i] = new float[maximumSamples];
        }

        internal bool TryWrite(float[] source, int offset, int count, T metadata)
        {
            ValidateRange(source, offset, count);
            if (count < 1 || count > MaximumSamples)
                throw new ArgumentOutOfRangeException(nameof(count));
            int next = Next(_write);
            if (next == Volatile.Read(ref _read))
                return false;
            Array.Copy(source, offset, _pcm[_write], 0, count);
            _metadata[_write] = metadata;
            _lengths[_write] = count;
            Volatile.Write(ref _write, next);
            return true;
        }

        internal bool TryPeek(out T metadata, out float[] pcm, out int count)
        {
            int read = _read;
            if (read == Volatile.Read(ref _write))
            {
                metadata = default;
                pcm = null;
                count = 0;
                return false;
            }
            metadata = _metadata[read];
            pcm = _pcm[read];
            count = _lengths[read];
            return true;
        }

        internal void Remove()
        {
            _metadata[_read] = default;
            Volatile.Write(ref _read, Next(_read));
        }

        internal static void ValidateRange(float[] samples, int offset, int count)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            if (offset < 0 || count < 0 || offset > samples.Length - count)
                throw new ArgumentOutOfRangeException(nameof(offset));
        }

        private int Next(int value) => value + 1 == _pcm.Length ? 0 : value + 1;
    }
}
