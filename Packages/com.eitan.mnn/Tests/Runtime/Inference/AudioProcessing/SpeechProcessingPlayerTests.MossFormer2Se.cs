using System;
using System.Linq;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void MossFormer2SeWaveformsMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNMossFormer2Se.Load(Model("MossFormer2-SE-48K-MNN"),4,backend))
            {
                foreach (string name in new[]{"speech","natural_speech","quiet","gain2","silence","noise","tone","short","long","minimum","maximum","speech"})
                {
                    var input = Read("MNN_TEST_MOSSFORMER2SE_REFERENCE",name+"-input");
                    var expected = Read("MNN_TEST_MOSSFORMER2SE_REFERENCE",name+"-output");
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        var actual = model.Enhance(input);
                        Assert.AreEqual(expected.Length,actual.Length);
                        Assert.True(actual.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v)),name);
                        double energy = expected.Sum(v=>(double)v*v);
                        double error = Math.Sqrt(actual.Select((v,i)=>Math.Pow((double)v-expected[i],2)).Sum()/(energy==0?1:energy));
                        TestContext.WriteLine(name+"; repeat="+repeat+"; waveform_nrmse="+error);
                        Assert.Less(error,.001,name);
                    }
                }
                Assert.AreEqual(backend,model.Backend);
            }
        }
    }
}
