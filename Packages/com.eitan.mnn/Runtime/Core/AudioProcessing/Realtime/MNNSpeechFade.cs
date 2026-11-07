using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Single render-thread gain ramp; requests may come from UI or VAD workers.</summary>
    public sealed class MNNSpeechFade
    {
        public const int DurationMilliseconds = 24;
        private MNNResponseId _renderResponse;
        private sealed class Request
        {
            internal readonly MNNResponseId Response;
            internal int Completed;
            internal Request(MNNResponseId response) { Response = response; }
        }
        private Request _request;
        private int _remainingFrames;
        private float _gain = 1f;
        public Task FadeOutAsync(MNNResponseId response)
        {
            if (!response.IsValid) return Task.CompletedTask;
            var request = new Request(response);
            Volatile.Write(ref _request, request);
            return WaitForRenderAsync(request);
        }
        private static async Task WaitForRenderAsync(Request request)
        {
            // Wait for the actual ramp, including the route's DSP block size. Never wait indefinitely
            // for a device that stopped rendering; session teardown must still be able to cancel.
            long deadline = Stopwatch.GetTimestamp() + Stopwatch.Frequency / 2;
            while (Volatile.Read(ref request.Completed) == 0 && Stopwatch.GetTimestamp() < deadline)
                await Task.Delay(5).ConfigureAwait(false);
        }
        public void Process(float[] samples, int count, int channels, int sampleRate, MNNResponseId response)
        {
            var format = new MNNPcmFormat(sampleRate, channels);
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (count < 0 || count > samples.Length || count % format.Channels != 0)
                throw new ArgumentException("Supply complete interleaved PCM frames.", nameof(count));
            if (response != _renderResponse)
            {
                _renderResponse = response;
                _gain = 1f;
                _remainingFrames = 0;
            }
            var request = Volatile.Read(ref _request);
            bool fading = response.IsValid && request?.Response == response;
            if (fading && _remainingFrames == 0 && _gain > 0f)
                _remainingFrames = Math.Max(1, sampleRate * DurationMilliseconds / 1000);
            for (int i = 0; i < count; i += channels)
            {
                if (fading && _remainingFrames > 0)
                {
                    _gain = Math.Max(0f, _gain - _gain / _remainingFrames);
                    --_remainingFrames;
                }
                // Smoothstep keeps the beginning and end of the fade soft.
                float gain = _gain * _gain * (3f - 2f * _gain);
                for (int c = 0; c < channels; ++c) samples[i + c] *= gain;
            }
            if (fading && _gain == 0f) Volatile.Write(ref request.Completed, 1);
        }
    }
}
