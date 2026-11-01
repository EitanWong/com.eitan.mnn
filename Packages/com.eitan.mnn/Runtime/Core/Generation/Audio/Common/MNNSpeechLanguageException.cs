using System;

namespace MNN.Unity
{
    /// <summary>A recoverable request failure: no installed speech model can pronounce this input.</summary>
    public sealed class MNNSpeechLanguageException : NotSupportedException
    {
        public MNNSpeechLanguageException(string message) : base(message) { }
    }
}
