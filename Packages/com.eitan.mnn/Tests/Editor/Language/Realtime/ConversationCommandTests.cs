using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class ConversationCommandTests
    {
        [Test]
        public void CommandsCannotRecordFromAnUnrelatedInputOwner()
        {
            var input = new MNNConversationInputState();
            Assert.Throws<ArgumentException>(() => new MNNConversationCommandBindings(input, new MNNConversationRecording(new MNNConversationInputState())));
        }

        [Test]
        public void CaptureCallbackFailureStillCompletesAllOwnedReservations()
        {
            var input = new MNNConversationInputState();
            var commands = new MNNConversationCommands(new MNNConversationCommandBindings(input, new MNNConversationRecording(input))
            {
                SessionState = () => MNNRealtimeSessionState.Running, SupportsRecordedVoice = () => true,
                CaptureFinishing = finishing => { if (!finishing) throw new InvalidOperationException("View failed."); },
                SubmitAudioAsync = (audio, token) => Task.CompletedTask
            });
            Assert.True(commands.BeginRecordingAsync().GetAwaiter().GetResult()); input.Consume(new float[4096]);
            var finish = commands.FinishRecordingAsync(false);
            Assert.True(finish.IsFaulted); Assert.IsNotNull(finish.Exception);
            Assert.True(commands.CaptureFinished.IsCompleted, "Callback failure cannot retain microphone ownership.");
            Assert.True(commands.SubmissionFinished.IsCompleted, "Shutdown must still be able to join the failed command.");
            Assert.False(commands.IsFinishingRecording);
        }
        [Test]
        public void PreparationCleanupFailureDoesNotLeaveAnUnfinishableEntrance()
        {
            var input = new MNNConversationInputState(); bool failed = false;
            var commands = new MNNConversationCommands(new MNNConversationCommandBindings(input, new MNNConversationRecording(input))
            {
                SessionState = () => MNNRealtimeSessionState.Running, SupportsRealtime = () => true,
                PrepareRealtimeAsync = token => { failed = true; return Task.FromException(new InvalidOperationException("Entrance failed.")); },
                SetCaptureEnabled = enabled => { if (failed && !enabled) throw new InvalidOperationException("Gate failed."); }
            });
            Task entrance = commands.SetRealtimeAsync(true);
            Assert.True(entrance.IsCompleted, "Cleanup errors must complete the published task.");
            Assert.True(entrance.IsFaulted); Assert.False(commands.IsPreparingRealtime);
            StringAssert.Contains("Entrance failed", entrance.Exception.ToString());
            StringAssert.Contains("Gate failed", entrance.Exception.ToString());
        }

        [UnityTest]
        public IEnumerator EntranceIsPublishedBeforeEffectsAndCancellationCannotEnableCapture()
        {
            var input = new MNNConversationInputState();
            var recording = new MNNConversationRecording(input);
            bool capture = false;
            var entered = new TaskCompletionSource<bool>();
            var release = new TaskCompletionSource<bool>();
            var bindings = new MNNConversationCommandBindings(input, recording)
            {
                SessionState = () => MNNRealtimeSessionState.Running,
                SupportsRealtime = () => true,
                SetCaptureEnabled = enabled => capture = enabled,
                PrepareRealtimeAsync = async token => { entered.TrySetResult(true); await release.Task; token.ThrowIfCancellationRequested(); }
            };
            var commands = new MNNConversationCommands(bindings);
            Task entrance = commands.SetRealtimeAsync(true);
            Assert.AreSame(entrance, commands.SetRealtimeAsync(true));
            Assert.True(entered.Task.IsCompleted); Assert.True(commands.IsPreparingRealtime);
            commands.SetRealtimeAsync(false);
            release.TrySetResult(true);
            while (!entrance.IsCompleted) yield return null;
            Assert.False(entrance.IsFaulted, entrance.Exception?.ToString());
            Assert.False(input.Realtime); Assert.False(capture);
        }

        [UnityTest]
        public IEnumerator NextHoldJoinsPhysicalTailButDoesNotWaitForCancelledTranscription()
        {
            var input = new MNNConversationInputState();
            var pause = new TaskCompletionSource<bool>();
            var asr = new TaskCompletionSource<string>();
            var recording = new MNNConversationRecording(input) { PauseCaptureAsync = token => pause.Task };
            var bindings = new MNNConversationCommandBindings(input, recording)
            {
                SessionState = () => MNNRealtimeSessionState.Running,
                SupportsRecordedVoice = () => true,
                SupportsTranscription = () => true,
                TranscribeAsync = (audio, token) => asr.Task
            };
            var commands = new MNNConversationCommands(bindings);
            Assert.True(commands.BeginRecordingAsync().GetAwaiter().GetResult()); input.Consume(new float[4096]);
            Task<string> first = commands.FinishRecordingAsync(true);
            Assert.False(commands.CaptureFinished.IsCompleted);
            Task<bool> next = commands.BeginRecordingAsync();
            Assert.False(next.IsCompleted);
            pause.TrySetResult(true);
            while (!next.IsCompleted) yield return null;
            Assert.True(next.GetAwaiter().GetResult()); Assert.True(input.Recording);
            input.Consume(new float[4096]);
            commands.CancelRecording();
            while (!first.IsCompleted) yield return null;
            Assert.True(first.IsCanceled); Assert.False(input.Recording);
        }

        [UnityTest]
        public IEnumerator RecordingGapOrDrainTimeoutCannotProducePartialVoiceMessage()
        {
            var input = new MNNConversationInputState(); long lost = 0;
            var recording = new MNNConversationRecording(input) { LostInputFrames = () => lost };
            recording.Begin(); input.Consume(new float[4096]); ++lost;
            Task drain = recording.DrainAsync(CancellationToken.None);
            while (!drain.IsCompleted) yield return null;
            Assert.True(drain.IsFaulted);
            StringAssert.Contains("missing", drain.Exception.GetBaseException().Message);
            input.FinishRecording(true);
        }
        [UnityTest]
        public IEnumerator DuplicateFinishCannotClearTheRecordingOwnedByItsFirstDrain()
        {
            var input = new MNNConversationInputState(); var pause = new TaskCompletionSource<bool>();
            var recording = new MNNConversationRecording(input) { PauseCaptureAsync = _ => pause.Task };
            var commands = new MNNConversationCommands(new MNNConversationCommandBindings(input, recording)
            { SessionState = () => MNNRealtimeSessionState.Running, SupportsRecordedVoice = () => true, SubmitAudioAsync = (pcm, token) => Task.CompletedTask });
            Assert.True(commands.BeginRecordingAsync().GetAwaiter().GetResult()); input.Consume(new float[4096]);
            Task<string> first = commands.FinishRecordingAsync(false), duplicate = commands.FinishRecordingAsync(false);
            bool retained = input.Recording;
            pause.TrySetResult(true);
            while (!first.IsCompleted || !duplicate.IsCompleted) yield return null;
            Assert.True(duplicate.IsFaulted); Assert.IsInstanceOf<InvalidOperationException>(duplicate.Exception.GetBaseException());
            Assert.True(retained, "The duplicate caller cannot clear another operation's PCM.");
            Assert.False(first.IsFaulted, first.Exception?.ToString());
        }
        [UnityTest]
        public IEnumerator CancelledProviderThatIgnoresItsTokenCannotReturnAnOldEditableDraft()
        {
            var input = new MNNConversationInputState(); var asr = new TaskCompletionSource<string>(); var entered = new TaskCompletionSource<bool>();
            var commands = new MNNConversationCommands(new MNNConversationCommandBindings(input, new MNNConversationRecording(input))
            {
                SessionState = () => MNNRealtimeSessionState.Running, SupportsRecordedVoice = () => true, SupportsTranscription = () => true,
                TranscribeAsync = (pcm, token) => { entered.TrySetResult(true); return asr.Task; }
            });
            Assert.True(commands.BeginRecordingAsync().GetAwaiter().GetResult()); input.Consume(new float[4096]);
            Task<string> first = commands.FinishRecordingAsync(true);
            while (!entered.Task.IsCompleted) yield return null;
            Task<bool> next = commands.BeginRecordingAsync();
            Assert.True(next.IsCompleted); Assert.True(next.GetAwaiter().GetResult()); Assert.True(input.Recording);
            Assert.False(first.IsCompleted, "A cancelled provider does not own the microphone.");
            commands.CancelRecording(); asr.TrySetResult("stale draft");
            while (!first.IsCompleted) yield return null;
            Assert.True(first.IsCanceled, "A stale draft must not overwrite the next input.");
        }
    }
}
