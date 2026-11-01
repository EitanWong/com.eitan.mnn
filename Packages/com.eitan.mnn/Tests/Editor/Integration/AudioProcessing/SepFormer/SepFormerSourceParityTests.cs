using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("SepFormer")]
    public sealed class SepFormerSourceParityTests
    {
        private static string Directory => Path.GetFullPath("Assets/StreamingAssets/MNN/Models/SepFormer-WSJ02Mix-MNN");
        private static readonly string[] Cases = { "mixture","speech","quiet","gain2","silence","noise","tone","short","long","minimum","boundary_before","boundary_at","boundary_after","maximum" };
        private static string Reference => Environment.GetEnvironmentVariable("MNN_TEST_SEPFORMER_REFERENCE")
            ?? throw new InvalidOperationException("Set MNN_TEST_SEPFORMER_REFERENCE to the original SpeechBrain oracle.");
        private static Type Require()
        {
            var type = typeof(MNNInterpreter).Assembly.GetType("MNN.Unity.MNNSepFormerSeparation");
            Assert.NotNull(type,"Missing SepFormer public API"); return type;
        }
        private static float[] Read(string name)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Reference,name+".f32"));
            var values = new float[bytes.Length/4]; Buffer.BlockCopy(bytes,0,values,0,bytes.Length); return values;
        }
        private static void Check(float[] expected, MNNSeparatedAudio actual, string name)
        {
            foreach (int speaker in new[]{0,1})
            {
                var values = speaker==0?actual.Speaker0:actual.Speaker1;
                Assert.AreEqual(expected.Length/2,values.Length);
                double energy=0,error=0;
                for (int i=0;i<values.Length;++i)
                {
                    double value=expected[i*2+speaker]; energy+=value*value;error+=Math.Pow(values[i]-value,2);
                }
                double nrmse=Math.Sqrt(error/(energy==0?1:energy));
                TestContext.WriteLine(name+"; speaker="+speaker+"; waveform_nrmse="+nrmse);
                Assert.That(nrmse,Is.LessThan(.001),name);
            }
        }
        [TestCase(MNNBackendType.CPU),TestCase(MNNBackendType.Metal),TestCase(MNNBackendType.Auto)]
        public void WaveformsMatchOriginalSpeechBrainSeparation(MNNBackendType backend)
        {
            using(var model=MNNSepFormerSeparation.Load(Directory,4,backend))
            {
                foreach(string name in Cases.Concat(new[]{Cases[0]}).SelectMany(name=>new[]{name,name}))
                    Check(Read(name+"-sources"),model.Separate(Read(name+"-input")),name);
                if (backend == MNNBackendType.Auto) Assert.AreNotEqual(MNNBackendType.Auto,model.Backend);
                else Assert.AreEqual(backend,model.Backend);
            }
        }
        [Test]
        public void StudioRecognizesSepFormerPipeline() => Assert.True(MNNStudioModel.Read(Directory).HasAudioProcessingPipeline);
        [Test]
        public void PublicApiExposesBothSpeakerOutputs() => Assert.NotNull(Require().GetMethod("SeparateWaveFile"));
        [Test]
        public void StudioExecutesActualFirstSpeakerWaveformAndCancellation()
        {
            var model=MNNStudioModel.Read(Directory);Assert.True(model.HasAudioProcessingPipeline);
            using(var backend=MNNStudioNativeBackend.Load(model,4,false))
            {
                string path=Path.Combine(Reference,"mixture.wav");
                var result=backend.Run(new MNNStudioRequest{Audio=path});
                Assert.AreEqual(8000,result.SampleRate);
                var expected=Read("mixture-sources");
                Assert.AreEqual(expected.Length/2,result.Waveform.Length);
                double energy=0,error=0;
                for(int i=0;i<result.Waveform.Length;++i)
                {
                    double value=expected[i*2];energy+=value*value;error+=Math.Pow(result.Waveform[i]-value,2);
                }
                Assert.Less(Math.Sqrt(error/energy),.001);
                Assert.Throws<OperationCanceledException>(()=>backend.Run(new MNNStudioRequest{Audio=path,Cancellation=new System.Threading.CancellationToken(true)}));
            }
        }
        [Test]
        public void MeasureWarmWholeCallAndAlternatingLengths()
        {
            foreach (var backend in new[]{MNNBackendType.CPU,MNNBackendType.Metal})
            foreach (int threads in backend==MNNBackendType.CPU?new[]{1,2,4}:new[]{1})
            using (var model=MNNSepFormerSeparation.Load(Directory,threads,backend))
            {
                var pcm=Read("mixture-input"); model.Separate(pcm);
                var timings=new double[5];
                for(int i=0;i<timings.Length;++i)
                {
                    var timer=System.Diagnostics.Stopwatch.StartNew(); var actual=model.Separate(pcm); timer.Stop();
                    timings[i]=timer.Elapsed.TotalMilliseconds; Check(Read("mixture-sources"),actual,"timing");
                }
                Array.Sort(timings);
                TestContext.WriteLine("backend="+backend+"; threads="+threads+"; warm_ms="+timings[2]);
                foreach(string name in new[]{"mixture","short","long","minimum","maximum","mixture"})
                {
                    var input=Read(name+"-input"); var timer=System.Diagnostics.Stopwatch.StartNew();
                    var actual=model.Separate(input); timer.Stop(); Check(Read(name+"-sources"),actual,name);
                    TestContext.WriteLine("alternating_backend="+backend+"; threads="+threads+"; case="+name+"; ms="+timer.Elapsed.TotalMilliseconds);
                }
            }
        }
        [Test]
        public void CompareMetalTuningAndSessionReuseAcrossAllSourceShapes()
        {
            foreach(bool recreate in new[]{false,true})
            foreach(var tuning in new[]{MNNGpuTuningMode.None,MNNGpuTuningMode.Fast,MNNGpuTuningMode.Wide})
            using(var graph=new MNNGenerationGraph(Path.Combine(Directory,MNNSepFormerModel.Graph),1,
                backendType:MNNBackendType.Metal,precisionMode:MNNPrecisionMode.High,gpuTuningMode:tuning,recreateGpuSessionOnShapeChange:recreate))
            {
                bool accepted=true; double maximum=0;
                foreach(string name in Cases.Concat(new[]{Cases[0]}).SelectMany(name=>new[]{name,name}))
                {
                    var input=Read(name+"-input"); var expected=Read(name+"-sources");
                    var actual=graph.RunOutputs(new[]{"separated"},new MNNGenerationGraph.Input("mixture",input,1,input.Length))[0].Values;
                    Assert.AreEqual(expected.Length,actual.Length,name);
                    foreach(int speaker in new[]{0,1})
                    {
                        double energy=0,error=0;
                        for(int i=speaker;i<expected.Length;i+=2)
                        {
                            energy+=(double)expected[i]*expected[i]; error+=Math.Pow((double)actual[i]-expected[i],2);
                        }
                        double nrmse=Math.Sqrt(error/(energy==0?1:energy));
                        accepted &= !double.IsNaN(nrmse)&&!double.IsInfinity(nrmse)&&nrmse<.001;
                        maximum=Math.Max(maximum,nrmse);
                        TestContext.WriteLine("tuning_case="+tuning+"; recreate="+recreate+"; case="+name+"; speaker="+speaker+"; waveform_nrmse="+nrmse);
                    }
                }
                var pcm=Read("mixture-input"); var timings=new double[5];
                for(int i=0;i<timings.Length;++i)
                {
                    var timer=System.Diagnostics.Stopwatch.StartNew();
                    graph.RunOutputs(new[]{"separated"},new MNNGenerationGraph.Input("mixture",pcm,1,pcm.Length));
                    timer.Stop(); timings[i]=timer.Elapsed.TotalMilliseconds;
                }
                Array.Sort(timings);
                TestContext.WriteLine("tuning="+tuning+"; recreate="+recreate+"; accepted="+accepted+"; max_nrmse="+maximum+"; core_ms="+timings[2]);
                if(tuning==MNNGpuTuningMode.None&&recreate) Assert.True(accepted,"Production policy must preserve every source shape and repeat.");
            }
        }
        [Test]
        public void InputBoundariesCancellationAndDisposalHaveExplicitBehavior()
        {
            var model=MNNSepFormerSeparation.Load(Directory,1,MNNBackendType.CPU);
            try
            {
                Assert.Throws<ArgumentException>(()=>model.Separate(new float[15]));
                Assert.Throws<ArgumentException>(()=>model.Separate(new float[80001]));
                Assert.Throws<ArgumentException>(()=>model.Separate(new[]{float.NaN}));
                Assert.Throws<OperationCanceledException>(()=>model.Separate(new float[8000],cancellationToken:new System.Threading.CancellationToken(true)));
                Assert.Throws<NotSupportedException>(()=>MNNSepFormerSeparation.Load(Directory,backendType:MNNBackendType.NN));
            }
            finally {model.Dispose();}
            Assert.Throws<ObjectDisposedException>(()=>model.Separate(new float[8000]));
        }
    }
}
