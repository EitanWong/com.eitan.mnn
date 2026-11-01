using System;
using System.Collections.Generic;
using System.IO;
namespace MNN.Unity
{
    internal sealed class MNNVoxTurnWeights : IDisposable
    {
        private readonly FileStream _embedding;
        private readonly Dictionary<int, float[]> _cache = new Dictionary<int, float[]>();
        private readonly byte[] _bytes = new byte[2048]; private readonly int[] _bits = new int[1024];
        internal readonly float[] Head, Cos, Sin; internal readonly float Bias;
        internal MNNVoxTurnWeights(string directory)
        {
            Head = MNNVoxTurnModel.Floats(directory, "head-weight.bin", 1024);
            Bias = MNNVoxTurnModel.Floats(directory, "head-bias.bin", 1)[0];
            Cos = MNNVoxTurnModel.Floats(directory, "rotary-cos.bin", 2048 * 128);
            Sin = MNNVoxTurnModel.Floats(directory, "rotary-sin.bin", 2048 * 128);
            _embedding = new FileStream(Path.Combine(directory, "embeddings_bf16.bin"), FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.RandomAccess);
        }
        internal float[] Embed(int[] ids)
        {
            var result = new float[checked(ids.Length * 1024)];
            for (int i = 0; i < ids.Length; ++i)
            {
                int id = ids[i];
                if (id < 0)
                    continue;
                if (id >= 151936)
                    throw new InvalidDataException("VoxTurn token exceeds embedding table.");
                if (!_cache.TryGetValue(id, out var vector))
                {
                    _embedding.Position = (long)id * 2048;
                    int count = 0;
                    while (count < _bytes.Length)
                    {
                        int read = _embedding.Read(_bytes, count, _bytes.Length - count);
                        if (read == 0)
                            throw new EndOfStreamException();
                        count += read;
                    }
                    for (int k = 0; k < 1024; ++k)
                        _bits[k] = (_bytes[k * 2] | _bytes[k * 2 + 1] << 8) << 16;
                    vector = new float[1024];
                    Buffer.BlockCopy(_bits, 0, vector, 0, 4096);
                    if (_cache.Count >= 4096)
                        _cache.Clear();
                    _cache.Add(id, vector);
                }
                Array.Copy(vector, 0, result, i * 1024, 1024);
            }
            return result;
        }
        internal float[] Rotary(float[] table, int[] positions)
        {
            var result = new float[checked(positions.Length * 128)];
            for (int i = 0; i < positions.Length; ++i)
                Array.Copy(table, positions[i] * 128, result, i * 128, 128);
            return result;
        }
        internal float[] Score(float[] hidden, int length, MNNVoxTurnPrompt.Option option)
        {
            var result = new float[option.Positions.Length];
            for (int j = 0; j < result.Length; ++j)
            {
                double sum = Bias;
                int at = (option.Row * length + option.Positions[j]) * 1024;
                for (int k = 0; k < 1024; ++k)
                    sum += (double)hidden[at + k] * Head[k];
                result[j] = (float)sum;
                if (float.IsNaN(result[j]) || float.IsInfinity(result[j]))
                    throw new InvalidDataException("Nonfinite VoxTurn score.");
            }
            return result;
        }
        public void Dispose() => _embedding.Dispose();
    }
}
