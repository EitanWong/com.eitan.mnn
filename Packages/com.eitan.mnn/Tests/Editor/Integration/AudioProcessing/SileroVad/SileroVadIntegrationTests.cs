using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SileroVad")]
    public class SileroVadIntegrationTests
    {
        private static string Root
        {
            get
            {
                string root = Environment.GetEnvironmentVariable("MNN_TEST_SILERO_ROOT");
                if (string.IsNullOrEmpty(root)) Assert.Ignore("Set MNN_TEST_SILERO_ROOT with existing pinned weights and ONNX reference fixtures; no downloads.");
                return root;
            }
        }
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Root, "Reference", name));
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            return values;
        }

        [TestCase("en"), TestCase("en-padded"), TestCase("zh-en"), TestCase("zh-en-padded")]
        [TestCase("silence"), TestCase("two-utterances")]
        public void ProbabilitiesAndIntervalsMatchIndependentOnnxReference(string fixture)
        {
            string directory = Path.Combine(Root, "Models");
            MNNSileroModel.Prepare(directory);
            var pcm = Read(fixture + "-pcm.f32");
            var reference = Read(fixture + "-probs.f32");
            using (var vad = MNNSileroVad.Load(directory))
            {
                var result = vad.Detect(pcm, 16000);
                Assert.AreEqual(MNNBackendType.CPU, vad.Backend);
                Assert.AreEqual(reference.Length, result.Probabilities.Count);
                var errors = result.Probabilities.Select((p, i) => Math.Abs(p - reference[i])).ToArray();
                TestContext.WriteLine(fixture + " max=" + errors.Max() + " mean=" + errors.Average());
                TestContext.WriteLine(string.Join("; ", result.Segments.Select(s => s.StartSeconds + "–" + s.EndSeconds)));
                File.WriteAllText(Path.Combine(Root, fixture + "-mnn-probabilities.txt"), string.Join("\n", result.Probabilities.Select(p => p.ToString("R", System.Globalization.CultureInfo.InvariantCulture))));
                Assert.Less(errors.Max(), fixture == "zh-en-padded" ? .1 : .08,
                    "The int8 export differs from float ONNX; zh-en-padded has a documented 0.0912 maximum probability drift.");
                Assert.Less(errors.Average(), .005);
                var expected = MNNSileroVad.Segment(reference, pcm.Length, .5f);
                Assert.AreEqual(expected.Length, result.Segments.Count);
                for (int i = 0; i < expected.Length; ++i)
                {
                    Assert.AreEqual(expected[i].StartSeconds, result.Segments[i].StartSeconds);
                    if (fixture == "zh-en-padded" && i == expected.Length - 1)
                    {
                        Assert.AreEqual(11.006, expected[i].EndSeconds);
                        Assert.AreEqual(10.942, result.Segments[i].EndSeconds,
                            "Known int8 boundary drift: 64 ms earlier than the corresponding float ONNX export.");
                    }
                    else Assert.AreEqual(expected[i].EndSeconds, result.Segments[i].EndSeconds);
                }
                Assert.AreEqual(0, vad.Detect(new float[16000], 16000).Segments.Count);
                Assert.Throws<OperationCanceledException>(() => vad.Detect(pcm, 16000, cancellationToken: new CancellationToken(true)));
                var next = vad.Detect(pcm, 16000);
                CollectionAssert.AreEqual(result.Probabilities, next.Probabilities);

                using (var realtime = MNNRealtimeVoiceActivity.Load(directory))
                {
                    var processor = new MNNRealtimeVoiceInputProcessor(new MNNPcmFormat(16000, 1), realtime,
                        maximumInputBlockFrames: 1201);
                    var streamed = ProcessIrregularBlocks(pcm, processor);
                    Assert.AreEqual(result.Probabilities.Count, streamed.Count);
                    for (int i = 0; i < streamed.Count; ++i)
                        Assert.That(streamed[i], Is.EqualTo(result.Probabilities[i]).Within(1e-6),
                            "Continuous and utterance inference diverged at frame " + i);

                    processor.Reset();
                    var repeated = ProcessIrregularBlocks(pcm, processor);
                    CollectionAssert.AreEqual(streamed, repeated,
                        "Reset must restore audio context and recurrent state deterministically.");
                }
            }
        }

        [Test]
        public void StudioRoutesRenamedPreparedDirectoryAndReportsIntervals()
        {
            string directory = Path.Combine(Root, "Models");
            MNNSileroModel.Prepare(directory);
            var model = MNNStudioModel.Read(directory);
            Assert.AreEqual(MNNStudioTask.AudioProcessing, model.Task);
            Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.AudioProcessing).CanRun(model));
            Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
            using (var backend = MNNStudioNativeBackend.Load(model, 4, false))
            {
                var result = backend.Run(new MNNStudioRequest {Audio = Path.Combine(Root, "../../ASR/Zipformer/Audio/en-0.wav")});
                Assert.AreEqual(1, result.VoiceActivity.Segments.Count);
                Assert.That(result.Text, Does.Contain("0.386–6.238 s"));
                Assert.That(result.BackendDescription, Does.Contain("Silero").And.Contain("CPU"));
            }
        }

        [UnityTest]
        public IEnumerator StudioWindowRunsAudioWorkspaceAndDisplaysNativeIntervals()
        {
            string directory = Path.Combine(Root, "Models");
            MNNSileroModel.Prepare(directory);
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            object Invoke(string name) => typeof(MNNChatStudio).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(window, null);
            var model = MNNStudioModel.Read(directory);
            var session = new MNNStudioSession(MNNStudioNativeBackend.Load(model, 1, false));
            try
            {
                Field("_available").SetValue(window, new List<MNNStudioModel> {model});
                Field("_modelIndex").SetValue(window, 0);
                Field("_taskIndex").SetValue(window, (int)MNNStudioTask.AudioProcessing);
                Field("_query").SetValue(window, "");
                Field("_session").SetValue(window, session);
                Field("_autoTitle").SetValue(window, false);
                Assert.False((bool)Invoke("CanRunTask"), "Audio workspace requires a recording.");
                Field("_audioPath").SetValue(window, Path.Combine(Root, "../../ASR/Zipformer/Audio/en-0.wav"));
                session.Completed += () => Invoke("OnCompleted");
                Assert.True((bool)Invoke("CanRunTask"));
                Invoke("RunTask");
                var deadline = DateTime.UtcNow.AddSeconds(30);
                while (session.Busy && DateTime.UtcNow < deadline) { MNNStudioJobs.Update(); yield return null; }
                Assert.False(session.Busy);
                Assert.IsNull(session.Error);
                var result = (MNNStudioResult)Field("_retrieval").GetValue(window);
                Assert.IsNotNull(result);
                Assert.AreEqual(1, result.VoiceActivity.Segments.Count);
                Assert.That(result.Text, Does.Contain("0.386–6.238 s"));
                Assert.IsEmpty((List<MNNStudioMessage>)Field("_messages").GetValue(window));
                LogAssert.NoUnexpectedReceived();
            }
            finally { UnityEngine.Object.DestroyImmediate(window); session.Dispose(); }
        }

        [Test]
        public void InvalidInputCancellationAndAsyncOwnershipRecover()
        {
            string directory = Path.Combine(Root, "Models");
            MNNSileroModel.Prepare(directory);
            var pcm = Read("two-utterances-pcm.f32");
            var vad = MNNSileroVad.Load(directory);
            try
            {
                Assert.Throws<ArgumentException>(() => vad.Detect(Array.Empty<float>(), 16000));
                Assert.Throws<ArgumentOutOfRangeException>(() => vad.Detect(pcm, 7999));
                Assert.Throws<ArgumentOutOfRangeException>(() => vad.Detect(pcm, 16000, float.NaN));
                Assert.Throws<ArgumentException>(() => vad.Detect(new[] {float.NaN}, 16000));
                using (var cancel = new CancellationTokenSource())
                {
                    cancel.CancelAfter(10);
                    Assert.Throws<OperationCanceledException>(() => vad.Detect(new float[16000 * 60], 16000, cancellationToken: cancel.Token));
                }
                var pending = vad.DetectAsync(pcm, 16000);
                Array.Clear(pcm, 0, pcm.Length);
                Assert.AreEqual(2, pending.GetAwaiter().GetResult().Segments.Count);
            }
            finally { vad.Dispose(); vad.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => vad.Detect(pcm, 16000));
        }

        [Test]
        public void RealtimeInputProcessorResetsOnGapAndHandlesCompletionAndInvalidBlocks()
        {
            string directory = Path.Combine(Root, "Models");
            MNNSileroModel.Prepare(directory);
            using (var vad = MNNRealtimeVoiceActivity.Load(directory))
            {
                var processor = new MNNRealtimeVoiceInputProcessor(new MNNPcmFormat(16000, 1), vad, 512);
                var silence = new float[512];
                Assert.Throws<ArgumentOutOfRangeException>(() => processor.Process(new float[513], 0, 513, _ => { }));
                Assert.Throws<ArgumentException>(() => processor.Process(new[] {float.NaN}, 0, 1, _ => { }));
                Assert.False(processor.IsFaulted);

                int updates = 0;
                MNNRealtimeVadUpdate last = default;
                Action<MNNRealtimeVadUpdate> collect = update => { ++updates; last = update; };
                using var modelInput = new MNNRealtimeModelInputBuffer(4);
                Action<float[], int, MNNRealtimeVadUpdate> enqueue = (frame, offset, update) =>
                    Assert.True(modelInput.TryWrite(frame, offset, update));
                processor.Process(silence, 0, silence.Length, collect, onFrame: enqueue);
                Assert.AreEqual(1, updates);
                var modelFrame = new float[MNNRealtimeModelInputBuffer.FrameSamples];
                Assert.True(modelInput.TryRead(modelFrame, 0, out var modelInfo));
                CollectionAssert.AreEqual(silence, modelFrame);
                Assert.AreEqual(last.StartSample, modelInfo.StartSample);
                Assert.AreEqual(last.SpeechProbability, modelInfo.SpeechProbability);
                Assert.False(modelInfo.Discontinuity);
                processor.Process(silence, 0, silence.Length, collect, discontinuity: true);
                Assert.AreEqual(2, updates);
                Assert.AreEqual(0, last.StartSample, "A gap resets recurrent and sample-position state before accepting new audio.");
                Assert.False(processor.Complete(collect));
                Assert.False(processor.Complete(collect));
                Assert.True(processor.IsCompleted);
                Assert.Throws<InvalidOperationException>(() => processor.Process(silence, 0, silence.Length, collect));
                processor.Reset();
                Assert.False(processor.IsCompleted);
                processor.Process(silence, 0, silence.Length, collect);
                Assert.AreEqual(3, updates);
            }
        }

        [Test]
        public void RealtimeInputProcessorFaultsClosedWhenConsumerCallbackThrows()
        {
            string directory = Path.Combine(Root, "Models");
            MNNSileroModel.Prepare(directory);
            using (var vad = MNNRealtimeVoiceActivity.Load(directory))
            {
                var processor = new MNNRealtimeVoiceInputProcessor(new MNNPcmFormat(16000, 1), vad, 512);
                var silence = new float[512];
                Assert.Throws<InvalidOperationException>(() => processor.Process(silence, 0, silence.Length,
                    _ => throw new InvalidOperationException("consumer failure")));
                Assert.True(processor.IsFaulted);
                Assert.Throws<InvalidOperationException>(() => processor.Process(silence, 0, silence.Length, _ => { }));
                processor.Reset();
                Assert.False(processor.IsFaulted);
                Assert.DoesNotThrow(() => processor.Process(silence, 0, silence.Length, _ => { }));
            }
        }

        [TestCase(8000), TestCase(44100), TestCase(48000), TestCase(95999)]
        public void ResampledInputPreservesSpeechIntervalsInSourceSeconds(int sampleRate)
        {
            string directory = Path.Combine(Root, "Models");
            MNNSileroModel.Prepare(directory);
            var source = Read("en-pcm.f32");
            var pcm = MNNSpeechDsp.Resample(source, 16000, sampleRate, CancellationToken.None);
            using (var vad = MNNSileroVad.Load(directory))
            {
                var result = vad.Detect(pcm, sampleRate);
                Assert.AreEqual(1, result.Segments.Count);
                Assert.That(result.Segments[0].StartSeconds, Is.EqualTo(.386).Within(.032));
                Assert.That(result.Segments[0].EndSeconds, Is.EqualTo(6.238).Within(.032));
                Assert.LessOrEqual(result.Segments[0].EndSeconds, result.AudioSeconds);
                Assert.AreEqual((double)pcm.Length / sampleRate, result.AudioSeconds);
            }
        }

        private static List<float> ProcessIrregularBlocks(float[] pcm, MNNRealtimeVoiceInputProcessor processor)
        {
            var probabilities = new List<float>();
            Action<MNNRealtimeVadUpdate> collect = update => probabilities.Add(update.SpeechProbability);
            int[] blockSizes = { 173, 997, 31, 640, 89, 1201, 257 };
            int sourceOffset = 0;
            int blockIndex = 0;
            while (sourceOffset < pcm.Length)
            {
                int blockCount = Math.Min(blockSizes[blockIndex++ % blockSizes.Length], pcm.Length - sourceOffset);
                processor.Process(pcm, sourceOffset, blockCount, collect);
                sourceOffset += blockCount;
            }
            processor.Complete(collect);
            return probabilities;
        }
    }
}
