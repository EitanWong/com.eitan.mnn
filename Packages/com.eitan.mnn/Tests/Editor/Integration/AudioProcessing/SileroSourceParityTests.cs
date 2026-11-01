using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class SileroSourceParityTests
    {
        private static string ModelDirectory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/Silero-v5.1-MNN");
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Compares current deployment against frozen official v5.1 floating ONNX across real recordings and physical conditions.")]
        public void PosteriorsMatchOfficialFloatingSource(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore("Backend unavailable.");
            using (var model = MNNSileroVad.Load(ModelDirectory, 1, backend))
            foreach (string name in new[]{"en", "zh-en", "en-padded", "zh-en-padded", "two-utterances", "silence", "noise", "tone", "quiet", "short", "en"})
            {
                var expected = Read(name, "probabilities");
                var actual = model.Detect(Read(name,"input"),16000).Probabilities;
                Assert.AreEqual(expected.Length,actual.Count);
                double maximum=expected.Select((value,i)=>Math.Abs(value-actual[i])).Max();
                TestContext.WriteLine(backend + " " + name + " max_abs=" + maximum);
                Assert.That(maximum,Is.LessThan(.0002));
            }
        }

        [Test, Explicit("Verifies real-time frame scheduling/state reset against the official source recording.")]
        public void RealtimeFramesMatchRecordingAndReset()
        {
            var pcm=Read("en","input");var expected=Read("en","probabilities");
            var frame=new float[512];
            using (var model=MNNRealtimeVoiceActivity.Load(ModelDirectory))
            for (int repeat=0;repeat<2;++repeat)
            {
                model.Reset();
                for (int i=0;i<expected.Length;++i)
                {
                    Array.Clear(frame,0,frame.Length);
                    Array.Copy(pcm,i*512,frame,0,Math.Min(512,pcm.Length-i*512));
                    var actual=model.ProcessFrame(frame,0);
                    Assert.That(Math.Abs(actual.SpeechProbability-expected[i]),Is.LessThan(.0002));
                }
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Prepared fixed-frame inference must avoid managed garbage on the audio worker.")]
        public void PreparedRealtimeFramesAllocateNoManagedBytes(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore("Backend unavailable.");
            var frame=new float[512];
            using (var model=MNNRealtimeVoiceActivity.Load(ModelDirectory, backendType:backend))
            {
                for (int i=0;i<16;++i) model.ProcessFrame(frame,0);
                long begin=GC.GetAllocatedBytesForCurrentThread();
                for (int i=0;i<32;++i) model.ProcessFrame(frame,0);
                Assert.AreEqual(0,GC.GetAllocatedBytesForCurrentThread()-begin);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Verifies all recurrent cache values against the original source over repeated recordings.")]
        public void RecurrentStatesMatchOfficialSource(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore("Backend unavailable.");
            using (var runner=new MNNSileroFrameRunner(ModelDirectory,1,backend))
            foreach (string name in new[]{"en", "zh-en", "quiet", "noise", "tone", "silence", "short", "en"})
            {
                var pcm=Read(name,"input");var expected=Read(name,"states");
                var packed=new float[832];var output=new float[257];double maximum=0;
                for (int frame=0;frame*512<pcm.Length;++frame)
                {
                    Array.Copy(packed,512,packed,0,64);Array.Clear(packed,64,512);
                    Array.Copy(pcm,frame*512,packed,64,Math.Min(512,pcm.Length-frame*512));
                    runner.Run(packed,output);
                    for (int i=0;i<256;++i) maximum=Math.Max(maximum,Math.Abs(output[i+1]-expected[frame*256+i]));
                    Array.Copy(output,1,packed,576,256);
                }
                TestContext.WriteLine(backend+" "+name+" state_max_abs="+maximum);
                Assert.That(maximum,Is.LessThan(.003));
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        [Explicit("Measures end-to-end warm VAD while checking every measured posterior vector.")]
        public void MeasuresVerifiedWarmLatency(MNNBackendType backend)
        {
            if (!MNNAcceleration.IsBackendAvailable(backend)) Assert.Ignore("Backend unavailable.");
            using (var model=MNNSileroVad.Load(ModelDirectory,1,backend))
            {
                var pcm=Read("en","input");var expected=Read("en","probabilities");
                model.Detect(pcm,16000);var timings=new double[5];
                for (int repeat=0;repeat<timings.Length;++repeat)
                {
                    var timer=System.Diagnostics.Stopwatch.StartNew();var actual=model.Detect(pcm,16000).Probabilities;timer.Stop();
                    Assert.That(expected.Select((value,i)=>Math.Abs(value-actual[i])).Max(),Is.LessThan(.0002));
                    timings[repeat]=timer.Elapsed.TotalMilliseconds;
                }
                Array.Sort(timings);TestContext.WriteLine(backend+" median_ms="+timings[2]);
            }
        }

        [Test, Explicit("Requires the official deployment for Studio, cancellation recovery and native resource lifetime checks.")]
        public void StudioAndCancellationUseOfficialProfile()
        {
            var input=Read("en","input");
            using (var model=MNNSileroVad.Load(ModelDirectory))
            {
                Assert.Throws<OperationCanceledException>(()=>model.Detect(input,16000,cancellationToken:new CancellationToken(true)));
                Assert.That(Math.Abs(model.Detect(input,16000).Probabilities[0]-Read("en","probabilities")[0]),Is.LessThan(.0002));
                model.Dispose();Assert.Throws<ObjectDisposedException>(()=>model.Detect(input,16000));
            }
            Assert.Throws<NotSupportedException>(()=>MNNSileroVad.Load(ModelDirectory,backendType:MNNBackendType.NN));
            var profile=MNNStudioModel.Read(ModelDirectory);Assert.True(profile.HasAudioProcessingPipeline);
            string wav=Path.GetFullPath("TestArtifacts~/silero-official-studio.wav");File.WriteAllBytes(wav,MNNWaveAudio.EncodePcm16(input,16000));
            try
            {
                using (var studio=MNNStudioNativeBackend.Load(profile,1,false))
                {
                    var result=studio.Run(new MNNStudioRequest{Audio=wav});
                    Assert.That(result.BackendDescription,Does.Contain("Silero"));
                    var expected=Read("en","probabilities");Assert.That(expected.Select((p,i)=>Math.Abs(p-result.VoiceActivity.Probabilities[i])).Max(),Is.LessThan(.002));
                }
            }
            finally {File.Delete(wav);}
        }

        private static float[] Read(string name,string part)
        {
            string directory=Environment.GetEnvironmentVariable("MNN_TEST_SILERO_REFERENCE");
            if (string.IsNullOrWhiteSpace(directory)) Assert.Ignore("Set MNN_TEST_SILERO_REFERENCE; tests never download models.");
            var bytes=File.ReadAllBytes(Path.Combine(directory,name+"-"+part+".f32"));
            var result=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,result,0,bytes.Length);return result;
        }
    }
}
