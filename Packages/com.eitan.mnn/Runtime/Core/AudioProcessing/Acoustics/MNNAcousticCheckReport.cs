using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MNN.Unity
{
    public enum MNNAcousticCheckSeverity { Information, Warning, Error }

    /// <summary>Stable code for applications; message, path and recovery action for developers.</summary>
    public sealed class MNNAcousticCheckEntry
    {
        public string Code { get; }
        public string Stage { get; }
        public MNNAcousticCheckSeverity Severity { get; }
        public string Message { get; }
        public string Path { get; }
        public string Action { get; }

        internal MNNAcousticCheckEntry(string code, string stage, MNNAcousticCheckSeverity severity,
            string message, string path = null, string action = null)
        {
            Code = code; Stage = stage; Severity = severity; Message = message; Path = path; Action = action;
        }
    }

    /// <summary>Immutable file-check snapshot. A successful check permits a startup attempt, not inference certification.</summary>
    public sealed class MNNAcousticCheckReport
    {
        public IReadOnlyList<MNNAcousticCheckEntry> Entries { get; }
        public bool CanAttemptPreparation { get; }

        internal MNNAcousticCheckReport(IEnumerable<MNNAcousticCheckEntry> entries)
        {
            var snapshot = entries.ToArray();
            Entries = Array.AsReadOnly(snapshot);
            CanAttemptPreparation = snapshot.All(entry => entry.Severity != MNNAcousticCheckSeverity.Error);
        }

        public override string ToString() => Format();

        /// <summary>Optional translator receives complete display strings. Codes and filesystem paths stay unchanged.</summary>
        public string Format(Func<string, string> translate = null)
        {
            translate = translate ?? (text => text);
            var result = new StringBuilder(translate("Acoustic file preflight")).AppendLine();
            result.AppendLine(translate(CanAttemptPreparation ? "No blocking file issues found." : "Resolve the reported issues before starting."));
            result.AppendLine(translate("This is a snapshot. Run the check again after editing settings or model files."));
            foreach (var entry in Entries)
            {
                result.Append('[').Append(entry.Code).Append("] ").Append(translate(entry.Stage)).Append(": ").AppendLine(translate(entry.Message));
                if (!string.IsNullOrEmpty(entry.Path)) result.AppendLine(entry.Path);
                if (!string.IsNullOrEmpty(entry.Action)) result.AppendLine(translate(entry.Action));
            }
            return result.Append(translate("Startup still validates model integrity and compatibility. This check does not run inference.")).ToString();
        }
    }
}
