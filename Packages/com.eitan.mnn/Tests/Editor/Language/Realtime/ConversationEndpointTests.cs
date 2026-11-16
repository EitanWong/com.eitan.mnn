using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ConversationEndpointTests
    {
        private sealed class Capture : IMNNAudioCaptureSource
        {
            public MNNPcmFormat CaptureFormat => new MNNPcmFormat(16000, 1);
            public int MaximumCaptureSamples => 512;
            public long DroppedCaptureBlocks => 0;
            public bool HasPendingCaptureFrames => false;
            public bool TryReadCapture(float[] buffer, int offset, out MNNAudioFrameInfo info, out int count)
            { info = default; count = 0; return false; }
        }
        private sealed class Turn : IMNNConversationTurnProvider
        {
            public int Calls, Samples;
            public bool Complete;
            public MNNRealtimeCapabilities Capabilities => null;
            public Task PrepareAsync(CancellationToken token) => Task.CompletedTask;
            public Task<bool> IsTurnCompleteAsync(float[] audio, int rate, CancellationToken token)
            { ++Calls; Samples = audio.Length; return Task.FromResult(Complete); }
            public void Dispose() { }
        }
        private sealed class Runtime : MNNConversationRuntime
        {
            internal Runtime(Turn turn, bool filter = false) : base(new MNNConversationPorts(new Capture(), () => null),
                new MNNConversationOptions { speechOutput = false, filterVoiceInput = filter },
                new MNNRealtimeOutputPipeline(new MNNPcmFormat(24000, 1), 512),
                MNNRealtimeRoute.CascadedSpeech, new MNNConversationProviders { Turn = turn }) { }
            internal void Append(bool speech, int frames)
            {
                var frame = new float[512];
                if (speech) for (int i = 0; i < frame.Length; ++i) frame[i] = (float)(.1 * Math.Sin(i * .1));
                for (int i = 0; i < frames; ++i) _utterance.Append(frame, speech);
            }
            internal bool Evaluate()
            {
                if (ShouldEvaluateTurn())
                {
                    BeginTurnCandidate();
                    _turnTask.GetAwaiter().GetResult();
                    FinishTurnCandidate();
                }
                return _acceptedAudio != null;
            }
        }
        [Test]
        public void NegativeModelCannotHoldACompletedUtteranceUntilRecordingCapacity()
        {
            var turn = new Turn(); var runtime = new Runtime(turn);
            runtime.Append(true, 10); runtime.Append(false, 18);
            Assert.False(runtime.Evaluate(), "A short pause still respects the semantic detector.");
            runtime.Append(false, 45);
            bool accepted = runtime.Evaluate();
            TestContext.WriteLine("silence_ms=2016; accepted=" + accepted + "; detector_calls=" + turn.Calls);
            Assert.True(accepted, "Continuous silence must eventually submit without waiting 30 seconds.");
        }
        [Test]
        public void PositiveModelSubmitsAtFirstCandidateWithoutWaitingForFallback()
        {
            var turn = new Turn { Complete = true }; var runtime = new Runtime(turn);
            runtime.Append(true, 10); runtime.Append(false, 18);
            Assert.True(runtime.Evaluate()); Assert.AreEqual(1, turn.Calls);
        }
        [Test]
        public void RetryPreservesSpeechInsteadOfFeedingAnEverGrowingSilentTail()
        {
            var turn = new Turn(); var runtime = new Runtime(turn);
            runtime.Append(true, 10); runtime.Append(false, 18); runtime.Evaluate();
            int first = turn.Samples;
            runtime.Append(false, 17); runtime.Evaluate();
            TestContext.WriteLine("first_samples=" + first + "; retry_samples=" + turn.Samples);
            Assert.LessOrEqual(turn.Samples, 10 * 512 + 800 * 16);
            Assert.AreEqual(2, turn.Calls);
        }
        [Test]
        public void ResumedSpeechRestartsTheSilenceBudget()
        {
            var runtime = new Runtime(new Turn());
            runtime.Append(true, 10); runtime.Append(false, 55); runtime.Evaluate();
            runtime.Append(true, 2); runtime.Append(false, 18);
            Assert.False(runtime.Evaluate(), "Previous silence cannot force completion after speech resumes.");
        }
        [Test]
        public void SilenceFallbackStillRejectsVoiceWithoutRequiredValidation()
        {
            var runtime = new Runtime(new Turn(), filter: true);
            runtime.Append(true, 10); runtime.Append(false, 63);
            Assert.False(runtime.Evaluate());
            Assert.AreEqual("No speech recognition provider for voice validation", runtime.LastVoiceRejection);
        }
    }
}
