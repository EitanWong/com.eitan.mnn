using System;
using System.IO;

namespace MNN.Unity
{
    internal static class MNNTtsModelProfiles
    {
        internal static MNNTtsModelProfile Detect(string directory, string name = null)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return MNNTtsModelProfile.Unknown;
            if (MNNCosyVoice300MModel.HasLayout(directory))
                return MNNTtsModelProfile.CosyVoice300M;
            if (File.Exists(Path.Combine(directory, "gpt_prefill_fp16.mnn")) || string.Equals(name, "IndexTTS-2.5-mnn", StringComparison.OrdinalIgnoreCase))
                return MNNTtsModelProfile.IndexTts25;
            if (File.Exists(Path.Combine(directory, "redae_encoder.mnn")) || string.Equals(name, "fireredtts3-mnn", StringComparison.OrdinalIgnoreCase))
                return MNNTtsModelProfile.FireRedTts3;
            if (File.Exists(Path.Combine(directory, "config-cpu-cosyvoice-ras.json")) || File.Exists(Path.Combine(directory, "model", "config-cpu-cosyvoice-ras.json")) || string.Equals(name, "Cosy-Voice-MNN", StringComparison.OrdinalIgnoreCase))
                return MNNTtsModelProfile.CosyVoice;
            if (MNNIndicF5Tts.IsComplete(directory))
                return MNNTtsModelProfile.IndicF5;
            if (MNNPocketTts.IsComplete(directory))
                return MNNTtsModelProfile.Pocket;
            if (MNNQwen3ExplicitTts.HasLayout(directory))
                return MNNTtsModelProfile.Qwen3Explicit;
            if (MNNDhVaani.IsComplete(directory))
                return MNNTtsModelProfile.DhVaani;
            if (MNNMossNano.IsComplete(directory))
                return MNNTtsModelProfile.MossNano;
            if (MNNSupertonic.IsComplete(directory))
                return MNNTtsModelProfile.Supertonic;
            if (MNNKokoroModel.IsComplete(directory))
                return MNNTtsModelProfile.Kokoro;
            if (MNNSherpaVitsChinese.IsComplete(directory))
                return MNNTtsModelProfile.SherpaVitsChinese;
            if (MNNMmsTamil.IsComplete(directory))
                return MNNTtsModelProfile.MmsTamil;
            if (MNNIndicVits.IsComplete(directory))
                return MNNTtsModelProfile.IndicVits;
            string config = Path.Combine(directory, "config.json");
            if (File.Exists(config))
            {
                try
                {
                    var metadata = MNNModelData.Object(MNNModelData.Read(config));
                    metadata.TryGetValue("model_type", out var modelType);
                    if (string.Equals(modelType as string, "piper", StringComparison.OrdinalIgnoreCase))
                        return MNNTtsModelProfile.Piper;
                    if (string.Equals(modelType as string, "bertvits", StringComparison.OrdinalIgnoreCase))
                        return MNNTtsModelProfile.BertVits2;
                    if (string.Equals(modelType as string, "supertonic", StringComparison.OrdinalIgnoreCase))
                        return MNNTtsModelProfile.Supertonic;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
                catch (ArgumentException)
                {
                }
            }

            // Do not infer a frontend from a single .mnn filename.
            // Other Qwen3-TTS and VITS exports need family-specific tokenizers
            // and autoregressive state handling that cannot be guessed safely.
            return MNNTtsModelProfile.Unknown;
        }

        internal static bool IsImplemented(MNNTtsModelProfile profile) => profile != MNNTtsModelProfile.Unknown;
    }
}
