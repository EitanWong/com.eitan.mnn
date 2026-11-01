using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("Integration"), Category("ResembleEnhancer")]
    public sealed class ResembleEnhancerSourceParityTests
    {
        [Test]
        public void SerializedCoefficientsKeepEveryOriginalFloat()
        {
            var config = MNNResembleEnhancerConfig.Read(ResembleEnhancerReference.Model);
            CheckBits(config.analysis_window, "denoiser_window");
            CheckBits(config.mel_window, "mel_window");
            CheckBits(config.mel_filterbank, "mel_filterbank");
            CheckBits(config.dt, "dt");
            CheckBits(new[] { config.running_mean }, "running_mean");
            CheckBits(new[] { config.running_std }, "running_std");
            for (int i = 0; i < 32; ++i)
                CheckBits(config.time_embeddings.Skip(i * 128).Take(128).ToArray(), "embedding-" + i);
        }

        [Test]
        public void ManagedFrontendsKeepAllOriginalInputConditions()
        {
            var dsp = new MNNResembleEnhancerDsp(MNNResembleEnhancerConfig.Read(ResembleEnhancerReference.Model));
            Assert.That(ResembleEnhancerReference.Names.Length, Is.EqualTo(24));
            foreach (string name in ResembleEnhancerReference.Names)
            {
                float[] padded = dsp.Prepare(ResembleEnhancerReference.Read(name + "-input"), out float scale);
                ResembleEnhancerReference.Check(name + " padded", padded,
                    ResembleEnhancerReference.Read(name + "-mel_0_input_0"), .000001);
                ResembleEnhancerReference.Check(name + " mel", dsp.Mel(padded, CancellationToken.None),
                    ResembleEnhancerReference.Read(name + "-encoded_0_input_0"), .00001);
                float[] features = dsp.DenoiserFeatures(padded, out int frames, CancellationToken.None);
                ResembleEnhancerReference.Check(name + " denoiser features", features,
                    ResembleEnhancerReference.Read(name + "-features", Path.Combine(ResembleEnhancerReference.Root, "phase-reference-v26")), .0000001);
                var condition = ResembleEnhancerReference.Read(name + "-velocity_0_input_l");
                MNNResembleEnhancerDsp.NormalizeCondition(condition, frames);
                ResembleEnhancerReference.Check(name + " normalized condition", condition,
                    ResembleEnhancerReference.Read(name + "-normalized_condition"), .000001);
            }
        }

        [TestCase(MNNBackendType.CPU), TestCase(MNNBackendType.Metal)]
        public void PublicPcmSurvivesCriticalShapeChanges(MNNBackendType backend)
        {
            using (var model = MNNResembleEnhancer.Load(ResembleEnhancerReference.Model, backendType: backend))
                foreach (string name in new[] { "hop_at", "maximum_before", "minimum", "ramp", "hop_at" })
                {
                    ResembleEnhancerReference.Check(name + " public PCM", model.Enhance(ResembleEnhancerReference.Read(name + "-input")),
                        ResembleEnhancerReference.Read(name + "-target"));
                    ResembleEnhancerReference.CheckBackends(model, backend);
                }
        }

        [Test]
        public void AutoMatchesTheQualifiedDefaultProfile()
        {
            using (var model = MNNResembleEnhancer.Load(ResembleEnhancerReference.Model))
            {
                ResembleEnhancerReference.Check("Auto minimum public PCM", model.Enhance(ResembleEnhancerReference.Read("minimum-input")),
                    ResembleEnhancerReference.Read("minimum-target"));
                ResembleEnhancerReference.CheckBackends(model,
                    MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU);
            }
        }

        [Test]
        public void InvalidInputCancellationAndDisposalStayExplicit()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNResembleEnhancer.Load("unused", 8));
            Assert.Throws<NotSupportedException>(() => MNNResembleEnhancer.Load("unused", backendType: MNNBackendType.NN));
            using (var model = MNNResembleEnhancer.Load(ResembleEnhancerReference.Model))
            {
                Assert.Throws<ArgumentException>(() => model.Enhance(null));
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[399]));
                Assert.Throws<ArgumentException>(() => model.Enhance(new float[132301]));
                Assert.Throws<ArgumentOutOfRangeException>(() => model.Enhance(new float[400], 16000));
                var values = new float[400]; values[20] = float.NaN;
                Assert.Throws<ArgumentException>(() => model.Enhance(values));
                values[20] = float.PositiveInfinity;
                Assert.Throws<ArgumentException>(() => model.Enhance(values));
                Assert.Throws<OperationCanceledException>(() => model.Enhance(new float[400], cancellationToken: new CancellationToken(true)));
                model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Enhance(new float[400]));
            }
        }

        private static void CheckBits(float[] values, string name)
        {
            var bytes = new byte[values.Length * 4];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            Assert.That(bytes, Is.EqualTo(File.ReadAllBytes(Path.Combine(ResembleEnhancerReference.Reference, name + ".f32"))), name);
        }
    }
}
