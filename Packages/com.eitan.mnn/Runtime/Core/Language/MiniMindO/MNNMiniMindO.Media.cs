using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    public sealed partial class MNNMiniMindO
    {
        private Input[] PrepareInputs(MNNMiniMindORequest request, int budget, CancellationToken cancellation)
        {
            float[] audio = null, image = null;
            string content = request.Text;
            if (content.Length + (request.History ?? Array.Empty<MNNChatMessage>()).Sum(m => m?.Content.Length ?? 0) > 16384)
                throw new ArgumentException("MiniMind-o text exceeds the bounded tokenizer input size.");
            if (request.Audio16Khz != null)
            {
                if (!SupportsAudioInput) throw new NotSupportedException("Install MiniMind-o's audio encoder and multimodal prefill graphs.");
                ValidateSamples(request.Audio16Khz, 1360, 16000 * 20, "Audio16Khz");
                var features = MNNSenseVoiceFeatures.Extract(request.Audio16Khz, out int frames, cancellation);
                using (var graph = Graph("audio_encoder"))
                    audio = graph.Run("features", new Input("fbank", features, 1, frames, 560));
                content = string.Concat(Enumerable.Repeat("<|audio_pad|>", audio.Length / 768)) + content;
            }
            if (request.ImagePixels != null)
            {
                if (!SupportsImageInput) throw new NotSupportedException("Install MiniMind-o's vision encoder and multimodal prefill graphs.");
                ValidateSamples(request.ImagePixels, 3 * 256 * 256, 3 * 256 * 256, "ImagePixels");
                using (var graph = Graph("vision_encoder"))
                    image = graph.Run("features", new Input("pixels", request.ImagePixels, 1, 3, 256, 256));
                content = string.Concat(Enumerable.Repeat("<|image_pad|>", 64)) + content;
            }
            cancellation.ThrowIfCancellationRequested();
            var history = (request.History ?? Array.Empty<MNNChatMessage>()).ToList();
            int[] tokens = _tokenizer.Encode(MNNMiniMindOTokenizer.Prompt(content, history.ToArray()));
            while (request.TrimHistoryToFit && tokens.Length + budget > 512)
            {
                int oldest = history.FindIndex(message => message.Role != MNNChatRole.System);
                if (oldest < 0) break;
                history.RemoveAt(oldest);
                while (oldest < history.Count && history[oldest].Role == MNNChatRole.Assistant) history.RemoveAt(oldest);
                tokens = _tokenizer.Encode(MNNMiniMindOTokenizer.Prompt(content, history.ToArray()));
            }
            if (tokens.Length + budget > 512)
                throw new ArgumentException("MiniMind-o's prompt plus generation budget exceeds the adapter's 512-token memory bound.");
            var inputs = new List<Input> { new Input("tokens", tokens, 1, tokens.Length),
                new Input("audio_codes", Enumerable.Repeat(2049, 8 * tokens.Length).ToArray(), 1, 8, tokens.Length) };
            if (audio != null || image != null)
            {
                var values = new float[tokens.Length * 768];
                var mask = new float[tokens.Length];
                InjectFeatures(tokens, 16, audio, values, mask);
                InjectFeatures(tokens, 12, image, values, mask);
                inputs.Add(new Input("features", values, 1, tokens.Length, 768));
                inputs.Add(new Input("feature_mask", mask, 1, tokens.Length, 1));
            }
            return inputs.ToArray();
        }

        private static void ValidateSamples(float[] values, int min, int max, string name)
        {
            if (values.Length < min || values.Length > max || values.Any(v => float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 1.001f))
                throw new ArgumentException(name + " has an invalid length or samples outside [-1,1].", name);
        }

        private static void InjectFeatures(int[] tokens, int marker, float[] source, float[] target, float[] mask)
        {
            if (source == null) return;
            int at = 0;
            foreach (int i in Enumerable.Range(0, tokens.Length).Where(i => tokens[i] == marker))
            {
                if (at + 768 > source.Length) throw new InvalidDataException("Unexpected media marker in prompt.");
                Array.Copy(source, at, target, i * 768, 768);
                mask[i] = 1; at += 768;
            }
            if (at != source.Length) throw new InvalidDataException("MiniMind-o media/token count mismatch.");
        }

        private float[] DecodeChunk(List<int[]> frames, bool speech, bool flush, CancellationToken cancellation)
        {
            int remainder = frames.Count % 4;
            int count = flush ? remainder : remainder == 0 && frames.Count > 0 ? 4 : 0;
            if (!speech || !SupportsSpeechOutput || count == 0) return Array.Empty<float>();
            cancellation.ThrowIfCancellationRequested();
            int overlap = Math.Min(2, frames.Count - count), total = overlap + count;
            var codes = new int[8 * total];
            for (int i = 0; i < 8; ++i)
                for (int t = 0; t < total; ++t) codes[i * total + t] = frames[frames.Count - total + t][i];
            _mimi = _mimi ?? Graph("mimi_decoder");
            float[] pcm = _mimi.Run("pcm", new Input("codes", codes, 1, 8, total));
            if (pcm.Length != total * 1920) throw new InvalidDataException("Mimi output must contain 1920 samples per code frame.");
            return pcm.Skip(overlap * 1920).ToArray();
        }
    }
}
