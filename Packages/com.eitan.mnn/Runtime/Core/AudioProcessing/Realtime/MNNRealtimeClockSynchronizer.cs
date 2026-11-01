using System;
using System.Diagnostics;

namespace MNN.Unity
{
    /// <summary>
    /// Maps independent capture and presentation sample clocks onto one monotonic timeline.
    /// Call from one audio-processing worker, never from an audio device callback.
    /// </summary>
    public sealed class MNNRealtimeClockSynchronizer
    {
        private const double DriftLimit = .02;
        private const double RateTimeConstantSeconds = 2.0;
        private readonly ClockTrack _capture;
        private readonly ClockTrack _presentation;

        public bool HasCaptureClock => _capture.HasObservation;
        public bool HasPresentationClock => _presentation.HasObservation;
        public double CaptureRateRatio => _capture.RateRatio;
        public double PresentationRateRatio => _presentation.RateRatio;
        public double CaptureDriftPpm => (_capture.RateRatio - 1.0) * 1000000.0;
        public double PresentationDriftPpm => (_presentation.RateRatio - 1.0) * 1000000.0;
        public double CaptureToPresentationFramesPerFrame =>
            _presentation.NominalRate * _presentation.RateRatio / (_capture.NominalRate * _capture.RateRatio);

        public MNNRealtimeClockSynchronizer(MNNPcmFormat captureFormat, MNNPcmFormat presentationFormat)
        {
            captureFormat.RequireValid();
            presentationFormat.RequireValid();
            _capture = new ClockTrack(captureFormat.SampleRate);
            _presentation = new ClockTrack(presentationFormat.SampleRate);
        }

        /// <summary>Audio-processing worker only. False rejects a duplicate, regression, or implausible clock jump.</summary>
        public bool ObserveCapture(long framePosition, long monotonicTicks) =>
            _capture.Observe(framePosition, monotonicTicks);

        /// <summary>Audio-processing worker only. False rejects a duplicate, regression, or implausible clock jump.</summary>
        public bool ObservePresentation(long framePosition, long monotonicTicks) =>
            _presentation.Observe(framePosition, monotonicTicks);

        /// <summary>Maps a capture sample position to the corresponding device presentation sample position.</summary>
        public bool TryMapCaptureToPresentation(long captureFrame, out long presentationFrame)
        {
            presentationFrame = 0;
            if (captureFrame < 0 || !HasCaptureClock || !HasPresentationClock)
                return false;

            double captureRate = _capture.NominalRate * _capture.RateRatio;
            double presentationRate = _presentation.NominalRate * _presentation.RateRatio;
            double elapsedTicks = (double)_capture.LastTicks - _presentation.LastTicks;
            double mapped = _presentation.LastFrame + elapsedTicks * presentationRate / Stopwatch.Frequency +
                ((double)captureFrame - _capture.LastFrame) * presentationRate / captureRate;
            if (double.IsNaN(mapped) || double.IsInfinity(mapped) || mapped < 0 || mapped > long.MaxValue - 1.0)
                return false;
            presentationFrame = (long)Math.Round(mapped, MidpointRounding.AwayFromZero);
            return true;
        }

        /// <summary>Reset after a capture discontinuity without discarding the independent output clock estimate.</summary>
        public void ResetCapture() => _capture.Reset();

        /// <summary>Reset after a presentation device or sample-rate change.</summary>
        public void ResetPresentation() => _presentation.Reset();

        public void Reset()
        {
            _capture.Reset();
            _presentation.Reset();
        }

        private sealed class ClockTrack
        {
            internal readonly int NominalRate;
            internal bool HasObservation;
            internal long LastFrame;
            internal long LastTicks;
            internal double RateRatio = 1.0;

            internal ClockTrack(int nominalRate) => NominalRate = nominalRate;

            internal bool Observe(long frame, long ticks)
            {
                if (frame < 0 || ticks < 0)
                    return false;
                if (!HasObservation)
                {
                    LastFrame = frame;
                    LastTicks = ticks;
                    HasObservation = true;
                    return true;
                }

                if (frame <= LastFrame || ticks <= LastTicks)
                    return false;

                long deltaFrames = frame - LastFrame;
                long deltaTicks = ticks - LastTicks;
                double measuredRatio = (double)deltaFrames * Stopwatch.Frequency / ((double)deltaTicks * NominalRate);
                if (double.IsNaN(measuredRatio) || double.IsInfinity(measuredRatio) ||
                    measuredRatio < 1.0 - DriftLimit || measuredRatio > 1.0 + DriftLimit)
                    return false;

                double elapsedSeconds = (double)deltaTicks / Stopwatch.Frequency;
                double alpha = 1.0 - Math.Exp(-elapsedSeconds / RateTimeConstantSeconds);
                RateRatio += (measuredRatio - RateRatio) * alpha;
                LastFrame = frame;
                LastTicks = ticks;
                return true;
            }

            internal void Reset()
            {
                HasObservation = false;
                LastFrame = 0;
                LastTicks = 0;
                RateRatio = 1.0;
            }
        }
    }
}
