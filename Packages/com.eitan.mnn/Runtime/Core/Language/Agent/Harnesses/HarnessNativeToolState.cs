using System;
using System.Linq;
using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Latest bounded native tool states. This is neither an execution log nor a task result.</summary>
    [DataContract]
    public sealed class HarnessNativeToolState
    {
        [DataMember(Name = "Calls", Order = 0)] private HarnessNativeToolCall[] _calls;
        [DataMember(Order = 1)] public bool Truncated { get; private set; }
        public HarnessNativeToolCall[] Calls => (HarnessNativeToolCall[])_calls.Clone();
        public HarnessNativeToolState(HarnessNativeToolCall[] calls, bool truncated = false)
        { _calls = calls == null ? new HarnessNativeToolCall[0] : (HarnessNativeToolCall[])calls.Clone(); Truncated = truncated; Validate(); }
        internal HarnessNativeToolState Copy()
        { var copy = (HarnessNativeToolState)MemberwiseClone(); copy._calls = (HarnessNativeToolCall[])_calls.Clone(); return copy; }
        internal void Validate()
        {
            if (_calls == null || _calls.Length > 32 || _calls.Any(c => c == null) ||
                _calls.Select(c => c.ToolCallId).Distinct(StringComparer.Ordinal).Count() != _calls.Length)
                throw new SerializationException("Invalid native tool state identities.");
            foreach (var call in _calls) call.Validate();
            if (TaskHostJson.Write(this).Length > 65536) throw new SerializationException("Native tool state exceeds its wire budget.");
        }
    }
    [DataContract]
    public sealed class HarnessNativeToolCall
    {
        [DataMember(Order = 0)] public string ToolCallId { get; private set; }
        [DataMember(Order = 1, EmitDefaultValue = false)] public string Title { get; private set; }
        [DataMember(Order = 2, EmitDefaultValue = false)] public string Kind { get; private set; }
        [DataMember(Order = 3, EmitDefaultValue = false)] public string Status { get; private set; }
        [DataMember(Order = 4)] public string Payload { get; private set; }
        [DataMember(Name = "OmittedFields", Order = 5)] private string[] _omittedFields;
        public string[] OmittedFields => (string[])_omittedFields.Clone();
        public HarnessNativeToolCall(string toolCallId, string title, string kind, string status, string payload, string[] omittedFields = null)
        {
            ToolCallId = toolCallId; Title = title; Kind = kind; Status = status; Payload = payload;
            _omittedFields = omittedFields == null ? new string[0] : (string[])omittedFields.Clone(); Validate();
        }
        internal void Validate()
        {
            HarnessText.Required(ToolCallId, 4096);
            if (Title?.Length > 1024 || Kind?.Length > 128 || Payload == null || Payload.Length > 32768 ||
                Status != null && Status != "pending" && Status != "in_progress" && Status != "completed" && Status != "failed" ||
                _omittedFields == null || _omittedFields.Length > 16 || _omittedFields.Any(f => string.IsNullOrWhiteSpace(f) || f.Length > 128))
                throw new SerializationException("Invalid native tool snapshot.");
        }
    }
}
