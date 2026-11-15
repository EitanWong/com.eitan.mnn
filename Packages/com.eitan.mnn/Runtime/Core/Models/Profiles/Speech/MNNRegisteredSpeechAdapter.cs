using System;

namespace MNN.Unity
{
    internal sealed class MNNRegisteredSpeechAdapter
    {
        internal MNNSpeechModelKind Kind { get; }
        internal string Marker { get; }
        internal Func<string, string[]> Voices { get; }
        internal Func<string, MNNSpeechSynthesisOptions, int, MNNBackendType, MNNSpeechSynthesizer> Load { get; }
        internal MNNRegisteredSpeechAdapter(MNNSpeechModelKind kind, string marker, Func<string, string[]> voices,
            Func<string, MNNSpeechSynthesisOptions, int, MNNBackendType, MNNSpeechSynthesizer> load)
        { Kind = kind; Marker = marker; Voices = voices; Load = load; }
    }
}
