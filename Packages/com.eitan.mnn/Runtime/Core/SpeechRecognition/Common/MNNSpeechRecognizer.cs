using System;
using System.IO;
using System.Text;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Profile-selected ASR over the package's existing task APIs. Owns its runner.</summary>
    public sealed class MNNSpeechRecognizer : IDisposable
    {
        private readonly IDisposable _owner;
        private readonly Func<float[], int, CancellationToken, MNNSpeechRecognition> _recognize;
        private readonly Func<MNNBackendType> _backend;
        public MNNBackendType Backend => _backend();
        private MNNSpeechRecognizer(IDisposable owner, Func<MNNBackendType> backend,
            Func<float[], int, CancellationToken, MNNSpeechRecognition> recognize)
        { _owner = owner; _backend = backend; _recognize = recognize; }
        public static bool SupportsRepository(MNNRemoteModel model)
        {
            string repository = model?.repositoryId ?? model?.modelScopeId;
            return model != null && (MNNZipformerModel.IsRepository(repository) ||
                model.provider == MNNModelSource.HuggingFace && MNNWhisperModel.IsRepository(repository) ||
                model.provider == MNNModelSource.ModelScope && MNNWhisperModel.IsModelScopeRepository(repository));
        }
        public static string ProfileFile(string directory)
        {
            foreach (string file in new[] { MNNQwen3AsrConfig.FileName, MNNParaformerConfig.FileName,
                MNNWhisperConfig.FileName, MNNSenseVoiceConfig.FileName, MNNZipformerConfig.FileName })
                if (File.Exists(Path.Combine(directory, file))) return file;
            return null;
        }
        public static MNNSpeechRecognizer Load(string directory, int threadCount = 4,
            MNNBackendType backendType = MNNBackendType.Auto, string language = null)
        {
            switch (ProfileFile(directory))
            {
                case MNNQwen3AsrConfig.FileName:
                    var qwen = MNNQwen3AsrRecognizer.Load(directory, string.IsNullOrWhiteSpace(language) ? null : language,
                        threadCount, backendType: backendType);
                    return new MNNSpeechRecognizer(qwen, () => qwen.Backend, (audio, rate, token) => qwen.Recognize(audio, rate, token));
                case MNNParaformerConfig.FileName:
                    var para = MNNParaformerRecognizer.Load(directory, threadCount: threadCount, backendType: backendType);
                    return new MNNSpeechRecognizer(para, () => para.Backend, para.Recognize);
                case MNNWhisperConfig.FileName:
                    var config = MNNWhisperConfig.Read(directory);
                    if (!string.IsNullOrWhiteSpace(language) && language != "auto") config.language = language;
                    var whisper = MNNWhisperRecognizer.Load(directory, config, threadCount);
                    return new MNNSpeechRecognizer(whisper, () => whisper.Backend, whisper.Recognize);
                case MNNSenseVoiceConfig.FileName:
                    var senseConfig = MNNSenseVoiceConfig.Read(directory);
                    if (!string.IsNullOrWhiteSpace(language)) senseConfig.language = language;
                    var sense = MNNSenseVoiceRecognizer.Load(directory, senseConfig, threadCount, backendType);
                    return new MNNSpeechRecognizer(sense, () => sense.Backend, sense.Recognize);
                case MNNZipformerConfig.FileName:
                    var zip = MNNZipformerRecognizer.Load(directory, threadCount: threadCount, backendType: backendType);
                    return new MNNSpeechRecognizer(zip, () => zip.Backend, zip.Recognize);
                default: throw new InvalidDataException("No supported ASR export profile in " + directory);
            }
        }
        public string Transcribe(float[] audio, int sampleRate, CancellationToken cancellationToken = default)
        {
            if (audio == null || audio.Length == 0) throw new ArgumentException("Supply nonempty audio.", nameof(audio));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            var text = new StringBuilder();
            // Whisper's fixed context is shorter than 30 seconds. Bound every runner request,
            // preserving order without silently dropping the end of a long recording.
            int maximum = sampleRate * 20;
            for (int offset = 0; offset < audio.Length; offset += maximum)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var block = new float[Math.Min(maximum, audio.Length - offset)];
                Array.Copy(audio, offset, block, 0, block.Length);
                string part = _recognize(block, sampleRate, cancellationToken).Text;
                if (text.Length > 0 && !string.IsNullOrEmpty(part)) text.Append(' ');
                text.Append(part);
            }
            return text.ToString();
        }
        public void Dispose() => _owner.Dispose();
    }
}
