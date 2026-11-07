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
        private readonly Func<float[], int, string, CancellationToken, bool> _predict;
        public bool RequiresTranscript { get; }
        private readonly Func<MNNBackendType> _backend;
        public MNNBackendType Backend => _backend();
        private MNNTurnDetector(IDisposable owner, Func<MNNBackendType> backend, bool text,
            Func<float[], int, string, CancellationToken, bool> predict)
        { _owner = owner; _backend = backend; RequiresTranscript = text; _predict = predict; }
        public static string ProfileFile(string directory)
        {
            foreach (string file in new[] { MNNSmartTurnConfig.FileName, MNNNamoTurnDetectorConfig.FileName,
                MNNTurnSenseConfig.FileName, MNNLiveKitTurnDetectorConfig.FileName, MNNCueLightConfig.FileName,
                MNNVoxTurnModel.ConfigFile, MNNDualTurnEndpointingConfig.FileName })
                if (File.Exists(Path.Combine(directory, file))) return file;
            return null;
        }
        public static MNNTurnDetector Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            switch (ProfileFile(directory))
            {
                case MNNSmartTurnConfig.FileName:
                    var smart = MNNSmartTurn.Load(directory, threadCount, backendType);
                    return new MNNTurnDetector(smart, () => smart.Backend, false, (a, r, t, c) => smart.Predict(a, r, cancellationToken: c).IsComplete);
                case MNNNamoTurnDetectorConfig.FileName:
                    var namo = MNNNamoTurnDetector.Load(directory, threadCount, backendType);
                    return new MNNTurnDetector(namo, () => namo.Backend, true, (a, r, t, c) => namo.Predict(t, c) >= .5f);
                case MNNTurnSenseConfig.FileName:
                    var sense = MNNTurnSense.Load(directory, threadCount, backendType);
                    return new MNNTurnDetector(sense, () => sense.Backend, true, (a, r, t, c) => sense.Predict(t, c).IsComplete);
                case MNNLiveKitTurnDetectorConfig.FileName:
                    var live = MNNLiveKitTurnDetector.Load(directory, threadCount, backendType);
                    return new MNNTurnDetector(live, () => live.Backend, true, (a, r, t, c) => live.Predict(t, cancellationToken: c) >= .5f);
                case MNNCueLightConfig.FileName:
                    var cue = MNNCueLightTurnDetector.Load(directory, threadCount, backendType);
                    return new MNNTurnDetector(cue, () => cue.Backend, false, (a, r, t, c) => {
                        var frames = cue.Analyze(a, r, cancellationToken: c).Frames;
                        // Endpoints can occur before trailing silence. Preserve a fired endpoint for this candidate.
                        foreach (var frame in frames) if (frame.CarefulEndpoint) return true;
                        return false;
                    });
                case MNNVoxTurnModel.ConfigFile:
                    var vox = MNNVoxTurn.Load(directory, 4, backendType);
                    return new MNNTurnDetector(vox, () => vox.FirstLanguageBackend, false, (a, r, t, c) =>
                        vox.Predict(new MNNVoxTurnRequest(MNNSpeechDsp.Resample(a, r, 16000, c), t),
                            new[] { "eot" }, c).Answers["eot"].Probability("yes") >= .5f);
                case MNNDualTurnEndpointingConfig.FileName:
                    var dual = MNNDualTurnEndpointing.Load(directory, threadCount, backendType);
                    return new MNNTurnDetector(dual, () => dual.Backend, false, (a, r, t, c) => PredictDual(dual, a, r, c));
                default: throw new InvalidDataException("No supported turn export profile in " + directory);
            }
        }
        public bool IsComplete(float[] audio, int sampleRate, string transcript = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (RequiresTranscript && string.IsNullOrWhiteSpace(transcript)) return false;
            return _predict(audio, sampleRate, transcript, cancellationToken);
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
