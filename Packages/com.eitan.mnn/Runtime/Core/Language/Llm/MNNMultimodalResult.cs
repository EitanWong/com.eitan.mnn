using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MNN.Unity
{
    public sealed class MNNMultimodalResult
    {
        public string Text { get; }

        public int GeneratedTokens { get; }

        /// <summary>Owned snapshot of the official LlmContext output token IDs.</summary>
        public IReadOnlyList<int> GeneratedTokenIds { get; }

        public bool ReachedTokenLimit { get; }

        public bool Cancelled { get; }

        /// <summary>Owned mono PCM samples, independent of the next inference call.</summary>
        public float[] Waveform { get; }

        public int SampleRate { get; }

        public long VisionMicroseconds { get; }

        public long AudioMicroseconds { get; }

        public float InputAudioSeconds { get; }

        /// <summary>Wall-clock time spent generating response text, in microseconds.</summary>
        public long TextGenerationMicroseconds { get; }
        public int PromptTokens { get; }
        /// <summary>Tokens reused from the previous exact conversation prefix.</summary>
        public int ReusedPromptTokens { get; }
        /// <summary>Full rendered prompt size, including reused tokens. PromptTokens is the newly computed suffix.</summary>
        public int TotalPromptTokens { get; }
        public MNNConversationCacheStatus ConversationCacheStatus { get; }
        public long PrefillMicroseconds { get; }
        public long DecodeMicroseconds { get; }
        public long SampleMicroseconds { get; }

        /// <summary>Generation start to the first nonempty streaming text callback.
        /// Zero when streaming produced no text; excludes async queueing and waiting for the model lock.</summary>
        public long FirstTextMicroseconds { get; }

        /// <summary>Total wall time inside the caller's synchronous progress callbacks.
        /// This time pauses native decoding and includes any downstream waits in those callbacks.</summary>
        public long ProgressCallbackMicroseconds { get; }

        /// <summary>Wall-clock time spent converting generated speech tokens to waveform, in microseconds.</summary>
        public long WaveformGenerationMicroseconds { get; }

        internal MNNMultimodalResult(string text, int tokens, bool limited, float[] waveform, int rate, long visionUs, long audioUs, float audioSeconds, long textGenerationUs = 0, long waveformGenerationUs = 0, bool cancelled = false, int[] tokenIds = null, int promptTokens = 0, long prefillUs = 0, long decodeUs = 0, long sampleUs = 0, long firstTextUs = 0, long progressCallbackUs = 0, int reusedPromptTokens = 0, int totalPromptTokens = 0, MNNConversationCacheStatus cacheStatus = MNNConversationCacheStatus.Cold)
        {
            Text = text;
            GeneratedTokens = tokens;
            GeneratedTokenIds = new ReadOnlyCollection<int>(tokenIds == null ? Array.Empty<int>() : (int[])tokenIds.Clone());
            ReachedTokenLimit = limited;
            Waveform = waveform;
            SampleRate = rate;
            VisionMicroseconds = visionUs;
            AudioMicroseconds = audioUs;
            InputAudioSeconds = audioSeconds;
            TextGenerationMicroseconds = textGenerationUs;
            PromptTokens = promptTokens;
            ReusedPromptTokens = reusedPromptTokens;
            TotalPromptTokens = totalPromptTokens > 0 ? totalPromptTokens : promptTokens;
            ConversationCacheStatus = cacheStatus;
            PrefillMicroseconds = prefillUs;
            DecodeMicroseconds = decodeUs;
            SampleMicroseconds = sampleUs;
            FirstTextMicroseconds = firstTextUs;
            ProgressCallbackMicroseconds = progressCallbackUs;
            WaveformGenerationMicroseconds = waveformGenerationUs;
            Cancelled = cancelled;
        }
    }
}
