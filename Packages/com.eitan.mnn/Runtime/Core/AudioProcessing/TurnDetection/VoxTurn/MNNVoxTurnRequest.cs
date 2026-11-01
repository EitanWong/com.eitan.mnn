using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>Immutable request: optional normalized mono 16 kHz audio and transcript context.</summary>
    public sealed class MNNVoxTurnRequest
    {
        internal readonly float[] Samples;
        public string Text
        {
            get;
        }
        public string Language
        {
            get;
        }
        public string Speaker
        {
            get;
        }
        public bool HasAudio => Samples != null;
        public MNNVoxTurnRequest(float[] samples = null, string text = null, string language = "en", string speaker = "user")
        {
            if (speaker != "user" && speaker != "agent")
                throw new ArgumentException("Speaker must be user or agent.", nameof(speaker));
            if (text != null && text.Length > 1048576)
                throw new ArgumentException("Transcript exceeds one million characters.", nameof(text));
            if (samples != null && samples.Length > 16000L * 600)
                throw new ArgumentException("Supply at most ten minutes of audio.", nameof(samples));
            if (samples != null)
            {
                for (int i = 0; i < samples.Length; ++i)
                    if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i]) > 1)
                        throw new ArgumentException("Audio must be normalized finite mono 16 kHz PCM.", nameof(samples));
                int count = Math.Min(samples.Length, 128000);
                Samples = new float[count];
                Array.Copy(samples, samples.Length - count, Samples, 0, count);
            }
            Text = text;
            Language = language == "zh" ? "zh" : "en";
            Speaker = speaker;
        }
    }
}
