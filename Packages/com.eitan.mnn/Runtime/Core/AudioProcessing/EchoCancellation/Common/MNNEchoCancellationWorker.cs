using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>One worker owns DTLN states, resamplers, delay estimation and reference history.
    /// Final model stage consumed by VAD, ASR and direct-audio routes for any backend.
    /// Compose subsequent model stages through IMNNAudioCaptureSource on this worker, never on device callbacks.
    /// The audio callback only downmixes/copies into a bounded SPSC queue.</summary>
    public sealed class MNNEchoCancellationWorker : IMNNAudioCaptureSource, IDisposable
    {
        private readonly IMNNAudioCaptureSource _input;
        private IMNNEchoHopProcessor _model;
        private readonly object _lifecycle = new object();
        private Task _stopTask;
        private readonly TaskCompletionSource<bool> _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        public MNNEchoDelayEstimator DelayEstimator => _delay;
        private readonly Thread _worker;
        private readonly MNNAudioCaptureBuffer _render, _processed = new MNNAudioCaptureBuffer(16000 / 128, 128);
        private readonly MNNAudioReferenceTimeline _history = new MNNAudioReferenceTimeline();
        private readonly MNNAdaptiveEchoFilter _linear = new MNNAdaptiveEchoFilter();
        private readonly float[] _linearResidual = new float[128];
        private readonly float[] _residualGains = new float[MNNDtlnAec.OutputDelaySamples / MNNDtlnAec.HopLength + 1];
        private int _gainHead;
        private float _previousGain = 1;
        private readonly MNNEchoDelayEstimator _delay = new MNNEchoDelayEstimator();
        private readonly MNNStreamingResampler _micResampler, _renderResampler;
        private readonly float[] _raw, _mono, _resampled, _renderRaw, _renderResampled, _callbackMono;
        private readonly float[] _hop = new float[128], _referenceNow = new float[128], _referenceAligned = new float[128], _clean = new float[128];
        private readonly int _rate, _micRate, _fixedDelay;
        private readonly bool _automaticDelay;
        private readonly long _streamId;
        private static long s_nextStream;
        private long _referenceSequence, _referenceFrames, _renderOrigin, _micOrigin, _micFrames, _outputSequence;
        private long _lastReferenceTicks, _healthyUntil, _referenceInvalidUntil, _gateRevision, _seenGateRevision;
        private bool _haveRenderOrigin, _haveMicOrigin, _publicationGap = true;
        private int _hopCount, _stop, _publish, _delaySamples, _alignedSamples;
        private long _hopStart;
        private long _lastMicSample, _drainRequestedTicks, _drainEnd = long.MaxValue;
        private TaskCompletionSource<bool> _drain;
        private Exception _fault;
        // Bound actual capture-to-processing latency; an isolated slow hop can be absorbed
        // by the queues and does not imply that the stream cannot sustain real time.
        private static readonly long MaximumCaptureAgeTicks = Stopwatch.Frequency / 5;
        private string _status = "Waiting for playback reference.";
        public Exception Fault => Volatile.Read(ref _fault);
        public string Status => Fault?.Message ??
            (Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastReferenceTicks) >= Stopwatch.Frequency / 4
                ? "Playback reference is unavailable or stopped." : Volatile.Read(ref _status));
        public bool Healthy => Fault == null && Stopwatch.GetTimestamp() < Interlocked.Read(ref _healthyUntil) &&
            Stopwatch.GetTimestamp() >= Interlocked.Read(ref _referenceInvalidUntil) &&
            Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastReferenceTicks) < Stopwatch.Frequency / 4;
        public int DelayMilliseconds => Volatile.Read(ref _delaySamples) / 16;
        public MNNPcmFormat CaptureFormat => new MNNPcmFormat(16000, 1);
        public int MaximumCaptureSamples => 128;
        public long DroppedCaptureBlocks => _input.DroppedCaptureBlocks + _render.DroppedBlocks + _processed.DroppedBlocks;
        public bool HasPendingCaptureFrames => _processed.HasPendingFrames;

        public MNNEchoCancellationWorker(IMNNAudioCaptureSource input, IMNNEchoHopProcessor model, int rate, MNNEchoCancellationOptions settings, int referenceBlockFrames = 0)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            input.CaptureFormat.RequireValid();
            new MNNPcmFormat(rate, 1).RequireValid();
            if (input.MaximumCaptureSamples < 1 || input.MaximumCaptureSamples % input.CaptureFormat.Channels != 0)
                throw new ArgumentException("Capture capacity must contain complete frames.", nameof(input));
            if (referenceBlockFrames < 0 || referenceBlockFrames > rate * 2) throw new ArgumentOutOfRangeException(nameof(referenceBlockFrames));
            _input = input; _model = model; _rate = rate; _micRate = input.CaptureFormat.SampleRate;
            _automaticDelay = settings.automaticDelay;
            _fixedDelay = settings.referenceDelayMilliseconds * 16;
            _streamId = Interlocked.Increment(ref s_nextStream);
            int inputFrames = input.MaximumCaptureSamples / input.CaptureFormat.Channels;
            if (referenceBlockFrames <= 0) referenceBlockFrames = (int)Math.Ceiling(inputFrames * (double)rate / _micRate);
            _raw = new float[input.MaximumCaptureSamples];
            _mono = new float[inputFrames];
            _resampled = new float[(int)Math.Ceiling(inputFrames * 16000.0 / _micRate) + 64];
            _renderRaw = new float[referenceBlockFrames];
            _callbackMono = new float[referenceBlockFrames];
            _renderResampled = new float[(int)Math.Ceiling(referenceBlockFrames * 16000.0 / rate) + 64];
            // One second of bounded reference storage. Capacity follows the actual DSP block size.
            _render = new MNNAudioCaptureBuffer(Math.Max(2, (int)Math.Ceiling(rate / (double)referenceBlockFrames)), referenceBlockFrames);
            _micResampler = new MNNStreamingResampler(new MNNPcmFormat(_micRate, 1), CaptureFormat);
            _renderResampler = new MNNStreamingResampler(new MNNPcmFormat(rate, 1), CaptureFormat);
            _worker = new Thread(Run) { IsBackground = true, Name = "MNN echo processing" };
            _worker.Start();
        }

        public void SetPublicationEnabled(bool enabled)
        {
            int next = enabled ? 1 : 0;
            if (Interlocked.Exchange(ref _publish, next) != next) Interlocked.Increment(ref _gateRevision);
        }

        public Task PausePublicationAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Volatile.Read(ref _stop) != 0 || Fault != null)
                throw new InvalidOperationException("Audio input worker is not running.", Fault);
            var pending = Volatile.Read(ref _drain);
            if (pending != null) return pending.Task;
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Exchange(ref _drainRequestedTicks, Stopwatch.GetTimestamp());
            var drain = Interlocked.CompareExchange(ref _drain, completion, null) ?? completion;
            if (Volatile.Read(ref _stop) != 0 || Fault != null)
            {
                Interlocked.CompareExchange(ref _drain, null, drain);
                drain.TrySetException(Fault ?? new OperationCanceledException("Audio input worker stopped."));
            }
            return drain.Task;
        }

        // Single backend render callback producer. No native inference, locks, waits or allocations.
        public void SubmitReference(float[] samples, int channels, long ticks, bool discontinuity = false)
            => SubmitReference(samples.AsSpan(), channels, ticks, discontinuity);

        public void SubmitReference(ReadOnlySpan<float> samples, int channels, long ticks, bool discontinuity = false)
        {
            if (Volatile.Read(ref _stop) != 0 || channels < 1 || samples.Length % channels != 0) return;
            int frames = samples.Length / channels;
            // Unity can change its DSP quantum without changing the sample rate. Keep
            // the negotiated storage and split the callback, preserving its sample clock.
            for (int offset = 0; offset < frames;)
            {
                int count = Math.Min(_callbackMono.Length, frames - offset);
                SubmitReferenceBlock(samples.Slice(offset * channels, count * channels), channels,
                    ticks + (long)offset * Stopwatch.Frequency / _rate, discontinuity && offset == 0);
                offset += count;
            }
        }

        private void SubmitReferenceBlock(ReadOnlySpan<float> samples, int channels, long ticks, bool discontinuity)
        {
            int frames = samples.Length / channels;
            bool clipped = false;
            for (int i = 0; i < frames; ++i)
            {
                double sum = 0;
                for (int channel = 0; channel < channels; ++channel) sum += samples[i * channels + channel];
                double value = sum / channels;
                if (double.IsNaN(value) || double.IsInfinity(value)) { value = 0; clipped = true; }
                if (Math.Abs(value) > 1) clipped = true;
                _callbackMono[i] = (float)Math.Max(-1, Math.Min(1, value));
            }
            var info = new MNNAudioFrameInfo(_streamId, 1, _referenceSequence++, _referenceFrames, ticks,
                new MNNPcmFormat(_rate, 1), clipped || discontinuity);
            _referenceFrames += frames;
            if (clipped) Interlocked.Exchange(ref _referenceInvalidUntil, ticks + Stopwatch.Frequency / 4);
            if (_render.TryWrite(_callbackMono, 0, frames, info)) Interlocked.Exchange(ref _lastReferenceTicks, ticks);
            else Interlocked.Exchange(ref _healthyUntil, 0);
        }

        private static long SampleTime(long ticks) => (long)(ticks * (16000.0 / Stopwatch.Frequency));
        private void ResetEchoModels()
        {
            _model.Reset(); _linear.Reset(); _alignedSamples = _gainHead = 0; _previousGain = 1;
            for (int i = 0; i < _residualGains.Length; ++i) _residualGains[i] = 1;
        }
        private void ResetProcessing()
        {
            _delay.Reset(); ResetEchoModels(); _micResampler.Reset();
            _haveMicOrigin = false; _hopCount = 0; _publicationGap = true;
            Interlocked.Exchange(ref _healthyUntil, 0);
        }
        private void ReadReference()
        {
            while (_render.TryRead(_renderRaw, 0, out var info, out int count))
            {
                long observed = SampleTime(info.MonotonicTicks);
                if (info.Discontinuity || !_haveRenderOrigin)
                {
                    _renderResampler.Reset(); _history.Reset();
                    _delay.Reset(); ResetEchoModels();
                    Interlocked.Exchange(ref _healthyUntil, 0);
                    _renderOrigin = observed; _haveRenderOrigin = true;
                }
                long first = _renderOrigin + (long)Math.Round(_renderResampler.NextOutputPosition * 16000 / _rate);
                _renderResampler.Process(_renderRaw, 0, count, _renderResampled, 0, _renderResampled.Length,
                    out int consumed, out int produced);
                if (consumed != count) throw new InvalidOperationException("Reference resampling capacity exceeded.");
                _history.Write(first, _renderResampled, produced);
            }
        }
        private void Run()
        {
            try
            {
                while (Volatile.Read(ref _stop) == 0)
                {
                    // Capture can stall without a final audio callback. Never leave a
                    // manual recording waiting indefinitely for the neural tail.
                    if (Volatile.Read(ref _drain) != null &&
                        Stopwatch.GetTimestamp() - Interlocked.Read(ref _drainRequestedTicks) > Stopwatch.Frequency)
                    {
                        _drainEnd = long.MaxValue;
                        _publicationGap = true;
                        Interlocked.Exchange(ref _healthyUntil, 0);
                        Interlocked.Exchange(ref _drain, null)?.TrySetException(
                            new TimeoutException("Microphone did not supply enough samples to drain the DTLN tail. Please record again."));
                    }
                    ReadReference();
                    // A paused manual recording still publishes its delayed neural tail.
                    // Reserve output capacity for both publication paths before consuming input.
                    if ((Volatile.Read(ref _publish) != 0 || Volatile.Read(ref _drain) != null) && _processed.AvailableBlocks <
                        (int)Math.Ceiling(_input.MaximumCaptureSamples * 16000.0 / _input.CaptureFormat.Channels / _micRate / 128) + 1)
                    {
                        Thread.Sleep(1);
                        continue;
                    }
                    if (!_input.TryReadCapture(_raw, 0, out var info, out int count))
                    {
                        if (Volatile.Read(ref _drain) != null && _drainEnd == long.MaxValue) _drainEnd = _lastMicSample;
                        Thread.Sleep(2); continue;
                    }
                    if (info.Format.SampleRate != _micRate) throw new InvalidOperationException("Capture rate changed during AEC.");
                    long observed = SampleTime(info.MonotonicTicks);
                    if (info.Discontinuity || !_haveMicOrigin) ResetProcessing();
                    if (!_haveMicOrigin) { _micOrigin = observed; _haveMicOrigin = true; }
                    int frames = count / info.Format.Channels;
                    for (int i = 0; i < frames; ++i)
                    {
                        double sum = 0;
                        for (int channel = 0; channel < info.Format.Channels; ++channel) sum += _raw[i * info.Format.Channels + channel];
                        _mono[i] = (float)Math.Max(-1, Math.Min(1, sum / info.Format.Channels));
                    }
                    long first = _micOrigin + (long)Math.Round(_micResampler.NextOutputPosition * 16000 / _micRate);
                    _micResampler.Process(_mono, 0, frames, _resampled, 0, _resampled.Length, out int consumed, out int produced);
                    if (consumed != frames) throw new InvalidOperationException("Capture resampling capacity exceeded.");
                    _lastMicSample = first + produced;
                    for (int i = 0; i < produced; ++i)
                    {
                        if (_hopCount == 0) _hopStart = first + i;
                        _hop[_hopCount++] = _resampled[i];
                        if (_hopCount != 128) continue;
                        ProcessHop();
                        _hopCount = 0;
                    }
                }
            }
            catch (Exception error) { Volatile.Write(ref _fault, error); }
            finally
            {
                Interlocked.Exchange(ref _healthyUntil, 0);
                Interlocked.Exchange(ref _drain, null)?.TrySetException(Fault ?? new OperationCanceledException("Audio input worker stopped."));
                _completion.TrySetResult(true);
            }
        }

        private void ProcessHop()
        {
            long captureTicks = (long)(_hopStart * (Stopwatch.Frequency / 16000.0));
            // Automatic estimation needs simultaneous PCM. A measured route only needs its
            // delayed acoustic reference (including the causal FIR's alignment headroom).
            // Do not wait for unrelated newer reference or run an unused automatic search.
            long referenceTime = _hopStart - (_automaticDelay ? 0 : _fixedDelay);
            int referenceHeadroom = _automaticDelay ? 0 : Math.Min(_fixedDelay, MNNEchoDelayEstimator.AlignmentHeadroomSamples);
            while (_history.EndSample < referenceTime + referenceHeadroom + 128 && Volatile.Read(ref _stop) == 0 &&
                Stopwatch.GetTimestamp() - captureTicks < MaximumCaptureAgeTicks &&
                Stopwatch.GetTimestamp() - Interlocked.Read(ref _lastReferenceTicks) < Stopwatch.Frequency / 4)
            {
                Thread.Sleep(1);
                ReadReference();
            }
            bool referencePresent = _history.Read(referenceTime, _referenceNow);
            bool wasLocked = !_automaticDelay || _delay.IsLocked;
            if (_automaticDelay)
            {
                if (referencePresent) _delay.Process(_hop, _referenceNow);
                else { _delay.Reset(); Interlocked.Exchange(ref _healthyUntil, 0); }
            }
            int delay = _automaticDelay ? _delay.DelaySamples : _fixedDelay;
            if (Math.Abs(delay - _delaySamples) > 32 || (!wasLocked && _delay.IsLocked))
            {
                // Alignment changes recurrent echo state, not microphone sample continuity.
                ResetEchoModels();
            }
            Volatile.Write(ref _delaySamples, delay);
            bool aligned = _history.Read(_hopStart - delay, _referenceAligned);
            // Keep the neural model's original input distribution. A parallel linear model
            // estimates how much microphone power cannot be explained by the playback path.
            _model.ProcessHop(_hop, _referenceAligned, _clean);
            // Reserve aggregation uncertainty plus bulk-delay hysteresis in the causal FIR.
            // Neither band alone covers the returned estimate's late-alignment error.
            int headroom = Math.Min(delay, MNNEchoDelayEstimator.AlignmentHeadroomSamples);
            _history.Read(_hopStart - delay + headroom, _referenceAligned);
            // Clipping the advance near zero delay does not shrink the possible
            // positive lag error: retain that side of the estimator uncertainty.
            float coherence = _linear.EstimateCoherence(_hop, _referenceAligned,
                headroom + MNNEchoDelayEstimator.AlignmentHeadroomSamples);
            _linear.Process(_hop, _referenceAligned, _linearResidual, coherence);
            _residualGains[_gainHead] = _linear.ResidualPowerRatio;
            _gainHead = (_gainHead + 1) % _residualGains.Length;
            // Wiener amplitude gain: estimated non-echo power / microphone power.
            // Match it to the signal time represented by DTLN's overlap-add output.
            float gain = _residualGains[_gainHead];
            for (int i = 0; i < _clean.Length; ++i)
            {
                float interpolated = _previousGain + (gain - _previousGain) * (i + 1) / 128;
                _clean[i] = Math.Max(-1, Math.Min(1, _clean[i] * interpolated));
            }
            _previousGain = gain;
            long now = Stopwatch.GetTimestamp();
            bool current = now - captureTicks < MaximumCaptureAgeTicks;
            bool locked = !_automaticDelay || _delay.IsLocked;
            // A reset output still contains the model's analysis/overlap transient.
            // Qualify only after one complete analysis window with aligned reference.
            if (referencePresent && aligned && locked && current) _alignedSamples = Math.Min(MNNDtlnAec.BlockLength, _alignedSamples + 128);
            else _alignedSamples = 0;
            if (_alignedSamples >= MNNDtlnAec.BlockLength)
                Interlocked.Exchange(ref _healthyUntil, now + Stopwatch.Frequency / 4);
            else Interlocked.Exchange(ref _healthyUntil, 0);
            Volatile.Write(ref _status, !referencePresent || !aligned ? "Waiting for synchronized playback reference." :
                !locked ? "Waiting for a stable echo delay during speaker playback." :
                !current ? "Echo processing is behind microphone capture." :
                _alignedSamples < MNNDtlnAec.BlockLength ? "Priming aligned echo analysis." : "Echo processing is ready for barge-in.");
            long revision = Interlocked.Read(ref _gateRevision);
            bool publishing = Volatile.Read(ref _publish) != 0;
            if (_seenGateRevision != revision && publishing) { _publicationGap = true; _seenGateRevision = revision; }
            var drain = Volatile.Read(ref _drain);
            if (!publishing && drain == null) { _publicationGap = true; return; }
            // Timestamp the signal represented by the delayed DTLN output, rather than its enqueue time.
            long sampleTime = _hopStart - MNNDtlnAec.OutputDelaySamples;
            int outputCount = drain == null || _drainEnd == long.MaxValue ? 128 : (int)Math.Max(0, Math.Min(128, _drainEnd - sampleTime));
            if (outputCount == 0)
            {
                _drainEnd = long.MaxValue;
                Interlocked.Exchange(ref _drain, null)?.TrySetResult(true);
                _publicationGap = true;
                return;
            }
            var info = new MNNAudioFrameInfo(_streamId, 1, _outputSequence++, _micFrames,
                Math.Max(0, (long)(sampleTime * (Stopwatch.Frequency / 16000.0))), CaptureFormat, _publicationGap);
            _micFrames += outputCount;
            _publicationGap = !_processed.TryWrite(_clean, 0, outputCount, info);
            if (_publicationGap) Interlocked.Exchange(ref _healthyUntil, 0);
        }
        public bool TryReadCapture(float[] destination, int offset, out MNNAudioFrameInfo info, out int count) =>
            _processed.TryRead(destination, offset, out info, out count);
        /// <summary>Gate capture/reference producers before calling. Never waits synchronously for inference.
        /// A failed model release retains ownership; the next StopAsync retries it.</summary>
        public Task StopAsync()
        {
            lock (_lifecycle)
            {
                Interlocked.Exchange(ref _healthyUntil, 0);
                Volatile.Write(ref _stop, 1);
                if (_stopTask == null || _stopTask.IsFaulted || _stopTask.IsCanceled) _stopTask = StopCoreAsync();
                return _stopTask;
            }
        }
        private async Task StopCoreAsync()
        {
            await _completion.Task.ConfigureAwait(false);
            await Task.Run(() =>
            {
                _worker.Join();
                if (_model != null) { _model.Dispose(); _model = null; }
            }).ConfigureAwait(false);
        }
        /// <summary>Blocking compatibility entry point for non-UI control threads. Prefer StopAsync in application lifecycles.</summary>
        public void Dispose() => StopAsync().GetAwaiter().GetResult();
    }
}
