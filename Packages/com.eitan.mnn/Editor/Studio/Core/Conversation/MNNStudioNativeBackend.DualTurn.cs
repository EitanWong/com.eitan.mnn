using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend
    {
        private MNNDualTurnEndpointing _dualTurn;

        private MNNStudioResult RunDualTurn(MNNStudioRequest request)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.Audio))
                throw new ArgumentException("Choose a mono user WAV or stereo WAV with user on CH0 and agent on CH1.");
            float[][] channels = MNNWaveAudio.ReadChannels(request.Audio, out int rate, false);
            if (channels[0].Length < 1 || channels[0].Length > rate * 120L)
                throw new InvalidDataException("Choose a nonempty WAV lasting at most 120 seconds.");
            for (int channel = 0; channel < channels.Length; ++channel)
                channels[channel] = MNNSpeechDsp.Resample(channels[channel], rate, MNNDualTurnEndpointing.SampleRate, request.Cancellation);
            int validSamples = channels[0].Length;
            var frames = new List<MNNDualTurnEndpointingResult>();
            var user = new float[MNNDualTurnEndpointing.TickSamples];
            var agent = channels.Length == 2 ? new float[user.Length] : null;
            int sustained = 0;
            _dualTurn.Reset();
            for (int offset = 0; offset < validSamples; offset += user.Length)
            {
                request.Cancellation.ThrowIfCancellationRequested();
                int count = Math.Min(user.Length, validSamples - offset);
                Array.Clear(user, 0, user.Length);
                Array.Copy(channels[0], offset, user, 0, count);
                if (agent != null)
                {
                    Array.Clear(agent, 0, agent.Length);
                    Array.Copy(channels[1], offset, agent, 0, count);
                }
                var frame = _dualTurn.Push(user, agent, request.Cancellation);
                frames.Add(frame);
                sustained = frame.EndOfTurnProbability > .5 ? sustained + 1 : 0;
            }
            var last = frames[frames.Count - 1];
            int padding = frames.Count * user.Length - validSamples;
            var text = new StringBuilder();
            text.AppendLine(channels.Length == 2 ? "CH0: user · CH1: agent" : "Mono: user · agent silent");
            text.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "Input: {0:F3} s · Frames: {1} · Final-frame silence padding: {2} samples\nEOT > 0.5 for three frames: {3}",
                validSamples / 24000.0, frames.Count, padding, sustained >= 3 ? "yes" : "no"));
            text.AppendLine("Time (s) | EOT | User VAD | Agent VAD | User FVAD [0–240, 240–640, 640–1200, 1200–2000 ms] | Agent FVAD");
            foreach (var frame in frames)
            {
                text.AppendFormat(CultureInfo.InvariantCulture, "{0:F3} | {1:F6} | {2:F6} | {3:F6}",
                    frame.ProcessedSeconds, frame.EndOfTurnProbability, frame.UserVoiceProbability, frame.AgentVoiceProbability);
                AppendFutureProbabilities(text, frame.UserFutureVoiceProbabilities);
                AppendFutureProbabilities(text, frame.AgentFutureVoiceProbabilities);
                text.AppendLine();
            }
            return new MNNStudioResult
            {
                Text = text.ToString().TrimEnd(), DualTurnFrames = frames.AsReadOnly(),
                TurnDetection = new MNNTurnDetectionResult(last.EndOfTurnProbability, .5f, validSamples / 24000.0),
                BackendDescription = "DualTurn Endpointing 80 ms · " + _dualTurn.Backend + " / High"
            };
        }

        private static void AppendFutureProbabilities(StringBuilder text, IReadOnlyList<float> values)
        {
            text.Append(" | ");
            for (int i = 0; i < values.Count; ++i)
            {
                if (i > 0) text.Append(", ");
                text.Append(values[i].ToString("F6", CultureInfo.InvariantCulture));
            }
        }

        private void DisposeDualTurn()
        {
            _dualTurn?.Dispose();
            _dualTurn = null;
        }
    }
}
