using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class MNNAudioAnalysisTests
    {
        private static MNNAudioFeatures Tone(MNNAudioAnalysis analysis, int rate, float hz, float amplitude = .3f, int channels = 1)
        {
            var pcm = new float[512 * channels];
            MNNAudioFeatures result = default;
            for (int block = 0; block < 40; ++block)
            {
                for (int i = 0; i < 512; ++i)
                for (int channel = 0; channel < channels; ++channel)
                    pcm[i * channels + channel] = amplitude * (float)Math.Sin(2 * Math.PI * hz * (block * 512 + i) / rate);
                result = analysis.Analyze(pcm, pcm.Length, channels, rate);
            }
            return result;
        }

        [TestCase(16000, 1)]
        [TestCase(48000, 2)]
        public void MeasuredFeaturesDescribePcmWithoutAnInferenceModel(int rate, int channels)
        {
            var low = Tone(new MNNAudioAnalysis(), rate, 140, channels: channels);
            var high = Tone(new MNNAudioAnalysis(), rate, 300, channels: channels);
            var bright = Tone(new MNNAudioAnalysis(), rate, 3000, channels: channels);
            Assert.That(low.Rms, Is.EqualTo(.3 / Math.Sqrt(2)).Within(.005));
            Assert.That(low.Peak, Is.EqualTo(.3).Within(.001));
            Assert.That(low.PitchHz, Is.EqualTo(140).Within(12));
            Assert.That(high.PitchHz, Is.EqualTo(300).Within(18));
            Assert.Greater(low.PitchConfidence, .65f);
            Assert.Greater(low.LowBand, high.LowBand);
            Assert.Greater(bright.HighBand, low.HighBand + .3f);
            Assert.Greater(low.GetEnvelope(), 0);
        }

        [Test]
        public void OffsetBlocksPreserveStreamingStateAndInputSamples()
        {
            var whole = new float[16000];
            for (int i = 0; i < whole.Length; ++i) whole[i] = .2f * (float)Math.Sin(2 * Math.PI * 180 * i / 16000);
            var expected = new MNNAudioAnalysis().Analyze(whole, whole.Length, 1, 16000);
            var analyzer = new MNNAudioAnalysis();
            MNNAudioFeatures actual = default;
            for (int offset = 0; offset < whole.Length; offset += 400)
                actual = analyzer.Analyze(whole, offset, 400, 1, 16000);
            Assert.That(actual.PitchHz, Is.EqualTo(expected.PitchHz).Within(.1f));
            Assert.That(actual.PitchConfidence, Is.EqualTo(expected.PitchConfidence).Within(.001f));
            for (int i = 0; i < whole.Length; ++i)
                Assert.AreEqual(.2f * (float)Math.Sin(2 * Math.PI * 180 * i / 16000), whole[i]);
        }

        [Test]
        public void ResetAndFormatChangesStartANewPitchWindow()
        {
            var analysis = new MNNAudioAnalysis();
            Assert.Greater(Tone(analysis, 16000, 140).PitchConfidence, .65f);
            analysis.Clear();
            var block = new float[128];
            for (int i = 0; i < block.Length; ++i) block[i] = .2f;
            Assert.Zero(analysis.Analyze(block, block.Length, 1, 16000).PitchConfidence);
            Tone(analysis, 16000, 140);
            Assert.Zero(analysis.Analyze(block, block.Length, 1, 48000).PitchConfidence);
            Tone(analysis, 48000, 140);
            Assert.Zero(analysis.Analyze(block, block.Length, 2, 48000).PitchConfidence);
        }

        [Test]
        public void SilenceInvalidSamplesAndIndependentStreamsStayBounded()
        {
            var a = new MNNAudioAnalysis();
            var b = new MNNAudioAnalysis();
            Tone(a, 16000, 140);
            var silent = b.Analyze(new float[512], 512, 1, 16000);
            Assert.Zero(silent.Rms); Assert.Zero(silent.PitchHz); Assert.Zero(silent.GetEnvelope());
            var invalid = a.Analyze(new[] { float.NaN, float.PositiveInfinity, -3f, 3f }, 4, 1, 16000);
            Assert.That(invalid.Rms, Is.InRange(0f, 1f));
            Assert.That(invalid.Peak, Is.InRange(0f, 1f));
            Assert.Throws<ArgumentException>(() => a.Analyze(new float[3], 3, 2, 16000));
            Assert.Throws<ArgumentException>(() => a.Analyze(new float[4], -1, 4, 1, 16000));
            Assert.Throws<ArgumentException>(() => a.Analyze(new float[4], 4, 1, 0));
        }

        [Test]
        public void WarmAnalysisAndEnvelopeAllocateNoPerBlockGarbage()
        {
            var analysis = new MNNAudioAnalysis();
            var envelope = new MNNAudioEnvelope();
            var block = new float[512];
            for (int i = 0; i < block.Length; ++i) block[i] = .2f * (float)Math.Sin(2 * Math.PI * 140 * i / 16000);
            for (int i = 0; i < 20; ++i) envelope.Update(analysis.Analyze(block, 512, 1, 16000).GetEnvelope(), .032f);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; ++i) envelope.Update(analysis.Analyze(block, 512, 1, 16000).GetEnvelope(), .032f);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Zero(allocated); Assert.Greater(envelope.Value, .1f);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void EnvelopeHasFrameIndependentAttackReleaseAndCanReset(int fps)
        {
            var envelope = new MNNAudioEnvelope(.28f, .65f);
            for (int i = 0; i < fps; ++i) envelope.Update(.8f, 1f / fps);
            Assert.That(envelope.Value, Is.EqualTo(.8f).Within(.001f));
            for (int i = 0; i < fps; ++i) envelope.Update(0, 1f / fps);
            Assert.Less(envelope.Value, .001f);
            envelope.Reset(); Assert.Zero(envelope.Value);
            Assert.Throws<ArgumentOutOfRangeException>(() => envelope.Update(float.NaN, .02f));
            Assert.Throws<ArgumentOutOfRangeException>(() => envelope.Update(.5f, -.01f));
        }

        [Test]
        public void EnvelopeMappingRetainsSpeechHeadroomAndSuppressesSilence()
        {
            Assert.Zero(new MNNAudioFeatures(.001f, 1).GetEnvelope());
            float quiet = new MNNAudioFeatures(.02f, .04f).GetEnvelope();
            float loud = new MNNAudioFeatures(.1f, .2f).GetEnvelope();
            Assert.Greater(quiet, 0); Assert.Greater(loud, quiet); Assert.Less(loud, 1);
            var features = new MNNAudioFeatures(.1f, .3f, .4f, .2f, 180, .9f, .5f).WithGain(.5f);
            Assert.AreEqual(.05f, features.Rms); Assert.AreEqual(180, features.PitchHz);
            Assert.Zero(features.WithGain(0).Onset);
        }
    }
}
