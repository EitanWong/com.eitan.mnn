namespace MNN.Unity
{
    public sealed class MNNLlmResult
    {
        public string Text { get; }

        public int GeneratedTokens { get; }

        public bool ReachedTokenLimit { get; }

        /// <summary>Templated input token count reported by official LlmContext.</summary>
        public int PromptTokens { get; }
        public long LoadMicroseconds { get; }
        public long PrefillMicroseconds { get; }
        public long DecodeMicroseconds { get; }
        /// <summary>Native sampling time; separate from the model's decode time.</summary>
        public long SampleMicroseconds { get; }

        internal MNNLlmResult(string text, int tokens, bool limited, int promptTokens = 0, long loadUs = 0, long prefillUs = 0, long decodeUs = 0, long sampleUs = 0)
        {
            Text = text;
            GeneratedTokens = tokens;
            ReachedTokenLimit = limited;
            PromptTokens = promptTokens;
            LoadMicroseconds = loadUs;
            PrefillMicroseconds = prefillUs;
            DecodeMicroseconds = decodeUs;
            SampleMicroseconds = sampleUs;
        }
    }
}
