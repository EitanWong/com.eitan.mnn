using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Exact terminal content collected into a parent result, independent of presentation leases.</summary>
    [DataContract]
    public sealed class HarnessCollectedResult
    {
        [DataMember(Order = 0)] public string TaskId { get; private set; }
        [DataMember(Order = 1)] public string ContentHash { get; private set; }
        private HarnessCollectedResult(string taskId, string hash) { TaskId = taskId; ContentHash = hash; Validate(); }
        public static string ConversationFor(string parentTaskId) => "coordination:" + HarnessText.Required(parentTaskId, 128);
        public static HarnessCollectedResult Capture(HarnessTask task)
        {
            if (task == null || !task.IsTerminal) throw new ArgumentException("Only terminal results can be collected.");
            return new HarnessCollectedResult(task.Id, Hash(TaskHostJson.Write(new Content { State = task.State, Text = task.Result, Artifacts = task.Artifacts })));
        }
        internal void Validate()
        {
            HarnessText.Required(TaskId, 128);
            if (ContentHash == null || ContentHash.Length != 64 || ContentHash.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')))
                throw new SerializationException("Invalid collected result hash.");
        }
        internal static HarnessCollectedResult[] Checked(HarnessCollectedResult[] sources)
        {
            var values = sources?.ToArray() ?? new HarnessCollectedResult[0];
            if (values.Length > 32 || values.Any(v => v == null)) throw new ArgumentException("Invalid collected result count.");
            foreach (var value in values) value.Validate();
            if (values.Select(v => v.TaskId).Distinct(StringComparer.Ordinal).Count() != values.Length) throw new ArgumentException("Duplicate collected result.");
            return values;
        }
        public static HarnessArtifact[] MergeArtifacts(HarnessArtifact[] own, HarnessTask[] children) => HarnessArtifact.Checked(
            (own ?? new HarnessArtifact[0]).Concat(children.SelectMany(task => (task.Artifacts ?? new HarnessArtifact[0]).Select((artifact, index) =>
                new HarnessArtifact("collected-" + Hash(Encoding.UTF8.GetBytes(task.Id + "/" + index + "/" + artifact.Id)),
                    artifact.Name, artifact.Description, artifact.Parts, task.Id, artifact.Id)))).ToArray());
        internal static string Hash(byte[] bytes)
        { using (var algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        [DataContract]
        private sealed class Content
        {
            [DataMember(Order = 0)] public HarnessTaskState State;
            [DataMember(Order = 1)] public string Text;
            [DataMember(Order = 2)] public HarnessArtifact[] Artifacts;
        }
    }
}
