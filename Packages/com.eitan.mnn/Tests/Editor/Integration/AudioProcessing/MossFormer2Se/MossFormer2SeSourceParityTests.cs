using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("MossFormer2Se")]
    public sealed class MossFormer2SeSourceParityTests
    {
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/MossFormer2-SE-48K-MNN");
        private static readonly string[] Cases = { "speech", "natural_speech", "quiet", "gain2", "silence", "noise", "tone", "short", "long", "minimum", "maximum" };
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_MOSSFORMER2SE_REFERENCE")
            ?? throw new InvalidOperationException("Set MNN_TEST_MOSSFORMER2SE_REFERENCE to the original checkpoint oracle.");
        private static Type Require(string name)
        {
            var type = typeof(MNNInterpreter).Assembly.GetType("MNN.Unity." + name);
            Assert.NotNull(type, "Missing public model implementation: " + name);
            return type;
        }
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference, name + ".f32"));
            var values = new float[bytes.Length / 4]; Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length); return values;
        }
        [Test]
        public void ModelExposesOfflineEnhancementApi() => Assert.NotNull(Require("MNNMossFormer2Se").GetMethod("Enhance"));
        [Test]
        public void StudioRecognizesVerifiedEnhancementProfile() => Assert.True(MNNStudioModel.Read(Directory).HasAudioProcessingPipeline);
        private static void Check(float[] expected, float[] actual, string name)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            double squared = actual.Select((v,i) => Math.Pow((double)v - expected[i], 2)).Sum();
            double energy = expected.Sum(v => (double)v * v);
            double error = Math.Sqrt(squared / (energy == 0 ? 1 : energy));
            TestContext.WriteLine(name + ": waveform_nrmse=" + error);
            Assert.That(error, Is.LessThan(.001), name);
        }
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal), TestCase(MNNBackendType.Auto)]
        public void WaveformMatchesOriginalDecode(MNNBackendType backend)
        {
            using (var model = MNNMossFormer2Se.Load(Directory, 4, backend))
            {
                foreach (string name in Cases.Concat(new[] { Cases[0] }).SelectMany(name => new[] { name, name }))
                    Check(Read(name + "-output"), model.Enhance(Read(name + "-input")), name);
                if (backend == MNNBackendType.Auto) Assert.AreNotEqual(MNNBackendType.Auto, model.Backend);
                else Assert.AreEqual(backend, model.Backend);
            }
        }
        [Test]
        public void FrontendMatchesOriginalDitheredFbankAndDeltas()
        {
            var config = MNNMossFormer2SeConfig.Read(Directory);
            var frontend = new MNNMossFormer2SeFeatures(config);
            foreach (string name in Cases)
            {
                var actual = frontend.Extract(Read(name + "-input"), out int frames, System.Threading.CancellationToken.None);
                var expected = Read(name + "-features"); Assert.AreEqual(expected.Length, frames * 180);
                double maximum = actual.Select((v,i) => Math.Abs((double)v-expected[i])).Max();
                TestContext.WriteLine(name + ": feature_max_abs=" + maximum);
                Assert.That(maximum, Is.LessThan(.001), name);
            }
        }
        [Test]
        public void StudioExecutesActualWaveformAndCancellation()
        {
            var model = MNNStudioModel.Read(Directory); Assert.True(model.HasAudioProcessingPipeline);
            using (var backend = MNNStudioNativeBackend.Load(model,4,false))
            {
                string path = Path.Combine(Reference,"speech.wav");
                var result = backend.Run(new MNNStudioRequest{Audio=path});
                Assert.AreEqual(48000,result.SampleRate); Check(Read("speech-output"),result.Waveform,"Studio");
                Assert.Throws<OperationCanceledException>(() => backend.Run(new MNNStudioRequest{Audio=path,Cancellation=new System.Threading.CancellationToken(true)}));
            }
        }
        [Test]
        public void BoundariesAndDisposalHaveExplicitContracts()
        {
            var model = MNNMossFormer2Se.Load(Directory,1,MNNBackendType.CPU);
            try
            {
                Assert.Throws<ArgumentException>(()=>model.Enhance(new float[1919]));
                Assert.Throws<ArgumentException>(()=>model.Enhance(new float[480001]));
                Assert.Throws<ArgumentException>(()=>model.Enhance(new[]{float.NaN}));
                Assert.Throws<OperationCanceledException>(()=>model.Enhance(new float[48000],cancellationToken:new System.Threading.CancellationToken(true)));
                Assert.Throws<NotSupportedException>(()=>MNNMossFormer2Se.Load(Directory,backendType:MNNBackendType.NN));
            }
            finally { model.Dispose(); }
            Assert.Throws<ObjectDisposedException>(()=>model.Enhance(new float[48000]));
        }
        [Test]
        public void MeasureWarmWholeCallAndAlternatingLengths()
        {
            foreach (var backend in new[] { MNNBackendType.CPU, MNNBackendType.Metal })
            foreach (int threads in backend == MNNBackendType.CPU ? new[] { 1, 2, 4 } : new[] { 1 })
            using (var model = MNNMossFormer2Se.Load(Directory,threads,backend))
            {
                var pcm = Read("speech-input"); model.Enhance(pcm);
                var timings = new double[5];
                for (int i = 0; i < timings.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew(); var output = model.Enhance(pcm); timer.Stop();
                    timings[i] = timer.Elapsed.TotalMilliseconds; Check(Read("speech-output"),output,"timing");
                }
                Array.Sort(timings);
                TestContext.WriteLine("backend="+backend+"; threads="+threads+"; warm_ms="+timings[2]);
                foreach (string name in new[]{"speech","short","long","minimum","maximum","speech"})
                {
                    var input = Read(name+"-input"); var timer = System.Diagnostics.Stopwatch.StartNew();
                    var output = model.Enhance(input); timer.Stop(); Check(Read(name+"-output"),output,name);
                    TestContext.WriteLine("alternating_backend="+backend+"; threads="+threads+"; case="+name+"; ms="+timer.Elapsed.TotalMilliseconds);
                }
            }
        }
        [Test]
        public void CompareMetalTuningAndSessionReuseAcrossAllSourceShapes()
        {
            foreach (bool recreate in new[]{false,true})
            foreach (var tuning in new[]{MNNGpuTuningMode.None,MNNGpuTuningMode.Fast,MNNGpuTuningMode.Wide})
            using (var graph = new MNNGenerationGraph(Path.Combine(Directory,MNNMossFormer2SeModel.Graph),1,
                backendType:MNNBackendType.Metal,precisionMode:MNNPrecisionMode.High,gpuTuningMode:tuning,recreateGpuSessionOnShapeChange:recreate))
            {
                bool accepted = true; double maximum = 0;
                foreach (string name in Cases.Concat(new[]{Cases[0]}).SelectMany(name=>new[]{name,name}))
                {
                    var input = Read(name+"-features"); var expected = Read(name+"-mask");
                    var actual = graph.RunOutputs(new[]{"mask"},new MNNGenerationGraph.Input("features",input,1,input.Length/180,180))[0].Values;
                    double error = Math.Sqrt(actual.Select((v,i)=>Math.Pow((double)v-expected[i],2)).Sum()/expected.Sum(v=>(double)v*v));
                    TestContext.WriteLine("tuning_case="+tuning+"; recreate="+recreate+"; case="+name+"; mask_nrmse="+error);
                    accepted &= !double.IsNaN(error) && !double.IsInfinity(error) && error < .001;
                    maximum = Math.Max(maximum,error);
                }
                var features = Read("speech-features"); var timings = new double[5];
                for (int i = 0; i < timings.Length; ++i)
                {
                    var timer = System.Diagnostics.Stopwatch.StartNew();
                    graph.RunOutputs(new[]{"mask"},new MNNGenerationGraph.Input("features",features,1,features.Length/180,180));
                    timer.Stop(); timings[i] = timer.Elapsed.TotalMilliseconds;
                }
                Array.Sort(timings);
                TestContext.WriteLine("tuning="+tuning+"; recreate="+recreate+"; accepted="+accepted+"; max_nrmse="+maximum+"; core_ms="+timings[2]);
                if (tuning == MNNGpuTuningMode.None && recreate) Assert.True(accepted,"Production policy must preserve every source shape and repeat.");
            }
        }
        [Test]
        public void SeededDitherMatchesOriginalTorchCpuNormal()
        {
            var type = Require("MNNSeededNormal");
            foreach (string name in new[] { "minimum", "speech", "maximum" })
            {
                var expected = Read(name + "-dither");
                var actual = (float[])type.GetMethod("Generate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,new object[] { expected.Length, (uint)20261030 });
                double error = actual.Select((v,i) => Math.Abs((double)v-expected[i])).Max();
                TestContext.WriteLine(name + ": dither_max_abs=" + error); Assert.Less(error,.000001);
            }
        }
    }
}
