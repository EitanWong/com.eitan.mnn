using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class AcousticUtilityTests
    {
        [Test]
        public void SourceContributionRecoversIndependentGainsAndRejectsAmbiguity()
        {
            var mixture = new[] { 2f, 1f, -2f, -1f };
            var first = new[] { .1f, 0, -.1f, 0 };
            var second = new[] { 0f, 8f, 0, -8f };
            Assert.True(MNNAudioMixtureContribution.TryEstimate(mixture, first, second, out float a, out float b));
            Assert.That(a, Is.EqualTo(.8f).Within(1e-5));
            Assert.That(b, Is.EqualTo(.2f).Within(1e-5));
            Assert.False(MNNAudioMixtureContribution.TryEstimate(mixture, first, first, out a, out b));
            Assert.Zero(a);
            Assert.Zero(b);
            first[0] = float.NaN;
            Assert.Throws<ArgumentException>(() => MNNAudioMixtureContribution.TryEstimate(mixture, first, second, out _, out _));
        }

        [TestCase(false), TestCase(true)]
        public void SourceContributionAcceptsOneSourceAndDeviceDc(bool swap)
        {
            var speech = new[] { .2f, .1f, -.2f, -.1f };
            var dc = new[] { .05f, .05f, .05f, .05f };
            Assert.True(MNNAudioMixtureContribution.TryEstimate(speech, swap ? dc : speech,
                swap ? speech : dc, out float a, out float b));
            Assert.AreEqual(swap ? 0 : 1, a);
            Assert.AreEqual(swap ? 1 : 0, b);
            Assert.False(MNNAudioMixtureContribution.TryEstimate(dc, speech, dc, out _, out _));
        }

        [Test]
        public void SpeakerContinuityFollowsIdentityAndExpires()
        {
            var selector = new MNNAutomaticSpeakerSelector(.08f, .06f);
            Assert.AreEqual(-1, selector.Select(new[] { .72f, .73f }, null, 0));
            Assert.AreEqual(0, selector.Select(new[] { .9f, .6f }, null, 0));
            Assert.AreEqual(1, selector.Select(new[] { .78f, .76f }, new[] { .1f, .9f }, 1));
            Assert.AreEqual(0, selector.Select(new[] { .85f, .65f }, new[] { .1f, .99f }, 20));
            selector.Reset();
            Assert.AreEqual(-1, selector.Select(new[] { .78f, .76f }, new[] { .1f, .9f }, 21));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNAutomaticSpeakerSelector(float.NaN, .06f));
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.Select(new[] { .9f }, null, double.NaN));
        }

        [Test]
        public void CalibrationRequiresFreshStableFramesAndResetsOnRouteChange()
        {
            var calibration = new MNNAecDelayCalibration();
            for (int i = 0; i < 30; ++i) Assert.False(calibration.Add(80, 1, 0, i * .05, out _));
            calibration.Reset();
            for (int i = 0; i < 11; ++i) Assert.False(calibration.Add(80 + i % 3, i, 0, i * .05, out _));
            Assert.True(calibration.Add(81, 11, 0, .56, out int measured));
            Assert.AreEqual(81, measured);
            Assert.False(calibration.Add(81, 12, 1, .61, out _));
            Assert.False(calibration.Add(140, 13, 1, .66, out _));
            Assert.False(calibration.Add(-1, 14, 1, .71, out _));
        }

        [TestCase(16000, 1), TestCase(48000, 2)]
        public void FadeIsContinuousAndANewResponseRestoresGain(int sampleRate, int channels)
        {
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(sampleRate, 1), sampleRate / 100);
            var response = output.BeginResponse();
            var fade = new MNNSpeechFade();
            Task finished = fade.FadeOutAsync(response);
            var pcm = new float[127 * channels];
            float previous = 1;
            for (int rendered = 0; rendered < sampleRate * MNNSpeechFade.DurationMilliseconds / 1000 + 256; rendered += 127)
            {
                Array.Fill(pcm, 1f);
                fade.Process(pcm, pcm.Length, channels, sampleRate, response);
                for (int i = 0; i < pcm.Length; i += channels)
                {
                    Assert.That(pcm[i], Is.InRange(0f, previous));
                    Assert.Less(previous - pcm[i], .005f);
                    if (channels == 2) Assert.AreEqual(pcm[i], pcm[i + 1]);
                    previous = pcm[i];
                }
            }
            Assert.Zero(previous);
            Assert.True(finished.Wait(1000));
            output.Interrupt();
            var fresh = new[] { .4f, -.2f };
            fade.Process(fresh, fresh.Length, channels, sampleRate, output.BeginResponse());
            CollectionAssert.AreEqual(new[] { .4f, -.2f }, fresh);
        }

        [Test]
        public void FadeRejectsInvalidFramesBeforeWritingSamples()
        {
            var fade = new MNNSpeechFade();
            var pcm = new[] { .4f, -.2f };
            Assert.Throws<ArgumentOutOfRangeException>(() => fade.Process(pcm, 2, 0, 48000, default));
            Assert.Throws<ArgumentException>(() => fade.Process(pcm, 1, 2, 48000, default));
            CollectionAssert.AreEqual(new[] { .4f, -.2f }, pcm);
        }

        [Test]
        public void PlaybackSnapshotsRequireTheCurrentResponseAndPreserveSignedPeaks()
        {
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 160);
            var response = output.BeginResponse();
            var waveform = new MNNPlaybackWaveform();
            var pcm = new[] { .5f, -.75f };
            var points = new float[MNNPlaybackWaveform.PointCount];
            waveform.Publish(pcm, pcm.Length, 1, response, 16000);
            Assert.True(waveform.TryRead(points, response, out float rms, out var features));
            Assert.That(rms, Is.EqualTo(Math.Sqrt((.25 + .5625) / 2)).Within(1e-6));
            Assert.Greater(features.Peak, 0);
            Assert.AreEqual(.5f, points[0]);
            Assert.AreEqual(-.75f, points[63]);
            output.Interrupt();
            Assert.False(waveform.TryRead(points, output.BeginResponse(), out rms));
            Assert.Zero(rms);
            Assert.That(points, Is.All.Zero);
        }

        [Test]
        public void UtilityProcessingAllocatesNothingAfterWarmup()
        {
            long counter = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(new byte[64]);
            if (GC.GetAllocatedBytesForCurrentThread() <= counter)
                Assert.Ignore("This runtime does not expose a working per-thread allocation counter.");
            var calibration = new MNNAecDelayCalibration();
            var fade = new MNNSpeechFade();
            var selector = new MNNAutomaticSpeakerSelector(.08f, .06f);
            var waveform = new MNNPlaybackWaveform();
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 160);
            var response = output.BeginResponse();
            var points = new float[MNNPlaybackWaveform.PointCount];
            var mixture = new[] { 2f, 1f, -2f, -1f };
            var first = new[] { .1f, 0, -.1f, 0 };
            var second = new[] { 0f, 8f, 0, -8f };
            var contributions = new[] { .9f, .1f };
            for (int i = 0; i < 64; ++i) calibration.Add(80, i, 0, i * .05, out _);
            MNNAudioMixtureContribution.TryEstimate(mixture, first, second, out _, out _);
            selector.Select(contributions, null, 0);
            fade.Process(first, 4, 1, 16000, default);
            waveform.Publish(first, 4, 1, response, 16000);
            waveform.TryRead(points, response, out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 64; i < 128; ++i)
            {
                calibration.Add(80, i, 0, i * .05, out _);
                MNNAudioMixtureContribution.TryEstimate(mixture, first, second, out _, out _);
                selector.Select(contributions, null, i * .05);
                fade.Process(first, 4, 1, 16000, default);
                waveform.Publish(first, 4, 1, response, 16000);
                waveform.TryRead(points, response, out _);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Zero(allocated, "Calibration, selection, mixture measurement, gain and waveform processing must reuse buffers.");
        }
    }
}
