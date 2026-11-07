using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;

namespace MNN.Unity
{
    public static partial class MNNConversationLocalProviders
    {
        private sealed class ProfiledRecognition : Provider, IMNNConversationAsrProvider
        {
            private MNNSpeechRecognizer _model;
            private readonly int _threads;
            private readonly string _language;
            internal ProfiledRecognition(MNNRealtimeCapabilities caps, SynchronizationContext unity, string directory, int threads,
                MNNBackendType backend, string language) : base(caps, unity, directory)
            { _threads = threads; _language = language; Backend = backend; }
            protected override void Load() => _model = MNNSpeechRecognizer.Load(Directory, _threads, Backend, _language);
            protected override string ReadBackend() => _model == null ? "Not loaded" : "Recognition: " + _model.Backend;
            public async Task<string> TranscribeAsync(float[] audio, int rate, CancellationToken token)
            {
                try { return await Task.Run(() => _model.Transcribe(audio, rate, token), token).ConfigureAwait(false); }
                finally { PublishBackend(); }
            }
            protected override void ReleaseModels() { _model?.Dispose(); _model = null; }
        }
        private sealed class ProfiledSpeech : Provider, IMNNConversationTtsProvider, IMNNConversationSpeechLanguages
        {
            private MNNSpeechSynthesizer _model;
            private readonly int _threads;
            private readonly MNNSpeechSynthesisOptions _options;
            internal ProfiledSpeech(MNNRealtimeCapabilities caps, SynchronizationContext unity, string directory, int threads,
                MNNBackendType backend, MNNSpeechSynthesisOptions options) : base(caps, unity, directory)
            { _threads = threads; Backend = backend; _options = options; }
            protected override void Load()
            {
                if (!string.IsNullOrWhiteSpace(_options.ReferenceWavePath) && !Path.IsPathRooted(_options.ReferenceWavePath))
                    _options.ReferenceWavePath = Path.GetFullPath(Path.Combine(Directory, _options.ReferenceWavePath));
                _model = MNNSpeechSynthesizer.Load(Directory, _options, _threads, Backend);
            }
            public int SampleRate => _model?.SampleRate ?? 24000;
            public System.Collections.Generic.IReadOnlyList<string> SupportedSpeechLanguages => _model?.Languages ?? Array.Empty<string>();
            public async Task SynthesizeAsync(string text, Func<float[], int, Task> pcm, CancellationToken token)
            {
                var result = await Task.Run(() => _model.Synthesize(text, token), token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                if (result.SampleRate != SampleRate) throw new InvalidOperationException("Unexpected synthesis sample rate.");
                await pcm(result.Waveform, result.Waveform.Length).ConfigureAwait(false);
            }
            protected override void ReleaseModels() { _model?.Dispose(); _model = null; }
        }
    }
}
