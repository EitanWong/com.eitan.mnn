using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MNN.Unity
{
    /// <summary>Speech transcript, token IDs and optional token start times in seconds. Timestamps are empty when the decoder does not provide alignment.</summary>
    public sealed class MNNSpeechRecognition
    {
        public string Text { get; }
        public IReadOnlyList<int> TokenIds { get; }
        public IReadOnlyList<float> Timestamps { get; }
        internal MNNSpeechRecognition(string text, int[] tokens, float[] timestamps)
        {
            Text = text;
            TokenIds = new ReadOnlyCollection<int>((int[])tokens.Clone());
            Timestamps = new ReadOnlyCollection<float>((float[])timestamps.Clone());
        }
    }
}
