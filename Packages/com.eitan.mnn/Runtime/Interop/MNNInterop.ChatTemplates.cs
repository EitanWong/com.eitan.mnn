using System;
using UnityEngine;

namespace MNN.Unity.Interop
{
    internal static partial class MNNInterop
    {
        [Serializable]
        private sealed class ChatTemplateConfig
        {
            public string prompt_template;
            public JinjaTemplate jinja;
        }

        [Serializable]
        private sealed class JinjaTemplate
        {
            public string chat_template, eos;
        }

        [Serializable]
        private sealed class JinjaOverride
        {
            public JinjaTemplate jinja;
        }

        private static void ConfigureLegacyChatTemplate(IntPtr model)
        {
            string options = LegacyChatTemplateOptions(TakeString(DumpConfig(model)));
            if (options != null)
                Configure(model, options);
        }

        // The pinned tokenizer concatenates message content when its Jinja
        // template is empty. Only adapt these exact legacy ChatML formats.
        // The user-only format retains its declared absence of a default system
        // message; the Smol profile retains its original default system text.
        internal static string LegacyChatTemplateOptions(string configuration)
        {
            var config = JsonUtility.FromJson<ChatTemplateConfig>(configuration);
            if (config == null || !string.IsNullOrEmpty(config.jinja?.chat_template))
                return null;

            const string userOnly = "<|im_start|>user\n%s<|im_end|>\n<|im_start|>assistant\n";
            const string smolSystem = "You are a helpful AI assistant named SmolLM, trained by Hugging Face";
            const string smol = "<|im_start|>system\n" + smolSystem + "<|im_end|>\n" + userOnly;
            string prefix;
            if (config.prompt_template == smol)
            {
                prefix = "{% if messages[0]['role'] != 'system' %}{{ '<|im_start|>system\\n" + smolSystem + "<|im_end|>\\n' }}{% endif %}";
            }
            else if (config.prompt_template == userOnly)
            {
                prefix = string.Empty;
            }
            else
            {
                return null;
            }

            string template = prefix + "{% for message in messages %}{{ '<|im_start|>' + message['role'] + '\\n' + message['content'] + '<|im_end|>\\n' }}{% endfor %}{% if add_generation_prompt %}{{ '<|im_start|>assistant\\n' }}{% endif %}";
            return JsonUtility.ToJson(new JinjaOverride
            {
                jinja = new JinjaTemplate { chat_template = template, eos = "<|im_end|>" }
            });
        }
    }
}
