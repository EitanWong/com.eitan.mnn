using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Profile-selected two-source separation. Output preserves the caller's sample rate.</summary>
    public sealed class MNNSpeechSeparator : IDisposable
    {
        private readonly IDisposable _owner;
        private readonly Func<float[], int, CancellationToken, MNNSeparatedAudio> _separate;
        internal MNNSpeechSeparator(IDisposable owner, Func<float[], int, CancellationToken, MNNSeparatedAudio> separate)
        { _owner = owner; _separate = separate; }
        private static readonly Dictionary<string, Func<string, int, MNNBackendType, MNNSpeechSeparator>> Profiles =
            new Dictionary<string, Func<string, int, MNNBackendType, MNNSpeechSeparator>>
        {
            { MNNMossFormer2Config.FileName, (d, t, b) => { var model = MNNMossFormer2Separation.Load(d, t, b); return new MNNSpeechSeparator(model, model.Separate); } },
            { MNNSepFormerConfig.FileName, (d, t, b) => { var model = MNNSepFormerSeparation.Load(d, t, b); return new MNNSpeechSeparator(model, model.Separate); } },
            { MNNConvTasNetConfig.FileName, (d, t, b) => { var model = MNNConvTasNetSeparation.Load(d, t, b); return new MNNSpeechSeparator(model, model.Separate); } },
            { MNNDprnnConfig.FileName, (d, t, b) => { var model = MNNDprnnSeparation.Load(d, t, b); return new MNNSpeechSeparator(model, model.Separate); } },
            { MNNSudoRmRfConfig.FileName, (d, t, b) => { var model = MNNSudoRmRfSeparation.Load(d, t, b); return new MNNSpeechSeparator(model, model.Separate); } },
            { MNNTigerConfig.FileName, (d, t, b) => { var model = MNNTigerSeparation.Load(d, t, b); return new MNNSpeechSeparator(model, model.Separate); } },
            { MNNSepReformerConfig.FileName, (d, t, b) => { var model = MNNSepReformerSeparation.Load(d, t, b); return new MNNSpeechSeparator(model, model.Separate); } },
        };
        public static string ProfileFile(string directory) => Profiles.Keys.FirstOrDefault(file => File.Exists(Path.Combine(directory, file)));
        public static MNNSpeechSeparator Load(string directory, int threadCount = 2, MNNBackendType backendType = MNNBackendType.Auto)
        {
            string profile = ProfileFile(directory) ?? throw new InvalidDataException("No supported separation export profile in " + directory);
            return Profiles[profile](directory, threadCount, backendType);
        }
        /// <summary>Separates bounded three-second windows with 250 ms overlap. Matches source slots
        /// across audible overlap and crossfades boundaries. Silence cannot establish speaker identity.</summary>
        public MNNSeparatedAudio Separate(float[] audio, int sampleRate = 16000, CancellationToken cancellationToken = default)
        {
            if (audio == null || audio.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.", nameof(audio));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            int maximum = sampleRate * 3, overlap = sampleRate / 4;
            var first = new float[audio.Length];
            var second = new float[audio.Length];
            for (int offset = 0; offset < audio.Length; offset += maximum - overlap)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(maximum, audio.Length - offset);
                var block = new float[Math.Max(sampleRate / 10, count)];
                Array.Copy(audio, offset, block, 0, count);
                var separated = _separate(block, sampleRate, cancellationToken);
                if (separated.SampleRate != sampleRate || separated.Speaker0.Length != block.Length)
                    throw new InvalidDataException("Separator changed audio rate or duration.");
                int shared = offset == 0 ? 0 : Math.Min(overlap, count);
                bool swap = ShouldSwap(first, second, separated, offset, shared);
                Merge(first, swap ? separated.Speaker1 : separated.Speaker0, offset, count, shared);
                Merge(second, swap ? separated.Speaker0 : separated.Speaker1, offset, count, shared);
                if (offset + count == audio.Length) break;
            }
            return new MNNSeparatedAudio(first, second, sampleRate);
        }
        private static bool ShouldSwap(float[] first, float[] second, MNNSeparatedAudio block, int offset, int overlap)
        {
            double direct = 0, swapped = 0;
            for (int i = 0; i < overlap; ++i)
            {
                double a = first[offset + i], b = second[offset + i];
                double c = block.Speaker0[i], d = block.Speaker1[i];
                direct += (a - c) * (a - c) + (b - d) * (b - d);
                swapped += (a - d) * (a - d) + (b - c) * (b - c);
            }
            return swapped < direct;
        }
        private static void Merge(float[] output, float[] block, int offset, int count, int overlap)
        {
            for (int i = 0; i < overlap; ++i)
            {
                float weight = (i + 1f) / (overlap + 1f);
                output[offset + i] = output[offset + i] * (1 - weight) + block[i] * weight;
            }
            Array.Copy(block, overlap, output, offset + overlap, count - overlap);
        }
        public void Dispose() => _owner?.Dispose();
    }
}
