using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;

namespace MNN.Unity
{
    public static partial class MNNConversationLocalProviders
    {
        private sealed class ParaformerRecognition : Provider, IMNNConversationStreamingAsrProvider
        {
            private readonly int _threads;
            private MNNParaformerRecognizer _model;
            protected override string ReadBackend() => _model == null ? "Not loaded" :
                "Paraformer encoder: " + _model.Backend + " · decoder: " + _model.DecoderBackend + " · stateful 600 ms advances";
            internal ParaformerRecognition(MNNRealtimeCapabilities caps, SynchronizationContext unity,
                string directory, int threads, MNNBackendType backend) : base(caps, unity, directory)
            { _threads = threads; Backend = backend; }
            protected override void Load() => _model = MNNParaformerRecognizer.Load(Directory, threadCount: _threads, backendType: Backend);
            public async Task<string> TranscribeAsync(float[] audio, int sampleRate, CancellationToken token)
            {
                try { return (await _model.RecognizeAsync(audio, sampleRate, token).ConfigureAwait(false)).Text; }
                finally { PublishBackend(); }
            }
            public IMNNConversationRecognitionStream CreateStream() => new RecognitionStream(_model.CreateStream());
            protected override void ReleaseModels() { _model?.Dispose(); _model = null; }
        }
        private sealed class RecognitionStream : IMNNConversationRecognitionStream
        {
            private readonly MNNParaformerRecognizer.Stream _stream;
            internal RecognitionStream(MNNParaformerRecognizer.Stream stream) { _stream = stream; }
            public string AcceptWaveform(float[] samples, int offset, int count, CancellationToken token)
                => _stream.AcceptWaveform(samples, offset, count, token);
            public void Reset() => _stream.Reset();
            public void Dispose() => _stream.Dispose();
        }
    }
}
