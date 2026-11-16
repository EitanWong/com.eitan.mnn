using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Owned audio/text endpoint detector selected from an installed export profile.
    /// Callers must supply an ASR transcript when RequiresTranscript is true.</summary>
    public sealed class MNNTurnDetector : IDisposable
    {
        private readonly IDisposable _owner;
        private readonly Func<float[], int, string, CancellationToken, string, bool> _predict;
        public bool RequiresTranscript { get; }
        private readonly Func<MNNBackendType> _backend;
        public MNNBackendType Backend => _backend();
        private MNNTurnDetector(IDisposable owner, Func<MNNBackendType> backend, bool text,
            Func<float[], int, string, CancellationToken, string, bool> predict)
        { _owner = owner; _backend = backend; RequiresTranscript = text; _predict = predict; }
        public static string ProfileFile(string directory)
        {
            foreach (string file in new[] { MNNSmartTurnConfig.FileName, MNNNamoTurnDetectorConfig.FileName,
                MNNTurnSenseConfig.FileName, MNNLiveKitTurnDetectorConfig.FileName, MNNCueLightConfig.FileName,
                MNNVoxTurnModel.ConfigFile, MNNDualTurnEndpointingConfig.FileName, MNNBairongTurnSenseConfig.FileName,
                MNNTenTurnDetector.ConfigFileName, MNNLiveKitMultilingual.ConfigFileName, MNNEasyTurn.ConfigFileName })
                if (File.Exists(Path.Combine(directory, file))) return file;
            return null;
        }
        public static MNNTurnDetector Load(string directory, int threadCount = 0, MNNBackendType backendType = MNNBackendType.Auto)
        {
            int threads = threadCount == 0 ? 4 : threadCount;
            int semanticThreads = threadCount == 0 ? 1 : threadCount;
            int lightThreads = threadCount == 0 ? 1 : threadCount;
            switch (ProfileFile(directory))
            {
                case MNNEasyTurn.ConfigFileName:
                    var easy = MNNEasyTurn.Load(directory, threads, backendType);
                    return new MNNTurnDetector(easy, () => easy.Backend, false, (a, r, t, c, l) => easy.Predict(a, r, c).IsComplete);
                case MNNTenTurnDetector.ConfigFileName:
                    var ten = MNNTenTurnDetector.Load(directory, semanticThreads, backendType);
                    return new MNNTurnDetector(ten, () => ten.Backend, true, (a, r, t, c, l) => ten.IsEndOfTurn(t, c));
                case MNNLiveKitMultilingual.ConfigFileName:
                    var multilingual = MNNLiveKitMultilingual.Load(directory, threads, backendType);
                    return new MNNTurnDetector(multilingual, () => multilingual.Backend, true, (a, r, t, c, l) => multilingual.IsEndOfTurn(t, l, c));
                case MNNBairongTurnSenseConfig.FileName:
                    var bairong = MNNBairongTurnSense.Load(directory, semanticThreads,
                        backendType == MNNBackendType.Auto ? MNNBackendType.CPU : backendType);
                    return new MNNTurnDetector(bairong, () => bairong.Backend, false, (a, r, t, c, l) => bairong.Predict(a, r, c).IsComplete);
                case MNNSmartTurnConfig.FileName:
                    var smart = MNNSmartTurn.Load(directory, threads, backendType);
                    return new MNNTurnDetector(smart, () => smart.Backend, false, (a, r, t, c, l) => smart.Predict(a, r, cancellationToken: c).IsComplete);
                case MNNNamoTurnDetectorConfig.FileName:
                    var namo = MNNNamoTurnDetector.Load(directory, threads, backendType);
                    return new MNNTurnDetector(namo, () => namo.Backend, true, (a, r, t, c, l) => namo.Predict(t, c) >= .5f);
                case MNNTurnSenseConfig.FileName:
                    var sense = MNNTurnSense.Load(directory, threads, backendType);
                    return new MNNTurnDetector(sense, () => sense.Backend, true, (a, r, t, c, l) => sense.Predict(t, c).IsComplete);
                case MNNLiveKitTurnDetectorConfig.FileName:
                    var live = MNNLiveKitTurnDetector.Load(directory, lightThreads, backendType);
                    return new MNNTurnDetector(live, () => live.Backend, true, (a, r, t, c, l) => live.Predict(t, cancellationToken: c) >= .5f);
                case MNNCueLightConfig.FileName:
                    var cue = MNNCueLightTurnDetector.Load(directory, lightThreads, backendType);
                    return new MNNTurnDetector(cue, () => cue.Backend, false, (a, r, t, c, l) => {
                        var frames = cue.Analyze(a, r, cancellationToken: c).Frames;
                        // Endpoints can occur before trailing silence. Preserve a fired endpoint for this candidate.
                        foreach (var frame in frames) if (frame.CarefulEndpoint) return true;
                        return false;
                    });
                case MNNVoxTurnModel.ConfigFile:
                    var vox = MNNVoxTurn.Load(directory, threads, backendType);
                    return new MNNTurnDetector(vox, () => vox.FirstLanguageBackend, false, (a, r, t, c, l) =>
                        vox.Predict(new MNNVoxTurnRequest(MNNSpeechDsp.Resample(a, r, 16000, c), t),
                            new[] { "eot" }, c).Answers["eot"].Probability("yes") >= .5f);
                case MNNDualTurnEndpointingConfig.FileName:
                    var dual = MNNDualTurnEndpointing.Load(directory, lightThreads, backendType);
                    return new MNNTurnDetector(dual, () => dual.Backend, false, (a, r, t, c, l) => PredictDual(dual, a, r, c));
                default: throw new InvalidDataException("No supported turn export profile in " + directory);
            }
        }
        public bool IsComplete(float[] audio, int sampleRate, string transcript = null, CancellationToken cancellationToken = default)
            => IsComplete(audio, sampleRate, transcript, "en", cancellationToken);

        /// <summary>Uses the model's published threshold for the supplied transcript language.</summary>
        public bool IsComplete(float[] audio, int sampleRate, string transcript, string language, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (RequiresTranscript && string.IsNullOrWhiteSpace(transcript)) return false;
            return _predict(audio, sampleRate, transcript, cancellationToken, language);
        }
        private static bool PredictDual(MNNDualTurnEndpointing model, float[] audio, int rate, CancellationToken token)
        {
            var pcm = MNNSpeechDsp.Resample(audio, rate, MNNDualTurnEndpointing.SampleRate, token);
            model.Reset();
            var chunk = new float[MNNDualTurnEndpointing.TickSamples];
            float probability = 0;
            for (int offset = 0; offset < pcm.Length; offset += chunk.Length)
            {
                Array.Clear(chunk, 0, chunk.Length);
                Array.Copy(pcm, offset, chunk, 0, Math.Min(chunk.Length, pcm.Length - offset));
                probability = model.Push(chunk, cancellationToken: token).EndOfTurnProbability;
            }
            return probability >= .5f;
        }
        public void Dispose() => _owner.Dispose();
    }
}
