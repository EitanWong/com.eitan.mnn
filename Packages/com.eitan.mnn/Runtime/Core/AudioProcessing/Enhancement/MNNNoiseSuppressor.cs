using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Profile-selected utterance enhancement. Owns one runner; never call from a device callback.</summary>
    public sealed class MNNNoiseSuppressor : IDisposable
    {
        private readonly IDisposable _owner;
        private readonly Func<float[], int, CancellationToken, float[]> _enhance;
        private MNNNoiseSuppressor(IDisposable owner, Func<float[], int, CancellationToken, float[]> enhance)
        { _owner = owner; _enhance = enhance; }
        private static readonly Dictionary<string, Func<string, int, MNNBackendType, MNNNoiseSuppressor>> Profiles =
            new Dictionary<string, Func<string, int, MNNBackendType, MNNNoiseSuppressor>>
        {
            { MNNDtlnConfig.FileName, (d, t, b) => { var model = MNNDtln.Load(d, t, b); return new MNNNoiseSuppressor(model, model.Enhance); } },
            { MNNGtcrnConfig.FileName, (d, t, b) => { var model = MNNGtcrn.Load(d, t, b); return new MNNNoiseSuppressor(model, model.Enhance); } },
            { MNNFullSubNetConfig.FileName, (d, t, b) => { var model = MNNFullSubNet.Load(d, t, b); return new MNNNoiseSuppressor(model, model.Enhance); } },
            { MNNMossFormerGanConfig.FileName, (d, t, b) => { var model = MNNMossFormerGan.Load(d, t, b); return new MNNNoiseSuppressor(model, model.Enhance); } },
            { MNNMossFormer2SeConfig.FileName, (d, t, b) => { var model = MNNMossFormer2Se.Load(d, t, b); return new MNNNoiseSuppressor(model, model.Enhance); } },
            { MNNFrcrnSeConfig.FileName, (d, t, b) => { var model = MNNFrcrnSe.Load(d, t, b); return new MNNNoiseSuppressor(model, model.Enhance); } },
            { MNNDeepFilterNet3Config.FileName, (d, t, b) => { var model = MNNDeepFilterNet3.Load(d, t, b); return new MNNNoiseSuppressor(model, model.Enhance); } },
            { MNNDptnetConfig.FileName, (d, t, b) => { var model = MNNDptnetEnhancement.Load(d, t, b); return new MNNNoiseSuppressor(model, model.Enhance); } },
            { MNNDccrnetConfig.FileName, (d, t, b) => { var model = MNNDccrnetEnhancement.Load(d, t, b); return new MNNNoiseSuppressor(model, model.Enhance); } },
            { MNNDcunetConfig.FileName, (d, t, b) => { var model = MNNDcunetEnhancement.Load(d, t, b); return new MNNNoiseSuppressor(model, model.Enhance); } },
            { MNNDccrnConfig.FileName, (d, t, b) => { var model = MNNDccrnEnhancement.Load(d, t, b); return new MNNNoiseSuppressor(model, model.Enhance); } },
            { MNNResembleDenoiserConfig.FileName, (d, t, b) => { var model = MNNResembleDenoiser.Load(d, t, b); return new MNNNoiseSuppressor(model, model.Enhance); } },
            { MNNResembleEnhancerConfig.FileName, (d, t, b) => { var model = MNNResembleEnhancer.Load(d, 4, b); return new MNNNoiseSuppressor(model, (audio, rate, token) => RestoreRate(model.Enhance(MNNSpeechDsp.Resample(audio, rate, 44100, token), 44100, token), audio.Length, rate, token)); } },
        };
        public static string ProfileFile(string directory) => Profiles.Keys.FirstOrDefault(file => File.Exists(Path.Combine(directory, file)));
        public static MNNNoiseSuppressor Load(string directory, int threadCount = 2, MNNBackendType backendType = MNNBackendType.Auto)
        {
            string profile = ProfileFile(directory) ?? throw new InvalidDataException("No supported enhancement export profile in " + directory);
            return Profiles[profile](directory, threadCount, backendType);
        }
        /// <summary>Processes bounded three-second blocks, preserving rate and sample count. Each block is an
        /// independent utterance; block boundaries can affect enhancement quality. No realtime-throughput claim.</summary>
        public float[] Enhance(float[] audio, int sampleRate = 16000, CancellationToken cancellationToken = default)
        {
            if (audio == null || audio.Length == 0) throw new ArgumentException("Supply nonempty audio.", nameof(audio));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            var result = new float[audio.Length];
            int maximum = sampleRate * 3;
            for (int offset = 0; offset < audio.Length; offset += maximum)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int count = Math.Min(maximum, audio.Length - offset);
                var block = new float[Math.Max(sampleRate / 10, count)];
                Array.Copy(audio, offset, block, 0, count);
                var enhanced = _enhance(block, sampleRate, cancellationToken);
                if (enhanced.Length != block.Length) throw new InvalidDataException("Enhancer changed the audio duration.");
                Array.Copy(enhanced, 0, result, offset, count);
            }
            return result;
        }
        private static float[] RestoreRate(float[] audio, int length, int rate, CancellationToken token)
        {
            var result = MNNSpeechDsp.Resample(audio, 44100, rate, token);
            if (result.Length != length) Array.Resize(ref result, length);
            return result;
        }
        public void Dispose() => _owner.Dispose();
    }
}
