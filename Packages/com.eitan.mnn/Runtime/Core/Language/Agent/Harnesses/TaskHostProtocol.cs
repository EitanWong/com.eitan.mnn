using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MNN.Samples.SiriAI.Harnesses
{
    [DataContract]
    public sealed class SubmitTaskRequest
    {
        [DataMember] public string ConversationId;
        [DataMember] public string SubmissionKey;
        [DataMember] public string HarnessId;
        [DataMember] public string WorkspaceKey;
        [DataMember] public string Objective;
        [DataMember(EmitDefaultValue = false)] public bool IsolatedContext;
        [DataMember(EmitDefaultValue = false)] public HarnessContentPart[] Attachments;
    }
    [DataContract]
    public sealed class TaskControlRequest
    {
        [DataMember] public string RequestId;
        [DataMember] public string Text;
        [DataMember] public bool Allow;
        [DataMember(EmitDefaultValue = false)] public HarnessContentPart[] Attachments;
        [DataMember(EmitDefaultValue = false)] public bool AdmitOnly;
        [DataMember(EmitDefaultValue = false)] public HarnessInputAction InputAction;
    }
    [DataContract]
    public sealed class ResultClaimRequest
    {
        [DataMember] public string ConversationId;
        [DataMember] public string ClientId;
        [DataMember] public int LeaseSeconds = 60;
    }
    [DataContract]
    public sealed class ResultReceiptRequest
    {
        [DataMember] public string LeaseId;
        [DataMember] public ResultReceipt Receipt;
        [DataMember] public int LeaseSeconds = 60;
    }
    [DataContract]
    public sealed class TaskHostError
    {
        [DataMember] public string Code;
        [DataMember] public string Message;
    }

    /// <summary>Versioned host DTO codec shared with Unity; no runtime-generated proxies.</summary>
    public static class TaskHostJson
    {
        public static byte[] Write<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return stream.ToArray();
            }
        }
        public static T Read<T>(byte[] bytes)
        {
            if (bytes == null || bytes.Length > 1024 * 1024) throw new SerializationException("Host message exceeds 1 MiB.");
            using (var stream = new MemoryStream(bytes, false))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
    }
}
