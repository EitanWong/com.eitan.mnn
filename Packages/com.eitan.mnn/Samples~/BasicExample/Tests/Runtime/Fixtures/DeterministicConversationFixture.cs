using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;

namespace MNN.Examples.Tests
{
    /// <summary>Injected acceptance inputs only. No real recognition, language model, synthesis or hardware.</summary>
    internal sealed class DeterministicConversationFixture
    {
        internal readonly InjectedAudioBinding Binding = new InjectedAudioBinding();
        internal readonly TextProvider Model = new TextProvider();
        internal readonly RecognitionProvider Recognition = new RecognitionProvider();
        internal readonly SpeechProvider Speech = new SpeechProvider();
        internal readonly MNNRealtimeCapabilities DeviceCapabilities = Caps(
            MNNRealtimeFeature.DuplexDevice | MNNRealtimeFeature.HalfDuplexDevice | MNNRealtimeFeature.PresentationClock);
        internal MNNConversationRegistrations Registrations => new MNNConversationRegistrations
        {
            Model = Registration(Model), Asr = Registration(Recognition), Tts = Registration(Speech),
            Vad = MNNConversationStageRegistration.Energy, AllowBufferedSpeech = true
        };
        private static MNNConversationStageRegistration Registration(Provider provider) =>
            new MNNConversationStageRegistration(provider.Capabilities, () => provider);
        private static MNNRealtimeCapabilities Caps(MNNRealtimeFeature features) => new MNNRealtimeCapabilities(
            features, "Deterministic sample acceptance v1", "Injected deterministic data; no model or hardware proof.",
            execution: MNNRealtimeExecution.Managed);

        internal abstract class Provider : IMNNConversationProvider
        {
            public MNNRealtimeCapabilities Capabilities { get; }
            internal int PrepareCalls, DisposeCalls;
            protected Provider(MNNRealtimeFeature features) { Capabilities = Caps(features); }
            public Task PrepareAsync(CancellationToken token)
            { token.ThrowIfCancellationRequested(); ++PrepareCalls; return Task.CompletedTask; }
            public virtual void Dispose() => ++DisposeCalls;
        }
        internal sealed class TextProvider : Provider, IMNNConversationTextProvider
        {
            internal bool BlockGeneration, DisposedWhileGenerating;
            internal Exception GenerationFailure;
            private bool _generating;
            internal string LastUserText;
            internal readonly TaskCompletionSource<bool> GenerationStarted = NewSignal(), GenerationCleanup = NewSignal();
            internal TextProvider() : base(MNNRealtimeFeature.TextInput | MNNRealtimeFeature.StableSpokenText | MNNRealtimeFeature.ContextRecovery) { }
            public async Task GenerateAsync(IReadOnlyList<MNNOpenAIMessage> history, float[] audio, int sampleRate,
                Func<string, Task> textDelta, CancellationToken token)
            {
                LastUserText = history[history.Count - 1].Text; _generating = true;
                GenerationStarted.TrySetResult(true);
                try
                {
                    if (GenerationFailure != null) throw GenerationFailure;
                    if (BlockGeneration)
                    {
                        try { await Task.Delay(Timeout.Infinite, token); }
                        finally { await GenerationCleanup.Task; }
                    }
                    token.ThrowIfCancellationRequested();
                    await textDelta("deterministic "); await textDelta("reply.");
                }
                finally { _generating = false; }
            }
            public override void Dispose() { DisposedWhileGenerating |= _generating; base.Dispose(); }
        }
        internal sealed class RecognitionProvider : Provider, IMNNConversationAsrProvider
        {
            internal int ReceivedSamples;
            internal RecognitionProvider() : base(MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.SpeechRecognition) { }
            public Task<string> TranscribeAsync(float[] audio, int sampleRate, CancellationToken token)
            { token.ThrowIfCancellationRequested(); ReceivedSamples = audio.Length; return Task.FromResult("deterministic input"); }
        }
        internal sealed class SpeechProvider : Provider, IMNNConversationTtsProvider
        {
            internal SpeechProvider() : base(MNNRealtimeFeature.IndependentTts | MNNRealtimeFeature.BufferedSpeech) { }
            public int SampleRate => 16000;
            public Task SynthesizeAsync(string text, Func<float[], int, Task> pcm, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var samples = new float[640];
                for (int i = 0; i < samples.Length; ++i) samples[i] = .125f;
                return pcm(samples, samples.Length);
            }
        }
        private static TaskCompletionSource<bool> NewSignal() => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
