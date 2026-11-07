using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Final listener mix: includes speech fade, source gain and Unity cue sounds.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MNN/Realtime/Unity Echo Reference Tap")]
    [RequireComponent(typeof(AudioListener))]
    public sealed class MNNUnityEchoReferenceTap : MonoBehaviour
    {
        private ReferenceBinding _binding;
        public void Bind(MNNEchoCancellationWorker capture, int sampleRate, double dspBufferMilliseconds)
        {
            if (capture == null) throw new ArgumentNullException(nameof(capture));
            var previous = Volatile.Read(ref _binding);
            if (previous != null && !ReferenceEquals(previous.Capture, capture))
                throw new InvalidOperationException("The Unity listener already belongs to another echo-control session.");
            // A late callback retains only its old binding. The main thread never
            // resets the sample clock an in-flight audio callback is still updating.
            Volatile.Write(ref _binding, new ReferenceBinding(capture, sampleRate, dspBufferMilliseconds));
        }
        public void Detach(MNNEchoCancellationWorker capture)
        {
            var binding = Volatile.Read(ref _binding);
            if (binding != null && ReferenceEquals(binding.Capture, capture))
                Interlocked.CompareExchange(ref _binding, null, binding);
        }
        private void OnAudioFilterRead(float[] samples, int channels)
        {
            Volatile.Read(ref _binding)?.Submit(samples, channels, Stopwatch.GetTimestamp());
        }

        private sealed class ReferenceBinding
        {
            internal readonly MNNEchoCancellationWorker Capture;
            private readonly int _sampleRate;
            private readonly long _bufferTicks;
            private long _origin, _frames;
            internal ReferenceBinding(MNNEchoCancellationWorker capture, int sampleRate, double dspBufferMilliseconds)
            {
                Capture = capture;
                _sampleRate = sampleRate;
                _bufferTicks = (long)(dspBufferMilliseconds * Stopwatch.Frequency / 1000);
            }
            internal void Submit(float[] samples, int channels, long now)
            {
                if (channels < 1 || samples.Length % channels != 0) return;
                int frames = samples.Length / channels;
                long expected = _origin + _frames * Stopwatch.Frequency / _sampleRate;
                // Remove callback jitter while restarting after an actual device pause.
                bool gap = _origin == 0 || Math.Abs(now - expected) > _bufferTicks + (long)frames * Stopwatch.Frequency / _sampleRate;
                if (gap) { _origin = now; _frames = 0; expected = now; }
                Capture.SubmitReference(samples, channels, expected, gap);
                _frames += frames;
            }
        }
    }
}
