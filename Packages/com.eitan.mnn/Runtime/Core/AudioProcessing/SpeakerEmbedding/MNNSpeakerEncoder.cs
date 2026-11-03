using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Owned profiled speaker encoder, including the VBx 8/16 kHz frontend.</summary>
    public sealed class MNNSpeakerEncoder : IDisposable
    {
        private readonly IDisposable _owner;
        private readonly Func<float[], int, CancellationToken, float[]> _encode;
        private MNNSpeakerEncoder(IDisposable owner, Func<float[], int, CancellationToken, float[]> encode)
        { _owner = owner; _encode = encode; }
        public static string ProfileFile(string directory) => File.Exists(Path.Combine(directory, MNNVbxConfig.FileName))
            ? MNNVbxConfig.FileName : File.Exists(Path.Combine(directory, MNNWeSpeakerModelConfig.FileName))
                ? MNNWeSpeakerModelConfig.FileName : File.Exists(Path.Combine(directory, MNNSpeakerModelConfig.FileName)) ? MNNSpeakerModelConfig.FileName : null;
        public static MNNSpeakerEncoder Load(string directory, int threadCount = 2, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (ProfileFile(directory) == MNNVbxConfig.FileName)
            { var model = MNNVbxEmbedding.Load(directory, threadCount, backendType); return new MNNSpeakerEncoder(model, (audio, rate, token) => model.Encode(
                    MNNSpeechDsp.Resample(audio, rate, model.InputSampleRate, token), model.InputSampleRate, token)); }
            if (ProfileFile(directory) == MNNWeSpeakerModelConfig.FileName)
            {
                var model = MNNWeSpeakerEmbedding.Load(directory, threadCount, backendType);
                return new MNNSpeakerEncoder(model, model.Encode);
            }
            var speaker = MNNSpeakerEmbedding.Load(directory, threadCount, backendType);
            return new MNNSpeakerEncoder(speaker, speaker.Encode);
        }
        public float[] Encode(float[] audio, int sampleRate = 16000, CancellationToken cancellationToken = default) =>
            _encode(audio, sampleRate, cancellationToken);
        public void Dispose() => _owner.Dispose();
    }
}
