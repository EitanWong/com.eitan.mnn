using System;
using System.IO;

namespace MNN.Unity
{
    // These are serialized model formats, not native object memory layouts.
    internal sealed class MNNSanaEmbedding : IDisposable
    {
        private readonly FileStream _weights;
        private readonly long _weightOffset, _alphaOffset;
        private readonly int _tokens;
        internal MNNSanaEmbedding(string root)
        {
            var config = MNNModelData.Read(Path.Combine(root, "llm_config.json"));
            var tie = MNNModelData.Array(MNNModelData.Get(config, "tie_embeddings"));
            if (MNNModelData.Integer(MNNModelData.Get(config, "hidden_size")) != 1024 || tie.Count != 5 || MNNModelData.Integer(tie[3]) != 4 || MNNModelData.Integer(tie[4]) != 64)
                throw new NotSupportedException("Expected Sana Qwen3 1024-wide q4/block64 embeddings.");
            _weightOffset = checked((long)MNNModelData.Number(tie[0]));
            _alphaOffset = checked((long)MNNModelData.Number(tie[1]));
            _tokens = checked((int)((_alphaOffset - _weightOffset) / 512));
            long alphaSize = checked((long)MNNModelData.Number(tie[2]));
            if (_tokens < 151669 || alphaSize != _tokens * 16L * 8)
                throw new InvalidDataException("Expected asymmetric fp32 embedding scales.");
            _weights = File.OpenRead(Path.Combine(root, "llm.mnn.weight"));
            if (_weights.Length < _alphaOffset + alphaSize)
            {
                _weights.Dispose();
                throw new InvalidDataException("Truncated Sana embedding weights.");
            }
        }

        internal float[] Read(int[] ids)
        {
            var result = new float[ids.Length * 1024];
            var packed = new byte[512];
            var alpha = new byte[128];
            for (int t = 0; t < ids.Length; ++t)
            {
                int id = ids[t];
                if (id < 0 || id >= _tokens)
                    throw new InvalidDataException("Sana token out of embedding range.");
                _weights.Position = _weightOffset + id * 512L;
                ReadFull(packed);
                _weights.Position = _alphaOffset + id * 128L;
                ReadFull(alpha);
                for (int block = 0; block < 16; ++block)
                {
                    float scale = BitConverter.ToSingle(alpha, block * 8 + 4);
                    float zero = BitConverter.ToSingle(alpha, block * 8) - 8 * scale;
                    for (int i = 0; i < 32; ++i)
                    {
                        byte b = packed[block * 32 + i];
                        int offset = t * 1024 + block * 64 + i * 2;
                        result[offset] = (b >> 4) * scale + zero;
                        result[offset + 1] = (b & 15) * scale + zero;
                    }
                }
            }

            return result;
        }

        private void ReadFull(byte[] buffer)
        {
            int n = 0;
            while (n < buffer.Length)
            {
                int read = _weights.Read(buffer, n, buffer.Length - n);
                if (read == 0)
                    throw new EndOfStreamException();
                n += read;
            }
        }

        public void Dispose() => _weights?.Dispose();
    }
}
