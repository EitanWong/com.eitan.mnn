using System;
using System.Linq;
using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    public enum HarnessInputMode { Text, Url }

    /// <summary>Immutable pending text or external URL handoff. Completion is a backend report, not proof of authentication.</summary>
    [DataContract]
    public sealed class HarnessInputPrompt
    {
        [DataMember(Order = 0)] public HarnessInputMode Mode { get; private set; }
        [DataMember(Order = 1, EmitDefaultValue = false)] public string Url { get; private set; }
        [DataMember(Order = 2, EmitDefaultValue = false)] public string NativeId { get; private set; }
        [DataMember(Order = 3)] public bool BackendCompleted { get; private set; }
        public HarnessInputPrompt(HarnessInputMode mode, string url = null, string nativeId = null, bool backendCompleted = false)
        { Mode = mode; Url = url; NativeId = nativeId; BackendCompleted = backendCompleted; Validate(); }
        internal HarnessInputPrompt WithCompletion() => new HarnessInputPrompt(Mode, Url, NativeId, true);
        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(HarnessInputMode), Mode)) throw new SerializationException("Unknown input mode.");
            if (Mode == HarnessInputMode.Text)
            {
                if (Url != null || NativeId != null || BackendCompleted) throw new SerializationException("Text input cannot carry an external flow.");
                return;
            }
            HarnessText.Required(NativeId, 256);
            Uri uri;
            if (Url == null || Url.Length > 4096 || Url.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)) ||
                !Uri.TryCreate(Url, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http") ||
                string.IsNullOrEmpty(uri.Host) || uri.UserInfo.Length != 0)
                throw new SerializationException("Input URL must be a bounded HTTP or HTTPS address without credentials.");
        }
        internal static string ReplyText(string text)
        {
            if (text == null || text.Length > 8192) throw new ArgumentException("Input reply exceeds its text budget.");
            return text;
        }
    }
}
