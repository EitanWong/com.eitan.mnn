using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>A snapshot of voice IDs accepted by one installed speech export. Does not load native models.</summary>
    public sealed class MNNSpeechVoiceOptions
    {
        private readonly string[] _voiceIds;
        public MNNSpeechModelKind ModelKind { get; }
        public bool RequiresReferenceAudio { get; }
        /// <summary>Preset IDs to pass unchanged to MNNSpeechSynthesisOptions.Voice. Reference audio is not a preset.</summary>
        public string[] VoiceIds => (string[])_voiceIds.Clone();

        internal MNNSpeechVoiceOptions(MNNSpeechModelKind kind, bool referenceAudio, string[] voiceIds)
        {
            ModelKind = kind;
            RequiresReferenceAudio = referenceAudio;
            if (voiceIds == null || Array.Exists(voiceIds, string.IsNullOrWhiteSpace))
                throw new InvalidDataException("Speech voice metadata must contain nonempty string IDs.");
            _voiceIds = voiceIds.Distinct(StringComparer.Ordinal).ToArray();
        }

        /// <summary>Empty means model default (or the supplied reference recording). IDs use ordinal matching.</summary>
        public bool SupportsVoice(string voice) => string.IsNullOrWhiteSpace(voice) || Array.IndexOf(_voiceIds, voice) >= 0;

        /// <summary>Validate a selection before native loading. Returns null for the model's own default.</summary>
        public string ResolveVoice(string voice)
        {
            if (!SupportsVoice(voice))
                throw new ArgumentException("Select a voice supported by the installed speech model.", nameof(MNNSpeechSynthesisOptions.Voice));
            return string.IsNullOrWhiteSpace(voice) ? null : voice;
        }
    }
}
