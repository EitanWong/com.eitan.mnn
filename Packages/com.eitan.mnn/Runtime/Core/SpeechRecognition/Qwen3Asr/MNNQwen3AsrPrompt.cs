using System;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Language conditioning for the pinned Qwen3-ASR audio template.</summary>
    internal static class MNNQwen3AsrPrompt
    {
        [Serializable] private sealed class Options
        {
            public string asr_language;
            public Template jinja = new Template();
        }
        [Serializable] private sealed class Template
        {
            // Keep MNN's audio delimiters and the upstream Qwen role newlines. Whitespace
            // control after the assistant role would erase the newline before generation.
            public string chat_template = "{%- set content = messages[-1].content -%}<|im_start|>system\n<|im_end|>\n<|im_start|>user\n{{ content }}<|im_end|>\n{% if add_generation_prompt and content is string and '<audio>' in content and '</audio>' in content %}<|im_start|>assistant\n{% if asr_language %}language {{ asr_language }}<asr_text>{% endif %}{% endif %}";
            public string eos = "<|im_end|>";
        }

        internal static string NormalizeLanguage(string language)
        {
            // Null preserves the original direct recognizer API's installed template.
            if (language == null) return null;
            switch (language.Trim().ToLowerInvariant())
            {
                case "": case "auto": return "";
                case "zh": case "chinese": return "Chinese";
                case "en": case "english": return "English";
                case "ja": case "japanese": return "Japanese";
                case "ko": case "korean": return "Korean";
                case "yue": case "cantonese": return "Cantonese";
                default: throw new ArgumentException("Qwen3-ASR language must be auto, zh, en, ja, ko or yue.", nameof(language));
            }
        }

        internal static string Configuration(string normalizedLanguage) => normalizedLanguage == null ? null :
            JsonUtility.ToJson(new Options { asr_language = normalizedLanguage });

        internal static string Transcript(string raw, bool automaticLanguage)
        {
            string text = (raw ?? "").Trim();
            if (!automaticLanguage) return text;
            int tag = text.IndexOf("<asr_text>", StringComparison.Ordinal);
            // Match the upstream parser: tagless output is already plain text.
            return tag < 0 ? text : text.Substring(tag + "<asr_text>".Length).Trim();
        }
    }
}
