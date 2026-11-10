using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
namespace MNN.Unity
{
    public sealed class MNNConversationProviders
    {
        public IMNNConversationProvider Model, Tts, Asr, Vad, Turn;
        public Func<IMNNConversationProvider, CancellationToken, Task> Prepare;
        public Action<IMNNConversationProvider> Release;
        public Func<MNNConversationSpeechTimings> SpeechTimings;
        public static readonly IEqualityComparer<IMNNConversationProvider> Identity = new ProviderIdentity();
        private sealed class ProviderIdentity : IEqualityComparer<IMNNConversationProvider>
        {
            public bool Equals(IMNNConversationProvider x, IMNNConversationProvider y) => ReferenceEquals(x, y);
            public int GetHashCode(IMNNConversationProvider value) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
        }
        public MNNConversationProviders Snapshot() => (MNNConversationProviders)MemberwiseClone();
        internal Task PrepareAsync(IMNNConversationProvider provider, CancellationToken token) => provider == null ? Task.CompletedTask :
            Prepare != null ? Prepare(provider, token) : provider.PrepareAsync(token);
        public void Dispose(IMNNConversationProvider provider) { if (provider == null) return; if (Release != null) Release(provider); else provider.Dispose(); }
    }
    public readonly struct MNNConversationSpeechTimings
    {
        public readonly double TokenGenerationMs, FlowMs, VocoderMs;
        public MNNConversationSpeechTimings(double tokens, double flow, double vocoder) { TokenGenerationMs = tokens; FlowMs = flow; VocoderMs = vocoder; }
    }
}
