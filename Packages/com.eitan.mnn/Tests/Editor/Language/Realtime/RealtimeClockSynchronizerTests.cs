using System.Diagnostics;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class RealtimeClockSynchronizerTests
    {
        private static readonly MNNPcmFormat CaptureFormat = new MNNPcmFormat(16000, 1);
        private static readonly MNNPcmFormat PresentationFormat = new MNNPcmFormat(48000, 2);
        private static readonly MNNPcmFormat DriftCaptureFormat = new MNNPcmFormat(48000, 1);

        [Test]
        public void MappingRequiresBothClocksAndConvertsRateAndEpochOffset()
        {
            var clocks = new MNNRealtimeClockSynchronizer(CaptureFormat, PresentationFormat);
            Assert.False(clocks.TryMapCaptureToPresentation(8000, out _));
            Assert.True(clocks.ObserveCapture(16000, Stopwatch.Frequency));
            Assert.False(clocks.TryMapCaptureToPresentation(8000, out _));
            Assert.True(clocks.ObservePresentation(96000, Stopwatch.Frequency));
            Assert.True(clocks.ObserveCapture(32000, Stopwatch.Frequency * 2));
            Assert.True(clocks.ObservePresentation(144000, Stopwatch.Frequency * 2));
            Assert.True(clocks.TryMapCaptureToPresentation(24000, out long mapped));
            Assert.AreEqual(120000, mapped);
            Assert.That(clocks.CaptureToPresentationFramesPerFrame, Is.EqualTo(3.0).Within(.000001));
        }

        [Test]
        public void RateEstimatorTracksSmallDriftAndRejectsOutliersWithoutMovingAnchor()
        {
            var clocks = new MNNRealtimeClockSynchronizer(DriftCaptureFormat, PresentationFormat);
            Assert.True(clocks.ObserveCapture(0, 0));
            Assert.True(clocks.ObservePresentation(0, 0));
            for (int second = 1; second <= 10; ++second)
            {
                long ticks = Stopwatch.Frequency * second;
                long captureFrames = 48000L * second + 5L * second;
                long presentationFrames = 48000L * second;
                Assert.True(clocks.ObserveCapture(captureFrames, ticks));
                Assert.True(clocks.ObservePresentation(presentationFrames, ticks));
            }
            Assert.That(clocks.CaptureDriftPpm, Is.InRange(100.0, 105.0));
            Assert.That(clocks.PresentationDriftPpm, Is.EqualTo(0).Within(.01));

            long previousFrame = 480000 + 50;
            long previousTicks = Stopwatch.Frequency * 10L;
            Assert.False(clocks.ObserveCapture(previousFrame + 500000, previousTicks + Stopwatch.Frequency));
            Assert.True(clocks.ObserveCapture(previousFrame + 48005, previousTicks + Stopwatch.Frequency));
            Assert.That(clocks.CaptureDriftPpm, Is.InRange(100.0, 105.0));
        }

        [Test]
        public void DuplicateRegressionAndInvalidObservationsAreRejected()
        {
            var clocks = new MNNRealtimeClockSynchronizer(CaptureFormat, PresentationFormat);
            Assert.False(clocks.ObserveCapture(-1, 0));
            Assert.False(clocks.ObserveCapture(0, -1));
            Assert.True(clocks.ObserveCapture(100, 1000));
            Assert.False(clocks.ObserveCapture(100, 2000));
            Assert.False(clocks.ObserveCapture(101, 1000));
            Assert.False(clocks.ObserveCapture(200, 1001));
            Assert.True(clocks.ObserveCapture(16100, 1000 + Stopwatch.Frequency));
            Assert.That(clocks.CaptureDriftPpm, Is.EqualTo(0).Within(.01));
        }

        [Test]
        public void IndependentResetRetainsOtherClockAndRequiresFreshObservation()
        {
            var clocks = new MNNRealtimeClockSynchronizer(CaptureFormat, PresentationFormat);
            Assert.True(clocks.ObserveCapture(0, 0));
            Assert.True(clocks.ObserveCapture(16000, Stopwatch.Frequency));
            Assert.True(clocks.ObservePresentation(0, 0));
            Assert.True(clocks.ObservePresentation(48000, Stopwatch.Frequency));
            Assert.True(clocks.TryMapCaptureToPresentation(8000, out _));

            clocks.ResetPresentation();
            Assert.True(clocks.HasCaptureClock);
            Assert.False(clocks.HasPresentationClock);
            Assert.False(clocks.TryMapCaptureToPresentation(8000, out _));
            Assert.True(clocks.ObservePresentation(90000, Stopwatch.Frequency * 2));
            Assert.True(clocks.TryMapCaptureToPresentation(8000, out _));

            clocks.ResetCapture();
            Assert.False(clocks.HasCaptureClock);
            Assert.True(clocks.HasPresentationClock);
            Assert.That(clocks.CaptureDriftPpm, Is.EqualTo(0).Within(.01));
        }

        [Test]
        public void DefaultUninitializedFormatsAreRejected()
        {
            Assert.Throws<System.ArgumentException>(() =>
                new MNNRealtimeClockSynchronizer(default, PresentationFormat));
            Assert.Throws<System.ArgumentException>(() =>
                new MNNRealtimeClockSynchronizer(CaptureFormat, default));
        }

    }
}
