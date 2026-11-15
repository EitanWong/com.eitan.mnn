using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>An immutable frontend conversation generation; returning to a conversation creates a new scope.</summary>
    public sealed class HarnessFrontendScope
    {
        public string ConversationId { get; }
        internal HarnessFrontendScope(string id) { ConversationId = id; }
    }

    /// <summary>Thread-safe application boundary for conversation identity and explicit input provenance.
    /// A draft has no scope and cannot submit or claim work. Binding never cancels admitted work.</summary>
    public sealed class HarnessFrontendSession
    {
        private readonly object _sync = new object();
        private HarnessFrontendScope _scope;
        private string _inputId, _context;
        private HarnessContentPart[] _attachments = new HarnessContentPart[0];
        public HarnessFrontendScope Scope { get { lock (_sync) return _scope; } }
        public void BindConversation(string conversationId)
        {
            if (conversationId != null) HarnessText.Required(conversationId, 128);
            lock (_sync)
            {
                if (_scope?.ConversationId == conversationId) return;
                _scope = conversationId == null ? null : new HarnessFrontendScope(conversationId);
                _inputId = null; _context = null; _attachments = new HarnessContentPart[0];
            }
        }
        public void BindInput(string inputId, string context = null, HarnessContentPart[] attachments = null)
        {
            HarnessText.Required(inputId, 256);
            if ((context?.Length ?? 0) > 4000) throw new ArgumentException("Frontend context exceeds 4000 characters.");
            var content = HarnessContentPart.CheckedInputs(attachments);
            lock (_sync)
            {
                if (_scope == null) throw new InvalidOperationException("Select a committed conversation before binding input.");
                if (_inputId == inputId && (_context != context || !HarnessContentPart.SameInputs(_attachments, content)))
                    throw new InvalidOperationException("Input identity already belongs to different context or attachments.");
                _inputId = inputId; _context = context; _attachments = content;
            }
        }
        /// <summary>Preserves exact current input separately from the model-written objective.
        /// Recent context includes whole messages only and explicitly records budget omissions.</summary>
        public void BindInputSnapshot(string inputId, string currentInput, string[] recentMessages,
            HarnessContentPart[] attachments = null)
        {
            HarnessText.Required(currentInput, 65536);
            if ((recentMessages?.Length ?? 0) > 128) throw new ArgumentException("Too many frontend context messages.");
            var selected = new List<string>();
            int remaining = 3800;
            var messages = recentMessages ?? new string[0];
            for (int i = messages.Length - 1; i >= 0; --i)
            {
                var message = messages[i] ?? "";
                if (message.Length + 1 > remaining) break;
                selected.Add(message); remaining -= message.Length + 1;
            }
            selected.Reverse();
            string context = (selected.Count < messages.Length ? "[Earlier context omitted due to budget; clarify missing requirements.]\n" : "") + string.Join("\n", selected);
            var parts = new List<HarnessContentPart> { new HarnessContentPart(HarnessContentKind.Text, currentInput, "text/plain", "foreground-input.txt") };
            parts.AddRange(attachments ?? new HarnessContentPart[0]);
            BindInput(inputId, context, parts.ToArray());
        }
        public SubmitTaskRequest CreateSubmission(string objective, string harnessId, string workspaceKey)
        {
            HarnessText.Required(objective, 32768);
            lock (_sync)
            {
                if (_scope == null) throw new InvalidOperationException("Select a committed conversation before submitting work.");
                string identity = _inputId == null ? Guid.NewGuid().ToString("N") : _inputId;
                string key;
                using (var sha = SHA256.Create()) key = BitConverter.ToString(sha.ComputeHash(
                    Encoding.UTF8.GetBytes(_scope.ConversationId + "\n" + identity))).Replace("-", "").ToLowerInvariant();
                string text = string.IsNullOrEmpty(_context) ? objective :
                    "<conversation_context>\n" + _context + "\n</conversation_context>\n<objective>\n" + objective + "\n</objective>";
                return new SubmitTaskRequest { ConversationId = _scope.ConversationId, SubmissionKey = key,
                    HarnessId = harnessId, WorkspaceKey = workspaceKey, Objective = HarnessText.Required(text, 32768),
                    Attachments = (HarnessContentPart[])_attachments.Clone() };
            }
        }
    }
}
