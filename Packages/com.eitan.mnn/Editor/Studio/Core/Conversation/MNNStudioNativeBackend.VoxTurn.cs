using System;
using System.Globalization;
using System.Text;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend
    {
        private MNNStudioResult RunVoxTurn(MNNStudioRequest request)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            float[] samples = null;
            double duration = 0;
            if (!string.IsNullOrWhiteSpace(request.Audio))
            {
                var source = MNNWaveAudio.Read(request.Audio, out int rate);
                if (rate < 8000 || rate > 96000 || source.Length > (long)rate * 600)
                    throw new ArgumentException("Supply a mono WAV at 8–96 kHz, up to ten minutes.");
                duration = (double)source.Length / rate;
                samples = MNNSpeechDsp.Resample(source, rate, 16000, request.Cancellation, tailSamples: 128000);
            }
            var input = new MNNVoxTurnRequest(samples, request.Prompt, request.TurnLanguage, request.TurnSpeaker);
            var decision = _voxTurn.Predict(input, request.TurnQuestions, request.Cancellation);
            var text = new StringBuilder();
            foreach (var pair in decision.Answers)
            {
                text.Append(pair.Key).Append(':');
                for (int i = 0; i < pair.Value.Labels.Count; ++i)
                    text.Append(' ').Append(pair.Value.Labels[i]).Append('=')
                        .Append(pair.Value.Probabilities[i].ToString("P2", CultureInfo.InvariantCulture));
                text.AppendLine();
            }
            MNNTurnDetectionResult turn = null;
            if (decision.Answers.TryGetValue("eot", out var ended))
                turn = new MNNTurnDetectionResult(ended.Probability("yes"), .5f, duration);
            return new MNNStudioResult
            {
                Text = text.ToString().TrimEnd(), TurnDetection = turn, TurnDecisions = decision,
                BackendDescription = "VoxTurn v1 · audio " + _voxTurn.AudioBackend + " / language " +
                    _voxTurn.FirstLanguageBackend + "+" + _voxTurn.SecondLanguageBackend + " / High / CPU4"
            };
        }
    }
}
