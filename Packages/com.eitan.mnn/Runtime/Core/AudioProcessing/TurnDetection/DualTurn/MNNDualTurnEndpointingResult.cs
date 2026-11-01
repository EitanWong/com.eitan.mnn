using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>One 80 ms frame: user EOT, both VAD channels, and four FVAD horizons per channel.</summary>
    public sealed class MNNDualTurnEndpointingResult
    {
        public float EndOfTurnProbability { get; }
        public float UserVoiceProbability { get; }
        public float AgentVoiceProbability { get; }
        public IReadOnlyList<float> UserFutureVoiceProbabilities { get; }
        public IReadOnlyList<float> AgentFutureVoiceProbabilities { get; }
        public long TickCount { get; }
        public double ProcessedSeconds => TickCount * .08;

        internal MNNDualTurnEndpointingResult(float eot, float[] vad, float[] fvad, long tick)
        {
            EndOfTurnProbability = eot;
            UserVoiceProbability = vad[0];
            AgentVoiceProbability = vad[1];
            UserFutureVoiceProbabilities = Array.AsReadOnly(new[] { fvad[0], fvad[1], fvad[2], fvad[3] });
            AgentFutureVoiceProbabilities = Array.AsReadOnly(new[] { fvad[4], fvad[5], fvad[6], fvad[7] });
            TickCount = tick;
        }
    }
}
