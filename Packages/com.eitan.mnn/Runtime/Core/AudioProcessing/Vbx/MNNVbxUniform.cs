namespace MNN.Unity
{
    // NumPy RandomState MT19937 double uniforms: two words, 27+26 bits.
    // Private state per recording; see Documentation~/Mt19937License.md.
    internal sealed class MNNVbxUniform
    {
        private readonly uint[] _state = new uint[624];
        private int _next = 624;
        internal MNNVbxUniform(uint seed)
        {
            _state[0] = seed;
            for (uint i = 1; i < _state.Length; ++i)
                _state[i] = unchecked(1812433253U * (_state[i - 1] ^ (_state[i - 1] >> 30)) + i);
        }
        internal double Next()
        {
            uint high = Word() >> 5, low = Word() >> 6;
            return (high * 67108864.0 + low) / 9007199254740992.0;
        }
        private uint Word()
        {
            if (_next == 624) Twist();
            uint value = _state[_next++]; value ^= value >> 11;
            value ^= (value << 7) & 0x9d2c5680; value ^= (value << 15) & 0xefc60000; value ^= value >> 18;
            return value;
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
    }
}
