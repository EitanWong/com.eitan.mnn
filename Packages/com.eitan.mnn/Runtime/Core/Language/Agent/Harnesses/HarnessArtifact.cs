using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace MNN.Samples.SiriAI.Harnesses
{
    public enum HarnessContentKind { Text, Url, Json, Base64 }

    /// <summary>A bounded value; URI parts are references and are never automatically downloaded.</summary>
    [DataContract]
    public sealed class HarnessContentPart
    {
        [DataMember(Order = 0)] public HarnessContentKind Kind { get; private set; }
        [DataMember(Order = 1)] public string Payload { get; private set; }
        [DataMember(Order = 2)] public string MediaType { get; private set; }
        [DataMember(Order = 3)] public string FileName { get; private set; }
        public HarnessContentPart(HarnessContentKind kind, string payload, string mediaType = null, string fileName = null)
        { Kind = kind; Payload = payload; MediaType = mediaType; FileName = fileName; Validate(); }
        internal void Validate()
        {
            if (!Enum.IsDefined(typeof(HarnessContentKind), Kind)) throw new ArgumentException("Unknown artifact content kind.");
            if (Payload == null || Payload.Length > 262144) throw new ArgumentException("Expected bounded artifact content.");
            if (Kind == HarnessContentKind.Json) HarnessText.Required(Payload, 262144);
            if ((MediaType?.Length ?? 0) > 256 || (FileName?.Length ?? 0) > 512) throw new ArgumentException("Artifact metadata exceeds its budget.");
            if (Kind == HarnessContentKind.Url)
            {
                Uri uri;
                if (!Uri.TryCreate(Payload, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https") || uri.UserInfo.Length != 0)
                    throw new ArgumentException("Artifact references require HTTP or HTTPS without URL credentials.");
            }
            if (Kind == HarnessContentKind.Base64)
            {
                try { Convert.FromBase64String(Payload); }
                catch (FormatException error) { throw new ArgumentException("Invalid artifact base64.", error); }
            }
            if (Kind == HarnessContentKind.Json)
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(Payload), XmlDictionaryReaderQuotas.Max))
                    while (reader.Read()) { }
        }
        internal int Bytes => Encoding.UTF8.GetByteCount(Payload) + Encoding.UTF8.GetByteCount(MediaType ?? "") + Encoding.UTF8.GetByteCount(FileName ?? "");
        internal static HarnessContentPart[] CheckedInputs(HarnessContentPart[] parts)
        {
            var values = parts?.ToArray() ?? new HarnessContentPart[0];
            if (values.Length > 32) throw new ArgumentException("Too many input attachments.");
            foreach (var value in values) { if (value == null) throw new ArgumentException("Missing input attachment."); value.Validate(); HarnessText.Required(value.Payload, 262144); }
            if (values.Sum(value => value.Bytes) > 262144) throw new ArgumentException("Input attachments exceed the 256 KiB budget.");
            return values;
        }
        internal static bool SameInputs(HarnessContentPart[] left, HarnessContentPart[] right) =>
            TaskHostJson.Write(left ?? new HarnessContentPart[0]).SequenceEqual(TaskHostJson.Write(right ?? new HarnessContentPart[0]));
    }

    [DataContract]
    public sealed class HarnessArtifact
    {
        [DataMember(Order = 0)] public string Id { get; private set; }
        [DataMember(Order = 1)] public string Name { get; private set; }
        [DataMember(Order = 2)] public string Description { get; private set; }
        [DataMember(Name = "Parts", Order = 3)] private HarnessContentPart[] _parts;
        [DataMember(Order = 4, EmitDefaultValue = false)] public string SourceTaskId { get; private set; }
        [DataMember(Order = 5, EmitDefaultValue = false)] public string SourceArtifactId { get; private set; }
        public HarnessContentPart[] Parts => _parts.ToArray();
        public HarnessArtifact(string id, string name, string description, HarnessContentPart[] parts,
            string sourceTaskId = null, string sourceArtifactId = null)
        { Id = id; Name = name; Description = description; _parts = parts?.ToArray(); SourceTaskId = sourceTaskId; SourceArtifactId = sourceArtifactId; Validate(); }
        internal void Validate()
        {
            HarnessText.Required(Id, 160);
            if (SourceTaskId != null || SourceArtifactId != null)
            { HarnessText.Required(SourceTaskId, 128); HarnessText.Required(SourceArtifactId, 160); }
            if ((Name?.Length ?? 0) > 240 || (Description?.Length ?? 0) > 1000 || _parts == null || _parts.Length == 0 || _parts.Length > 64)
                throw new ArgumentException("Invalid or oversized artifact.");
            foreach (var part in _parts) { if (part == null) throw new ArgumentException("Missing artifact part."); part.Validate(); }
        }
        internal int Bytes => _parts.Sum(part => part.Bytes) + Encoding.UTF8.GetByteCount(Name ?? "") + Encoding.UTF8.GetByteCount(Description ?? "") + Encoding.UTF8.GetByteCount(SourceTaskId ?? "") + Encoding.UTF8.GetByteCount(SourceArtifactId ?? "");
        internal static HarnessArtifact[] Checked(HarnessArtifact[] artifacts)
        {
            var values = artifacts?.ToArray() ?? new HarnessArtifact[0];
            if (values.Length > 32) throw new ArgumentException("Too many artifacts.");
            foreach (var value in values) { if (value == null) throw new ArgumentException("Missing artifact."); value.Validate(); }
            if (values.Sum(value => value.Bytes) > 262144) throw new ArgumentException("Artifact payloads exceed the 256 KiB task budget; use file references.");
            return values;
        }
    }
}
