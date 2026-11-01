using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>Owned speech intervals and probabilities; each model defines its frame cadence.</summary>
    public sealed class MNNVoiceActivityResult
    {
        public IReadOnlyList<MNNSpeechSegment> Segments { get; }
        public IReadOnlyList<float> Probabilities { get; }
        public double AudioSeconds { get; }
        internal MNNVoiceActivityResult(MNNSpeechSegment[] segments, float[] probabilities, double duration)
        {
            // Resampling rounds the final 16 kHz sample up. Keep timestamps inside the source duration.
            Segments = new ReadOnlyCollection<MNNSpeechSegment>(segments.Select(segment =>
                new MNNSpeechSegment(Math.Min(duration, segment.StartSeconds), Math.Min(duration, segment.EndSeconds))).ToArray());
            Probabilities = new ReadOnlyCollection<float>((float[])probabilities.Clone());
            AudioSeconds = duration;
        }
    }
}
