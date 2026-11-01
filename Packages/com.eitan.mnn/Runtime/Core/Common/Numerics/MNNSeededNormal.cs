using System;

namespace MNN.Unity
{
    // PyTorch CPU scalar normal_fill<float>, with a private MT19937 per recording.
    // See Documentation~/PyTorchNumericsLicense.md and Mt19937License.md.
    internal sealed class MNNSeededNormal
    {
        private readonly uint[] _state = new uint[624];
        private int _next = 624;

        internal MNNSeededNormal(uint seed)
        {
            _state[0] = seed;
            for (uint i = 1; i < _state.Length; ++i)
                _state[i] = unchecked(1812433253U * (_state[i - 1] ^ (_state[i - 1] >> 30)) + i);
        }

        internal static float[] Generate(int count, uint seed)
        {
            var output = new float[count];
            new MNNSeededNormal(seed).Fill(output);
            return output;
        }

        internal void Fill(float[] output)
        {
            if (output == null || output.Length < 16)
                throw new ArgumentException("Supply at least sixteen normal samples.", nameof(output));
            for (int i = 0; i < output.Length; ++i) output[i] = Uniform();
            for (int offset = 0; offset + 16 <= output.Length; offset += 16) Transform(output, offset);
            if (output.Length % 16 == 0) return;
            int last = output.Length - 16;
            for (int i = last; i < output.Length; ++i) output[i] = Uniform();
            Transform(output, last);
        }

        private float Uniform()
        {
            if (_next == 624) Twist();
            uint value = _state[_next++];
            value ^= value >> 11;
            value ^= (value << 7) & 0x9d2c5680;
            value ^= (value << 15) & 0xefc60000;
            value ^= value >> 18;
            return (value & 0xffffff) * (1f / 16777216);
        }

        private void Twist()
        {
            for (int i = 0; i < 624; ++i)
            {
                uint mixed = (_state[i] & 0x80000000) | (_state[(i + 1) % 624] & 0x7fffffff);
                _state[i] = _state[(i + 397) % 624] ^ (mixed >> 1) ^ ((mixed & 1) == 0 ? 0U : 0x9908b0dfU);
            }
            _next = 0;
        }

        private static void Transform(float[] values, int offset)
        {
            for (int i = 0; i < 8; ++i)
            {
                float u = 1 - values[offset + i];
                float radius = (float)Math.Sqrt(-2 * (float)Math.Log(u));
                float angle = (float)(2 * Math.PI * values[offset + i + 8]);
                values[offset + i] = radius * (float)Math.Cos(angle);
                values[offset + i + 8] = radius * (float)Math.Sin(angle);
            }
        }
    }
}
