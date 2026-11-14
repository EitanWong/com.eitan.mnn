using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    [DataContract] public sealed class HarnessSkillTarget
    {
        [DataMember] public string Backend;
        [DataMember] public string Installer;
    }
    [DataContract] public sealed class HarnessSkillCatalog
    {
        [DataMember] public bool Enabled;
        [DataMember] public string Package;
        [DataMember] public HarnessSkillTarget[] Targets;
        [DataMember] public string[] DefaultTargets;
    }
    /// <summary>Explicit host-wide skill management. Never an executable command line.</summary>
    [DataContract] public sealed class HarnessSkillRequest
    {
        [DataMember] public string SubmissionKey;
        [DataMember] public string Operation;
        [DataMember] public string Source;
        [DataMember] public string[] Names;
        [DataMember] public string[] Backends;
        [OnDeserialized] private void OnDeserialized(StreamingContext context) => Validate();
        public void Validate()
        {
            HarnessText.Required(SubmissionKey, 256);
            if (SubmissionKey.Any(char.IsControl)) throw new ArgumentException("Invalid skill invocation identity.");
            if (Array.IndexOf(new[] { "source-list", "install", "list", "remove", "update" }, Operation) < 0)
                throw new ArgumentException("Unknown skill operation.");
            Names = Names ?? new string[0]; Backends = Backends ?? new string[0];
            if (Names.Length > 64 || Backends.Length > 20) throw new ArgumentException("Too many skill names or backends.");
            foreach (string name in Names.Concat(Backends)) Argument(name, 128);
            if (Operation == "source-list" || Operation == "install") Argument(Source, 4096);
            else if (!string.IsNullOrEmpty(Source)) throw new ArgumentException("This skill operation has no source.");
            if (Operation == "install" ? Names.Length == 0 : Operation == "remove" ? Names.Length != 1 : Names.Length != 0)
                throw new ArgumentException("Select exact skill names for install or one name for remove.");
            if (Operation != "install" && Backends.Length != 0) throw new ArgumentException("Backend targets apply only to installation.");
        }
        private static void Argument(string value, int maximum)
        {
            HarnessText.Required(value, maximum);
            if (value[0] == '-' || value.Any(char.IsControl)) throw new ArgumentException("Skill arguments cannot be options or contain control characters.");
        }
    }
    /// <summary>Separate durable admission journal for management; never replays to the user-task route.</summary>
    public sealed class HarnessSkillClient : IDisposable
    {
        private readonly RemoteTaskClient _remote;
        private readonly SubmissionJournal _journal;
        public int PendingCount => _journal.PendingCount;
        public HarnessSkillClient(RemoteTaskClient remote, string journalDirectory)
        { _remote = remote ?? throw new ArgumentNullException(nameof(remote)); _journal = new SubmissionJournal(journalDirectory); }
        public Task<HarnessTask> SubmitAsync(HarnessSkillRequest request, CancellationToken cancellation)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            request.Validate();
            var envelope = new SubmitTaskRequest { ConversationId = "skill-library", SubmissionKey = request.SubmissionKey,
                HarnessId = "skills", WorkspaceKey = "host", Objective = Encoding.UTF8.GetString(TaskHostJson.Write(request)) };
            return _journal.SubmitAsync(envelope, SendAsync, cancellation);
        }
        private Task<HarnessTask> SendAsync(SubmitTaskRequest envelope, CancellationToken cancellation) =>
            _remote.SubmitSkillOperationAsync(TaskHostJson.Read<HarnessSkillRequest>(Encoding.UTF8.GetBytes(envelope.Objective)), cancellation);
        public Task RetryAsync(CancellationToken cancellation) => _journal.ReplayAsync(SendAsync, cancellation);
        public void Dispose() => _journal.Dispose();
    }
}
