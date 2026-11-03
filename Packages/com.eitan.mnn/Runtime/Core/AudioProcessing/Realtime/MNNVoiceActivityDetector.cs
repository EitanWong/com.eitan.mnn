using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>One owned VAD runner, selected by its installed export profile, for Editor and Player.</summary>
    public sealed class MNNVoiceActivityDetector : IDisposable
    {
        private readonly IDisposable _owner;
        private readonly Func<float[], int, float, CancellationToken, MNNVoiceActivityResult> _detect;
        public MNNBackendType Backend { get; }
        private MNNVoiceActivityDetector(IDisposable owner, MNNBackendType backend,
            Func<float[], int, float, CancellationToken, MNNVoiceActivityResult> detect)
        { _owner = owner; Backend = backend; _detect = detect; }

        public static bool HasProfile(string directory) => ProfileFile(directory) != null;
        public static string ProfileFile(string directory)
        {
            foreach (string file in new[] { MNNSileroVadConfig.FileName, MNNFsmnVadConfig.FileName,
                MNNMarbleNetVadConfig.FileName, MNNCueLightConfig.FileName })
                if (File.Exists(Path.Combine(directory, file))) return file;
            return null;
        }

        public static MNNVoiceActivityDetector Load(string directory, int threadCount = 1, MNNBackendType backendType = MNNBackendType.Auto)
        {
            switch (ProfileFile(directory))
            {
                case MNNSileroVadConfig.FileName:
                    var silero = MNNSileroVad.Load(directory, threadCount, backendType);
                    return new MNNVoiceActivityDetector(silero, silero.Backend, silero.Detect);
                case MNNFsmnVadConfig.FileName:
                    var fsmn = MNNFsmnVad.Load(directory, threadCount, backendType);
                    return new MNNVoiceActivityDetector(fsmn, fsmn.Backend, fsmn.Detect);
                case MNNMarbleNetVadConfig.FileName:
                    var marble = MNNMarbleNetVad.Load(directory, threadCount, backendType);
                    return new MNNVoiceActivityDetector(marble, marble.Backend, marble.Detect);
                case MNNCueLightConfig.FileName:
                    var cue = MNNCueLightTurnDetector.Load(directory, threadCount, backendType);
                    return new MNNVoiceActivityDetector(cue, cue.Backend, (audio, rate, threshold, token) =>
                        CueActivity(cue.Analyze(audio, rate, cancellationToken: token), threshold));
                default: throw new InvalidDataException("No supported VAD export profile in " + directory);
            }
        }

        public MNNVoiceActivityResult Detect(float[] audio, int sampleRate, float threshold = .5f, CancellationToken cancellationToken = default) =>
            _detect(audio, sampleRate, threshold, cancellationToken);

        private static MNNVoiceActivityResult CueActivity(MNNCueLightAnalysis analysis, float threshold)
        {
            if (!(threshold > 0 && threshold < 1)) throw new ArgumentOutOfRangeException(nameof(threshold));
            var segments = new List<MNNSpeechSegment>();
            int start = -1;
            for (int i = 0; i <= analysis.FrameCount; ++i)
            {
                bool active = i < analysis.FrameCount && analysis.Frames[i].SpeechProbability >= threshold;
                if (active && start < 0) start = i;
                if (!active && start >= 0) { segments.Add(new MNNSpeechSegment(start * .02, i * .02)); start = -1; }
            }
            return new MNNVoiceActivityResult(segments.ToArray(), analysis.Frames.Select(frame => frame.SpeechProbability).ToArray(), analysis.DurationSeconds);
        }
        public void Dispose() => _owner.Dispose();
    }
}
