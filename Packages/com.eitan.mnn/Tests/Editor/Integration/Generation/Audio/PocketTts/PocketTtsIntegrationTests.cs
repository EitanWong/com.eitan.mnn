using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text;
using System.Globalization;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("RepresentativeModel")]
    public class PocketTtsIntegrationTests
    {
        private static string Root => Path.Combine(Environment.GetEnvironmentVariable("MNN_TEST_LOCAL_MODEL_ROOT"), "Pocket-TTS-MNN");

        [Test]
        public void TokenizerMatchesIndependentSentencePieceCorpus()
        {
            var tokenizer = new MNNPocketText(Path.Combine(Root, "tokenizer.model"));
            var corpus = MNNModelData.Array(MNNModelData.Get(MNNModelData.Read(Path.Combine(AdditionalTtsValidation.Artifacts, "pocket-tokenizer-reference.json")), "cases"));
            foreach (var item in corpus)
            {
                string text = (string)MNNModelData.Get(item, "text");
                int[] expected = MNNModelData.Array(MNNModelData.Get(item, "ids")).Select(value => Convert.ToInt32(value)).ToArray();
                CollectionAssert.AreEqual(expected, tokenizer.Encode(text), text);
            }
        }

        [Test]
        public void CorrectedEncoderMatchesIndependentOnnxReference()
        {
            var reference = MNNReferenceAudio.FromWaveFile(Path.Combine(Root, "reference_sample.wav"));
            var pcm = MNNSpeechDsp.Resample(reference.Samples, reference.SampleRate, 24000, default);
            string graphPath = Path.Combine(Root, "mnn", "mimi_encoder_w8.mnn");
            using (var graph = new MNNExpressGraph(graphPath, 4, MNNBackendType.CPU, MNNPocketGraph.Encoder(File.ReadAllBytes(graphPath))))
            {
                var actual = graph.Run("latents", new[]{new MNNGenerationGraph.Input("audio", pcm, 1, 1, pcm.Length)});
                byte[] bytes = File.ReadAllBytes(Path.Combine(AdditionalTtsValidation.Artifacts, "pocket-reference-voice-embeddings.f32"));
                var expected = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, expected, 0, bytes.Length);
                Assert.AreEqual(expected.Length, actual.Length);
                double max = expected.Zip(actual, (a, b) => Math.Abs(a - b)).Max();
                double rmse = Math.Sqrt(expected.Zip(actual, (a, b) => Math.Pow(a - b, 2)).Average());
                Assert.Less(max, .01); Assert.Less(rmse, .002);
                TestContext.WriteLine("Pocket quantized encoder max=" + max + ", RMSE=" + rmse);
            }
        }

        [Test]
        public void ChangedExportIsRejectedBeforeNativeExecution()
        {
            string root = Path.Combine(AdditionalTtsValidation.Artifacts, "pocket-invalid-" + Guid.NewGuid().ToString("N"));
            try
            {
                foreach (var file in MNNVerifiedTtsModel.PocketFiles().Where(file => file.Path != "README.md" && file.Path != "LICENSE" && file.Path != "reference_sample.wav"))
                {
                    string path = Path.Combine(root, file.Path); Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var stream = File.Create(path)) stream.SetLength(file.Bytes);
                }
                Assert.IsTrue(MNNPocketTts.IsComplete(root));
                Assert.Throws<InvalidDataException>(() => MNNPocketTts.Load(root));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        [TestCase(MNNPrecisionMode.Normal), TestCase(MNNPrecisionMode.Low)]
        [Explicit("Real MNN-only speech, reference-voice cache reuse and independent English ASR.")]
        public void NativeSpeechAndWarmReferenceReuse(MNNPrecisionMode mainPrecision)
        {
            var reference = MNNReferenceAudio.FromWaveFile(Path.Combine(Root, "reference_sample.wav"));
            using (var model = MNNPocketTts.Load(Root, 4, mainPrecision))
            {
                foreach (int seed in new[]{42, 73})
                {
                    var audio = model.Synthesize("The capital of France is Paris.", reference, seed);
                    string path = AdditionalTtsValidation.ValidateAndSave(audio, "pocket-native-" + mainPrecision + "-" + seed + ".wav", 24000);
                    AdditionalTtsValidation.Transcribe(path, false);
                }
                var cancelled = new CancellationToken(true);
                Assert.Throws<OperationCanceledException>(() => model.Synthesize("Hello.", reference, cancellationToken: cancelled));
                Assert.Throws<ArgumentException>(() => model.Synthesize(string.Join(" ", Enumerable.Repeat("Testing", 60)), reference));
                Assert.Throws<ArgumentNullException>(() => model.Synthesize("Hello.", null));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Synthesize("Hello.", reference, maxFrames: 5));
                Assert.Throws<InvalidDataException>(() => model.Synthesize("The capital of France is Paris.", reference, maxFrames: 12));
            }
        }

        [TestCase(MNNPrecisionMode.Normal), TestCase(MNNPrecisionMode.Low)]
        [Explicit("Compare stateful 80 ms streaming decode with batch Pocket-TTS output on the pinned MNN export.")]
        public void IncrementalDecoderMatchesBatchAndHonorsCancellation(MNNPrecisionMode mainPrecision)
        {
            var reference = MNNReferenceAudio.FromWaveFile(Path.Combine(Root, "reference_sample.wav"));
            using (var model = MNNPocketTts.Load(Root, 4, mainPrecision))
            {
                const string text = "The capital of France is Paris.";
                const int seed = 42;
                var batch = model.Synthesize(text, reference, seed);
                var chunks = new List<float[]>();
                bool ended = false;
                int frames = model.SynthesizeStreaming(text, reference, (samples, offset, count, endsSegment) =>
                {
                    Assert.AreEqual(1920, count, "Each Pocket decoder step must publish 80 ms at 24 kHz.");
                    Assert.False(ended, "The final-chunk marker must be published exactly once and last.");
                    ended = endsSegment;
                    var copy = new float[count];
                    Array.Copy(samples, offset, copy, 0, count);
                    chunks.Add(copy);
                }, seed);

                Assert.AreEqual(batch.Waveform.Length, frames);
                Assert.True(ended, "The decoder must identify the final PCM block without buffering the preceding blocks.");
                Assert.AreEqual(batch.Waveform.Length, chunks.Sum(chunk => chunk.Length));
                var streamed = chunks.SelectMany(chunk => chunk).ToArray();
                Assert.Less(batch.Waveform.Zip(streamed, (expected, actual) => Math.Abs(expected - actual)).Max(), .02,
                    "Incremental decoder state must preserve the pinned batch waveform.");

                using (var cancellation = new CancellationTokenSource())
                {
                    int delivered = 0;
                    Assert.Throws<OperationCanceledException>(() => model.SynthesizeStreaming(text, reference,
                        (_, __, ___, ____) =>
                        {
                            ++delivered;
                            cancellation.Cancel();
                        }, seed, cancellationToken: cancellation.Token));
                    Assert.AreEqual(1, delivered, "Cancellation must stop before another latent step is decoded.");
                }

                var recovered = model.Synthesize(text, reference, seed);
                Assert.Less(batch.Waveform.Zip(recovered.Waveform, (expected, actual) => Math.Abs(expected - actual)).Max(), .02,
                    "Cancellation must leave reusable model graphs in a valid state.");
            }
        }

        [TestCase(MNNPrecisionMode.Normal), TestCase(MNNPrecisionMode.Low)]
        [Explicit("Real Pocket PCM through the bounded live output loop, software render receipts and interruption under backpressure.")]
        public void StreamingProductionRendersNativePcmAndInterruptsBackpressure(MNNPrecisionMode mainPrecision)
        {
            var reference = MNNReferenceAudio.FromWaveFile(Path.Combine(Root, "reference_sample.wav"));
            using (var model = MNNPocketTts.Load(Root, 4, mainPrecision))
            {
                const string spoken = "The capital of France is Paris.";
                var baseline = model.Synthesize(spoken, reference);
                var format = new MNNPcmFormat(model.SampleRate, 1);
                var output = new MNNRealtimeOutputPipeline(format, 240, blockCapacity: 3);
                var response = output.BeginResponse();
                var text = new MNNRealtimeSpeechTextChannel(output, response);
                Assert.True(text.Append(spoken));
                Assert.True(text.Complete());
                var stream = new MNNRealtimeSpeechStream(output, response);
                var actual = new List<float>();
                long renderedFrames = 0;
                using var cancel = new CancellationTokenSource();
                var task = Task.Run(() => stream.Produce(text, format, (sentence, publish, token) =>
                    model.SynthesizeStreaming(sentence, reference, publish, cancellationToken: token), cancel.Token));
                try
                {
                    var scratch = new float[240];
                    var deadline = Stopwatch.StartNew();
                    bool renderedBeforeEos = false;
                    while (!task.IsCompleted || output.QueuedFrames > 0)
                    {
                        Assert.Less(deadline.Elapsed.TotalSeconds, 30, "Live producer did not complete.");
                        if (output.QueuedFrames == 0) { Thread.Sleep(1); continue; }
                        long before = output.GetSegment(0).SubmittedFrames;
                        output.Render(scratch, 0, scratch.Length);
                        renderedFrames += scratch.Length;
                        output.DrainReceipts(256);
                        int copied = checked((int)(output.GetSegment(0).SubmittedFrames - before));
                        if (copied > 0 && !task.IsCompleted) renderedBeforeEos = true;
                        for (int i = 0; i < copied; ++i) actual.Add(scratch[i]);
                    }
                    Assert.True(task.GetAwaiter().GetResult());
                    Assert.True(renderedBeforeEos, "Real decoded PCM must be renderable before synthesis finishes.");
                    Assert.AreEqual(baseline.Waveform.Length, actual.Count);
                    Assert.Less(baseline.Waveform.Zip(actual, (a, b) => Math.Abs(a - b)).Max(), .02);
                    Assert.AreEqual(actual.Count / 1920, stream.SynthesisMeter.WindowChunkCount,
                        "The meter counts decoder blocks, excluding publication retries and 10 ms slices.");
                    output.AcknowledgePresentation(renderedFrames, false);
                    Assert.True(output.CanBeginNextResponse(response));
                }
                finally
                {
                    cancel.Cancel();
                    Assert.True(SpinWait.SpinUntil(() => task.IsCompleted, TimeSpan.FromSeconds(10)));
                }

                var interruptedOutput = new MNNRealtimeOutputPipeline(format, 240, blockCapacity: 1);
                var interruptedResponse = interruptedOutput.BeginResponse();
                var interruptedText = new MNNRealtimeSpeechTextChannel(interruptedOutput, interruptedResponse);
                Assert.True(interruptedText.Append(spoken));
                Assert.True(interruptedText.Complete());
                var interruptedStream = new MNNRealtimeSpeechStream(interruptedOutput, interruptedResponse);
                using var stop = new CancellationTokenSource();
                var interrupted = Task.Run(() => interruptedStream.Produce(interruptedText, format, (sentence, publish, token) =>
                    model.SynthesizeStreaming(sentence, reference, publish, cancellationToken: token), stop.Token));
                try
                {
                    Assert.True(SpinWait.SpinUntil(() => interruptedOutput.QueuedFrames >= 240 || interrupted.IsCompleted,
                        TimeSpan.FromSeconds(10)));
                    Assert.False(interrupted.IsCompleted, "The first 80 ms block should be waiting for the 10 ms queue.");
                    interruptedOutput.Interrupt();
                    Assert.True(SpinWait.SpinUntil(() => interrupted.IsCompleted, TimeSpan.FromSeconds(3)));
                    Assert.False(interrupted.GetAwaiter().GetResult());
                    var muted = new float[240];
                    interruptedOutput.Render(muted, 0, muted.Length);
                    Assert.That(muted, Is.All.Zero);
                }
                finally
                {
                    stop.Cancel();
                    Assert.True(SpinWait.SpinUntil(() => interrupted.IsCompleted, TimeSpan.FromSeconds(10)));
                }

                var stalledOutput = new MNNRealtimeOutputPipeline(format, 240, blockCapacity: 1);
                var stalledResponse = stalledOutput.BeginResponse();
                var stalledText = new MNNRealtimeSpeechTextChannel(stalledOutput, stalledResponse);
                Assert.True(stalledText.Append(spoken));
                Assert.True(stalledText.Complete());
                long stalledAt = 0;
                int stalledChunks = 0;
                var stalledStream = new MNNRealtimeSpeechStream(stalledOutput, stalledResponse,
                    TimeSpan.FromMilliseconds(300), null, () => stalledAt, () => stalledAt += Stopwatch.Frequency);
                Assert.Throws<TimeoutException>(() => stalledStream.Produce(stalledText, format, (sentence, publish, token) =>
                    model.SynthesizeStreaming(sentence, reference, (samples, offset, count, final) =>
                    {
                        ++stalledChunks;
                        publish(samples, offset, count, final);
                    }, cancellationToken: token)));
                Assert.AreEqual(1, stalledChunks, "A stalled consumer must stop before another latent is generated.");
                Assert.AreEqual(240, stalledOutput.QueuedFrames);
                Assert.False(stalledOutput.CanBeginNextResponse(stalledResponse));
                stalledOutput.Interrupt();
                var discarded = new float[240];
                stalledOutput.Render(discarded, 0, discarded.Length);
                Assert.That(discarded, Is.All.Zero);
                Assert.AreEqual(0, stalledOutput.QueuedFrames);

                var recovered = model.Synthesize(spoken, reference);
                Assert.AreEqual(baseline.Waveform.Length, recovered.Waveform.Length);
                Assert.Less(baseline.Waveform.Zip(recovered.Waveform, (a, b) => Math.Abs(a - b)).Max(), .02);
            }
        }

        [TestCase(MNNPrecisionMode.Normal), TestCase(MNNPrecisionMode.Low)]
        [Explicit("Verify exact final-frame bounds, callback failures, reentrancy rejection and subsequent model reuse.")]
        public void FinalFrameLimitAndCallbackFailurePreserveReusableState(MNNPrecisionMode mainPrecision)
        {
            var reference = MNNReferenceAudio.FromWaveFile(Path.Combine(Root, "reference_sample.wav"));
            using (var model = MNNPocketTts.Load(Root, 4, mainPrecision))
            {
                const string text = "The capital of France is Paris.";
                var baseline = model.Synthesize(text, reference);
                int frameLimit = baseline.Waveform.Length / 1920;
                Assert.That(frameLimit, Is.InRange(13, 374), "The pinned sentence must exercise the public frame-limit boundary.");
                int delivered = 0, finalMarkers = 0;
                double maxDifference = 0;
                int count = model.SynthesizeStreaming(text, reference, (samples, offset, length, endsSegment) =>
                {
                    ++delivered;
                    if (endsSegment) ++finalMarkers;
                    for (int i = 0; i < length; ++i)
                        maxDifference = Math.Max(maxDifference, Math.Abs(samples[offset + i] - baseline.Waveform[(delivered - 1) * 1920 + i]));
                }, maxFrames: frameLimit);
                Assert.AreEqual(baseline.Waveform.Length, count);
                Assert.AreEqual(frameLimit, delivered);
                Assert.AreEqual(1, finalMarkers, "A final PCM frame within the limit must complete successfully.");
                Assert.Less(maxDifference, .02, "The exact frame limit must preserve the batch waveform.");

                finalMarkers = 0;
                Assert.Throws<InvalidDataException>(() => model.SynthesizeStreaming(text, reference,
                    (_, __, ___, final) => { if (final) ++finalMarkers; }, maxFrames: frameLimit - 1));
                Assert.Zero(finalMarkers, "An incomplete stream must not publish a final marker.");

                var callbackError = new InvalidOperationException("Deliberate PCM consumer failure.");
                delivered = 0;
                var actualError = Assert.Throws<InvalidOperationException>(() => model.SynthesizeStreaming(text, reference,
                    (_, __, ___, ____) =>
                    {
                        ++delivered;
                        Assert.Throws<InvalidOperationException>(() => model.Synthesize(text, reference));
                        Assert.Throws<InvalidOperationException>(() => model.Dispose());
                        throw callbackError;
                    }));
                Assert.AreSame(callbackError, actualError, "Do not mask a consumer exception.");
                Assert.AreEqual(1, delivered, "Failed consumers must not receive more PCM.");

                var recovered = model.Synthesize(text, reference, maxFrames: frameLimit);
                Assert.AreEqual(baseline.Waveform.Length, recovered.Waveform.Length);
                Assert.Less(baseline.Waveform.Zip(recovered.Waveform, (expected, actual) => Math.Abs(expected - actual)).Max(), .02,
                    "Failed or incomplete requests must leave graphs reusable with the same reference and seed.");
            }
        }

        [TestCase(MNNPrecisionMode.Normal), TestCase(MNNPrecisionMode.Low)]
        [Explicit("Compare compact KV cache with the pinned 1000-row cache, including the last writable row.")]
        public void CompactCachePreservesMainOutputsAndPopulatedState(MNNPrecisionMode precision)
        {
            string path = Path.Combine(Root, "mnn", "flow_lm_main_w8.mnn");
            var cacheNames = Enumerable.Range(0, 6).Select(layer => "out_state_" + layer * 3).ToArray();
            string[] outputs = new[]{"conditioning", "eos_logit"}.Concat(cacheNames).ToArray();
            using (var priming = new MNNExpressGraph(path, 4, MNNBackendType.CPU,
                MNNPocketGraph.Main(File.ReadAllBytes(path), cacheNames, true)))
            using (var main = new MNNExpressGraph(path, 4, MNNBackendType.CPU,
                MNNPocketGraph.Main(File.ReadAllBytes(path), outputs, false), precision))
            {
                const int capacity = 8, primedSteps = 3;
                var states = new[]{CreateCache(1000), CreateCache(capacity)};
                var random = new Random(42);
                var embeddings = Enumerable.Range(0, primedSteps * 1024).Select(_ => (float)(random.NextDouble() - .5)).ToArray();
                for (int run = 0; run < states.Length; ++run)
                    Apply(states[run], priming.RunOutputs(cacheNames, states[run].Concat(new[]{
                        new MNNGenerationGraph.Input("text_embeddings", embeddings, 1, primedSteps, 1024)}).ToArray()), 0, primedSteps);
                CompareCaches(states[0], states[1], primedSteps);

                for (int step = primedSteps + 1; step <= capacity; ++step)
                {
                    float[] sequence = step == primedSteps + 1 ? Enumerable.Repeat(float.NaN, 32).ToArray()
                        : Enumerable.Range(0, 32).Select(_ => (float)(random.NextDouble() - .5)).ToArray();
                    var results = new MNNGenerationGraph.Output[2][];
                    for (int run = 0; run < states.Length; ++run)
                    {
                        results[run] = main.RunOutputs(outputs, states[run].Concat(new[]{
                            new MNNGenerationGraph.Input("sequence", sequence, 1, 1, 32)}).ToArray());
                        Apply(states[run], results[run], 2, step);
                    }
                    for (int output = 0; output < 2; ++output)
                    {
                        Assert.That(results[0][output].Values, Has.All.Matches<float>(value => !float.IsNaN(value) && !float.IsInfinity(value)));
                        Assert.That(results[1][output].Values, Has.All.Matches<float>(value => !float.IsNaN(value) && !float.IsInfinity(value)));
                        CollectionAssert.AreEqual(results[0][output].Shape, results[1][output].Shape);
                        Assert.That(results[0][output].Values.Zip(results[1][output].Values, (a, b) => Math.Abs(a - b)).Max(),
                            Is.LessThan(1e-5), "Main output " + outputs[output] + " at step " + step);
                    }
                    CompareCaches(states[0], states[1], step);
                }
            }

            List<MNNGenerationGraph.Input> CreateCache(int rows)
            {
                var inputs = new List<MNNGenerationGraph.Input>();
                for (int layer = 0; layer < 6; ++layer)
                {
                    inputs.Add(new MNNGenerationGraph.Input("state_" + layer * 3,
                        Enumerable.Repeat(float.NaN, 2 * rows * 1024).ToArray(), 2, 1, rows, 16, 64));
                    // The pruned graph has no consumers of the empty state_(3*layer+1).
                    inputs.Add(new MNNGenerationGraph.Input("state_" + (layer * 3 + 2), new[]{0}, 1));
                }
                return inputs;
            }

            void Apply(List<MNNGenerationGraph.Input> state, MNNGenerationGraph.Output[] result, int offset, int step)
            {
                for (int layer = 0; layer < 6; ++layer)
                {
                    CollectionAssert.AreEqual(state[layer * 2].Shape, result[offset + layer].Shape);
                    state[layer * 2] = result[offset + layer].AsInput("state_" + layer * 3);
                    state[layer * 2 + 1] = new MNNGenerationGraph.Input("state_" + (layer * 3 + 2), new[]{step}, 1);
                }
            }

            void CompareCaches(List<MNNGenerationGraph.Input> expected, List<MNNGenerationGraph.Input> actual, int steps)
            {
                for (int layer = 0; layer < 6; ++layer)
                for (int half = 0; half < 2; ++half)
                for (int i = 0; i < steps * 1024; ++i)
                {
                    float a = ((float[])expected[layer * 2].Values)[half * expected[layer * 2].Shape[2] * 1024 + i];
                    float b = ((float[])actual[layer * 2].Values)[half * actual[layer * 2].Shape[2] * 1024 + i];
                    if (float.IsNaN(a) || float.IsInfinity(a) || float.IsNaN(b) || float.IsInfinity(b) || Math.Abs(a - b) >= 1e-5)
                        Assert.Fail("Cache mismatch at layer=" + layer + ", half=" + half + ", index=" + i + ", steps=" + steps);
                }
            }
        }

        [Test, Explicit("Measure CPU candidates, repeated stage reuse and actual speech content.")]
        public void MeasureCpuThreadsAndWarmReferenceCache()
        {
            var reference = MNNReferenceAudio.FromWaveFile(Path.Combine(Root, "reference_sample.wav"));
            var report = new StringBuilder("["); float[] baseline = null;
            foreach (int threads in new[]{1, 2, 4, 8})
            using (var model = MNNPocketTts.Load(Root, threads))
            {
                var watch = Stopwatch.StartNew();
                var audio = model.Synthesize("The capital of France is Paris.", reference);
                double cold = watch.Elapsed.TotalSeconds;
                var timings = new double[3];
                for (int run = 0; run < timings.Length; ++run)
                {
                    watch.Restart(); audio = model.Synthesize("The capital of France is Paris.", reference);
                    timings[run] = watch.Elapsed.TotalSeconds;
                }
                if (baseline == null) baseline = (float[])audio.Waveform.Clone();
                Assert.AreEqual(baseline.Length, audio.Waveform.Length);
                Assert.Less(baseline.Zip(audio.Waveform, (a, b) => Math.Abs(a - b)).Max(), .02);
                string path = AdditionalTtsValidation.ValidateAndSave(audio, "pocket-cpu-" + threads + ".wav", 24000);
                AdditionalTtsValidation.Transcribe(path, false);
                double median = timings.OrderBy(value => value).ElementAt(1), seconds = (double)audio.Waveform.Length / audio.SampleRate;
                if (report.Length > 1) report.Append(',');
                report.AppendFormat(CultureInfo.InvariantCulture,
                    "{{\"threads\":{0},\"coldSeconds\":{1:R},\"medianSeconds\":{2:R},\"audioSeconds\":{3:R},\"rtf\":{4:R}",
                    threads, cold, median, seconds, median / seconds);
                report.Append('}');
                File.WriteAllText(Path.Combine(AdditionalTtsValidation.Artifacts, "pocket-performance.json"), report + "]");
                TestContext.WriteLine("Pocket CPU" + threads + ": cold=" + cold + ", warm=" + median + ", RTF=" + median / seconds);
            }
        }
    }
}
