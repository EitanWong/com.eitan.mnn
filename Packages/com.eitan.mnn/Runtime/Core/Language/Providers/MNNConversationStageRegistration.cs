using System;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;

namespace MNN.Unity
{
    public sealed class MNNConversationStageRegistration
    {
        public readonly MNNRealtimeCapabilities Capabilities;
        public readonly Func<IMNNConversationProvider> Create;
        public readonly Func<CancellationToken, Task> Install;
        public Action Prepared { get; set; }
        public MNNConversationStageRegistration(MNNRealtimeCapabilities capabilities, Func<IMNNConversationProvider> create,
            Func<CancellationToken, Task> install = null)
        { Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities)); Create = create; Install = install; }
        public static MNNConversationStageRegistration Energy => new MNNConversationStageRegistration(new MNNRealtimeCapabilities(
            MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.VoiceActivityDetection | MNNRealtimeFeature.BoundedScheduling,
            "Managed RMS VAD v1", "Fixed 512-sample frame detector on the shared capture worker.",
            "Baseline energy detection; use a model VAD for noisy environments.", execution: MNNRealtimeExecution.Managed), null);
        public async Task PrepareAsync(IMNNConversationProvider provider, CancellationToken token)
        {
            if (provider == null) throw new InvalidOperationException("The provider factory returned null.");
            var actual = provider.Capabilities;
            if (!Matches(actual, Capabilities))
                throw new InvalidOperationException("The created provider capabilities do not match the preflight registration.");
            if (Install != null) await Install(token).ConfigureAwait(false);
            await provider.PrepareAsync(token).ConfigureAwait(false);
            if (!Matches(provider.Capabilities, Capabilities))
                throw new InvalidOperationException("The prepared provider capabilities no longer match the preflight registration.");
            Prepared?.Invoke();
        }

        private static bool Matches(MNNRealtimeCapabilities actual, MNNRealtimeCapabilities expected) => actual != null &&
            actual.Execution == expected.Execution && actual.Features == expected.Features &&
            string.Equals(actual.Contract, expected.Contract, StringComparison.Ordinal) &&
            string.Equals(actual.Evidence, expected.Evidence, StringComparison.Ordinal) &&
            string.Equals(actual.Limitation, expected.Limitation, StringComparison.Ordinal) &&
            actual.SustainedRtf == expected.SustainedRtf &&
            actual.SustainedRtfWindowAudioSeconds.Equals(expected.SustainedRtfWindowAudioSeconds);
    }
}
