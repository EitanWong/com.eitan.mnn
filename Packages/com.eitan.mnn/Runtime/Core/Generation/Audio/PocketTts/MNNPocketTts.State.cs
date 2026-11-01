using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;
using Output = MNN.Unity.MNNGenerationGraph.Output;

namespace MNN.Unity
{
    public sealed partial class MNNPocketTts
    {
        private void PrepareVoice(MNNReferenceAudio reference, CancellationToken token)
        {
            string key;
            using (var hash = SHA256.Create())
            {
                var bytes = new byte[reference.Samples.Length * 4 + 4];
                Buffer.BlockCopy(reference.Samples, 0, bytes, 4, bytes.Length - 4);
                Buffer.BlockCopy(BitConverter.GetBytes(reference.SampleRate), 0, bytes, 0, 4);
                key = Convert.ToBase64String(hash.ComputeHash(bytes));
            }

            if (_voiceState != null && key == _voiceKey)
                return;
            var samples = MNNSpeechDsp.Resample(reference.Samples, reference.SampleRate, SampleRate, token);
            var encoded = _encoder.Run("latents", new[]{new Input("audio", samples, 1, 1, samples.Length)});
            if (encoded.Length == 0 || encoded.Length % 1024 != 0)
                throw new InvalidDataException("Invalid Pocket reference embeddings.");
            Finite(encoded, encoded.Length);
            var voice = _bos.Concat(encoded).ToArray();
            int steps = voice.Length / 1024;
            if (steps > 1000)
                throw new ArgumentException("Pocket reference exceeds the cache capacity.", nameof(reference));
            token.ThrowIfCancellationRequested();
            var state = InitialState(_flowManifest, steps);
            Update(state, Run(_priming, _cacheNames, state, new Input("text_embeddings", voice, 1, steps, 1024)), 0, steps);
            _voiceState = state;
            _voiceKey = key;
            _voiceSteps = steps;
        }

        internal static void Update(List<Input> state, Output[] result, int offset, int step)
        {
            for (int layer = 0; layer < 6; ++layer)
            {
                var cache = result[offset + layer];
                var values = cache.Values;
                int capacity = state[layer * 3].Shape[2];
                if (step < 0 || step > capacity || !cache.Shape.SequenceEqual(new[]{2, 1, capacity, 16, 64}) || values.Length != 2 * capacity * 1024)
                    throw new InvalidDataException("Unexpected Pocket cache shape.");
                // Unused cache slots intentionally contain NaN. Only populated
                // rows must be finite; do not relax other graph validators.
                for (int half = 0; half < 2; ++half)
                    for (int i = 0; i < step * 1024; ++i)
                    {
                        float value = values[half * capacity * 1024 + i];
                        if (!(value >= -float.MaxValue && value <= float.MaxValue))
                            throw new InvalidDataException("Non-finite populated Pocket cache.");
                    }

                state[layer * 3] = cache.AsInput("state_" + layer * 3);
                state[layer * 3 + 2] = new Input("state_" + (layer * 3 + 2), new[]{step}, 1);
            }
        }

        // Public serialized tensor dimensions, not a native object/ABI layout.
        // Only populated rows are copied; future rows must retain the export's NaN fill.
        internal static Input ResizeCache(Input cache, int capacity, int populatedRows)
        {
            if (capacity < 1 || capacity > 1000)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (cache == null)
                throw new ArgumentNullException(nameof(cache));
            if (cache.Shape.Length != 5 || cache.Shape[0] != 2 || cache.Shape[1] != 1 || cache.Shape[2] < 1 || cache.Shape[2] > 1000 || cache.Shape[3] != 16 || cache.Shape[4] != 64 || !(cache.Values is float[] source) || source.Length != 2 * cache.Shape[2] * 1024)
                throw new InvalidDataException("Unexpected Pocket cache input.");
            if (populatedRows < 0 || populatedRows > cache.Shape[2] || populatedRows > capacity)
                throw new ArgumentOutOfRangeException(nameof(populatedRows));
            var values = new float[2 * capacity * 1024];
            for (int i = 0; i < values.Length; ++i)
                values[i] = float.NaN;
            for (int half = 0; half < 2; ++half)
                Array.Copy(source, half * cache.Shape[2] * 1024, values, half * capacity * 1024, populatedRows * 1024);
            return new Input(cache.Name, values, 2, 1, capacity, 16, 64);
        }

        private static List<Input> InitialState(List<object> manifest, int flowCacheRows = 1000)
        {
            var result = new List<Input>();
            foreach (var entry in manifest)
            {
                int[] shape = MNNModelData.Shape(MNNModelData.Get(entry, "shape"));
                if (shape.SequenceEqual(new[]{2, 1, 1000, 16, 64}))
                    shape[2] = flowCacheRows;
                int count = shape.Aggregate(1, (a, b) => checked(a * b));
                string fill = (string)MNNModelData.Get(entry, "fill"), dtype = (string)MNNModelData.Get(entry, "dtype");
                Array values = dtype == "float32" ? (Array)Enumerable.Repeat(fill == "nan" ? float.NaN : fill == "ones" ? 1f : 0f, count).ToArray() : Enumerable.Repeat(fill == "ones" ? 1 : 0, count).ToArray();
                result.Add(new Input((string)MNNModelData.Get(entry, "input_name"), values, shape));
            }

            return result;
        }

        private static void Finite(float[] values, int expected)
        {
            if (values.Length != expected || values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidDataException("Invalid Pocket stage output.");
        }

        private static float[] ReadBos(string path)
        {
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (!reader.ReadBytes(8).SequenceEqual(new byte[]{147, 78, 85, 77, 80, 89, 1, 0}))
                    throw new InvalidDataException("Invalid Pocket BOS NumPy file.");
                int count = reader.ReadUInt16();
                string header = Encoding.ASCII.GetString(reader.ReadBytes(count));
                if (!header.Contains("'<f4'") || !header.Contains("'fortran_order': False") || !header.Contains("(1, 1, 1024)") || reader.BaseStream.Length - reader.BaseStream.Position != 4096)
                    throw new InvalidDataException("Unexpected Pocket BOS NumPy layout.");
                var result = new float[1024];
                Buffer.BlockCopy(reader.ReadBytes(4096), 0, result, 0, 4096);
                Finite(result, 1024);
                return result;
            }
        }
    }
}
