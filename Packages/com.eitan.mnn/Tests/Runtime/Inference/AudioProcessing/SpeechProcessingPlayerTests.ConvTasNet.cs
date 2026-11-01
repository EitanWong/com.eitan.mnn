using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed partial class SpeechProcessingPlayerTests
    {
        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void ConvTasNetWaveformsMatchOriginalCheckpointInBuiltPlayer(MNNBackendType backend)
        {
            RequirePlayer(backend);
            using (var model = MNNConvTasNetSeparation.Load(Model("ConvTasNet-Libri2Mix-8K-MNN"),4,backend))
            {
                foreach (string name in new[]{"mixture","speech","quiet","gain2","silence","noise","tone","short","long","minimum","boundary_before","boundary_at","boundary_after","maximum","mixture"})
                {
                    var input = Read("MNN_TEST_CONVTASNET_REFERENCE",name+"-input");
                    var expected = Read("MNN_TEST_CONVTASNET_REFERENCE",name+"-sources");
                    for (int repeat = 0; repeat < 2; ++repeat)
                    {
                        var actual = model.Separate(input);
                        foreach (int speaker in new[]{0,1})
                        {
                            var samples = speaker==0?actual.Speaker0:actual.Speaker1;
                            Assert.AreEqual(expected.Length/2,samples.Length);
                            double energy=0,error=0;
                            for (int i=0;i<samples.Length;++i)
                            {
                                Assert.False(float.IsNaN(samples[i])||float.IsInfinity(samples[i]),name);
                                double value=expected[i*2+speaker];energy+=value*value;error+=Math.Pow(samples[i]-value,2);
                            }
                            double nrmse=Math.Sqrt(error/(energy==0?1:energy));
                            TestContext.WriteLine(name+"; speaker="+speaker+"; repeat="+repeat+"; waveform_nrmse="+nrmse);
                            Assert.Less(nrmse,.001,name);
                        }
                    }
                }
                Assert.AreEqual(backend,model.Backend);
            }
        }
    }
}
