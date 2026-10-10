using System;
using System.IO;
using System.Threading.Tasks;
using MNN.Unity.Interop;

namespace MNN.Unity
{
    public sealed partial class MNNLlm
    {
        public MNNModelCapabilities Capabilities
        {
            get
            {
                lock (_gate)
                {
                    ThrowIfDisposed();
                    return (MNNModelCapabilities)MNNInterop.MNN_Llm_getCapabilities(_handle);
                }
            }
        }

        /// <summary>One image and/or audio file plus text. Speech output currently supports Qwen2.5-Omni at 24 kHz.
        /// Audio input must be a decodable WAV file. Operations are serialized with text generation and disposal.</summary>
        public MNNMultimodalResult GenerateMultimodal(string prompt, string imagePath = null, string audioPath = null, int maxNewTokens = 128, bool generateSpeech = false, int maxAudioTokens = 256)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentNullException(nameof(prompt));
            if (maxNewTokens < 1 || maxNewTokens > 8192)
                throw new ArgumentOutOfRangeException(nameof(maxNewTokens));
            if (maxAudioTokens < 1 || maxAudioTokens > 2048)
                throw new ArgumentOutOfRangeException(nameof(maxAudioTokens));
            imagePath = ValidateMediaPath(imagePath);
            audioPath = ValidateMediaPath(audioPath);
            lock (_gate)
            {
                ThrowIfDisposed();
                var required = (imagePath != null ? MNNModelCapabilities.Vision : 0) | (audioPath != null ? MNNModelCapabilities.AudioInput : 0) | (generateSpeech ? MNNModelCapabilities.SpeechOutput : 0);
                if ((Capabilities & required) != required)
                    throw new NotSupportedException("This model does not support the requested media or speech output.");
                RequireSpeechCompatibility(generateSpeech);
                return UseNative(handle => MNNInterop.GenerateMedia(handle, prompt, imagePath, audioPath, generateSpeech, maxAudioTokens, maxNewTokens));
            }
        }

        public Task<MNNMultimodalResult> GenerateMultimodalAsync(string prompt, string imagePath = null, string audioPath = null, int maxNewTokens = 128, bool generateSpeech = false, int maxAudioTokens = 256) => Task.Run(() => GenerateMultimodal(prompt, imagePath, audioPath, maxNewTokens, generateSpeech, maxAudioTokens));
        private static string ValidateMediaPath(string path)
        {
            if (path == null)
                return null;
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Media path cannot be empty.", nameof(path));
            path = Path.GetFullPath(path);
            if (!File.Exists(path))
                throw new FileNotFoundException("Input media is missing.", path);
            return path;
        }
    }
}
