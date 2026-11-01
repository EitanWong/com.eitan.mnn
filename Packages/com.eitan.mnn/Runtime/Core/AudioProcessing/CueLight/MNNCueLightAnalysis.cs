using System;
using System.Globalization;
using System.Text;

namespace MNN.Unity
{
    public sealed class MNNCueLightFrame
    {
        public float[] Probabilities { get; }
        public float SpeechProbability => Probabilities[0];
        public float EndOfTurnProbability => Probabilities[1];
        public int ResetCount { get; }
        public bool SnappyEndpoint { get; }
        public bool CarefulEndpoint { get; }
        internal MNNCueLightFrame(float[] probabilities, int resets, bool snappy, bool careful)
        { Probabilities = probabilities; ResetCount = resets; SnappyEndpoint = snappy; CarefulEndpoint = careful; }
    }
    public sealed class MNNCueLightAnalysis
    {
        public MNNCueLightFrame[] Frames { get; }
        public int FrameCount => Frames.Length;
        public double DurationSeconds { get; }
        public float[] Probabilities
        {
            get
            {
                var values = new float[FrameCount * 6];
                for (int i = 0; i < FrameCount; ++i) Array.Copy(Frames[i].Probabilities, 0, values, i * 6, 6);
                return values;
            }
        }
        internal MNNCueLightAnalysis(MNNCueLightFrame[] frames, double duration) { Frames = frames; DurationSeconds = duration; }
        public string ToTsv()
        {
            var text = new StringBuilder("time_seconds\tp_speech\tp_end\tresets\tsnappy_endpoint\tcareful_endpoint\n");
            for (int i = 0; i < Frames.Length; ++i)
            {
                var f = Frames[i];
                text.Append(((i + 1) * .02).ToString("F2", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(f.SpeechProbability.ToString("R", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(f.EndOfTurnProbability.ToString("R", CultureInfo.InvariantCulture)).Append('\t')
                    .Append(f.ResetCount).Append('\t').Append(f.SnappyEndpoint ? '1' : '0').Append('\t')
                    .Append(f.CarefulEndpoint ? '1' : '0').Append('\n');
            }
            return text.ToString();
        }
    }
}
