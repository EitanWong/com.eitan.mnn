using System;
using System.Linq;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend
    {
        private MNNStudioResult ExecuteMiniMindO(MNNStudioRequest request)
        {
            var input = new MNNMiniMindORequest { Text = request.Prompt ?? "" };
            if (!string.IsNullOrEmpty(request.Image))
            {
                if (request.VisionImage == null) throw new ArgumentException("Decode the MiniMind-o image on the Editor thread before inference.");
                input.ImagePixels = MNNMiniMindO.PrepareImage(request.VisionImage, request.Cancellation);
            }
            if (!string.IsNullOrEmpty(request.Audio))
            {
                var samples = MNNWaveAudio.Read(request.Audio, out int rate);
                input.Audio16Khz = MNNAudioResampling.Resample(samples, rate, 16000, request.Cancellation);
            }
            if (request.History != null)
                input.History = request.History.Select(m => new MNNChatMessage(m.IsUser ? MNNChatRole.User : MNNChatRole.Assistant, m.Text)).ToArray();
            else if (request.Conversation != null)
            {
                var messages = request.Conversation;
                // Conversation requests include their current user message at the end.
                int count = messages.Length;
                if (count > 0 && messages[count - 1].Role == MNNChatRole.User)
                {
                    input.Text = messages[count - 1].Content;
                    --count;
                }
                input.History = messages.Take(count).ToArray();
            }
            var result = _miniMindO.Generate(input, new MNNMiniMindOOptions {
                MaxNewTokens = request.TokenLimit, Seed = request.Seed, GenerateSpeech = request.Speech },
                update => request.Progress?.Invoke(new MNNGenerationUpdate(update.Text, update.GeneratedTokens)), request.Cancellation);
            return new MNNStudioResult { Text = result.Text, Tokens = result.GeneratedTokens,
                Limited = result.TokenLimitReached, Waveform = result.Waveform, SampleRate = result.SampleRate,
                BackendDescription = "MiniMind-o · CPU / Low memory · FP16 prefill + Thinker W8/block64, Talker FP16" };
        }
    }
}
