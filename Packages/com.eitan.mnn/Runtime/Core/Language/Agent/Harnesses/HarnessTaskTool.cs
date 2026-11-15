using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    public enum HarnessTaskToolOperation { Submit, Status, Cancel, Update, Pause, Resume, Respond, Continue }

    /// <summary>Plain C# model-tool operations. A supplied durable submitter owns admission identity and recovery.</summary>
    public sealed class HarnessTaskTool
    {
        private readonly RemoteTaskClient _client;
        private readonly string _unavailableMessage;
        private readonly Func<SubmitTaskRequest, CancellationToken, Task<HarnessTask>> _submit;
        private readonly string _conversation, _harness, _workspace;
        private readonly HarnessFrontendSession _session;
        private readonly HarnessTaskToolOperation _operation;
        public string Name => _operation == HarnessTaskToolOperation.Submit ? "spawn_thinking" :
            _operation == HarnessTaskToolOperation.Respond ? "respond_agent_task" :
            _operation == HarnessTaskToolOperation.Continue ? "continue_agent_task" :
            _operation == HarnessTaskToolOperation.Status ? "get_agent_task_status" :
            _operation == HarnessTaskToolOperation.Update ? "update_agent_task" :
            _operation == HarnessTaskToolOperation.Pause ? "pause_agent_task" :
            _operation == HarnessTaskToolOperation.Resume ? "resume_agent_task" : "cancel_agent_task";
        public bool RequiresNetwork { get; }
        public bool CanUseNetwork => _client != null;
        public string Description => _operation == HarnessTaskToolOperation.Submit ?
            "Submit sustained execution, project/file changes, investigation or research reports to the background host. Ordinary questions and short text answers stay in the foreground. Input is a self-contained objective with context and acceptance criteria. The runtime also supplies the exact foreground input and bounded recent conversation snapshot; do not invent missing requirements. Returns host admission, not completion; reply promptly and keep chatting. Never poll or wait for completion after admission." :
            _operation == HarnessTaskToolOperation.Continue ?
            "Continue completed work in its original native session. Input JSON requires exact TaskId and Text (new objective). Creates a linked new task and preserves the previous result. Requires verified original context; unavailable context is an error, never silently start replacement work. Use update for running work and respond for pending requests." :
            _operation == HarnessTaskToolOperation.Respond ?
            "Answer an actual pending background question or permission request in this conversation. Input JSON requires exact TaskId, RequestId and Text from the current request. Permission requires explicit Allow:true or Allow:false reflecting the user's decision. Never guess request IDs or treat an update as permission. Private fields and external authorization require task details." :
            _operation == HarnessTaskToolOperation.Status ?
            "Read current task status. Input is an exact system-returned task ID, active to list ALL unfinished work, or empty to list all work. Never create work to check progress." :
            _operation == HarnessTaskToolOperation.Update ?
            "Adjust an existing running task. Input JSON requires TaskId, RequestId (stable instruction identity) and Text. Optional Attachments use bounded Kind/Payload/MediaType/FileName values. Reuse RequestId with identical Text and Attachments after uncertain delivery; never claim delivery from admission alone." :
            _operation == HarnessTaskToolOperation.Pause ?
            "Pause a queued task the user explicitly asked to hold. Input is an exact system-returned task ID. Running tasks cannot be suspended by this host; never cancel and replay them as pause." :
            _operation == HarnessTaskToolOperation.Resume ?
            "Resume a paused task without creating a replacement. Input is an exact system-returned task ID. Finished tasks cannot be resumed." :
            "Cancel a task the user explicitly asked to stop. Input must be an exact system-returned task ID. Returns promptly with host state. Cancelling means confirmation is pending, never claim the task stopped until Cancelled.";
        public HarnessTaskTool(RemoteTaskClient client, Func<SubmitTaskRequest, CancellationToken, Task<HarnessTask>> submit, string conversation, HarnessTaskToolOperation operation,
            string harness, string workspace, bool network, string unavailableMessage = null)
        {
            if (!Enum.IsDefined(typeof(HarnessTaskToolOperation), operation)) throw new ArgumentOutOfRangeException(nameof(operation));
            if (client != null && (operation == HarnessTaskToolOperation.Submit || operation == HarnessTaskToolOperation.Continue))
            {
                if (submit == null) throw new ArgumentNullException(nameof(submit));
                HarnessText.Required(conversation, 128); HarnessText.Required(harness, 128); HarnessText.Required(workspace, 1024);
            }
            _client = client; _submit = submit; _conversation = conversation; _operation = operation;
            _harness = harness; _workspace = workspace; RequiresNetwork = network;
            _unavailableMessage = unavailableMessage ?? "Connect a background task host before using background work.";
        }
        public HarnessTaskTool(RemoteTaskClient client, Func<SubmitTaskRequest, CancellationToken, Task<HarnessTask>> submit,
            HarnessFrontendSession session, HarnessTaskToolOperation operation, string harness, string workspace,
            bool network, string unavailableMessage = null)
            : this(client, submit, "frontend", operation, harness, workspace, network, unavailableMessage)
        { _session = session ?? throw new ArgumentNullException(nameof(session)); }
        public async Task<string> ExecuteAsync(string input, CancellationToken cancellation)
        {
            if (_client == null) return _unavailableMessage;
            if (_operation == HarnessTaskToolOperation.Respond)
            {
                var scope = _session?.Scope;
                var reply = TaskHostJson.Read<HarnessTaskReply>(Encoding.UTF8.GetBytes(input ?? ""));
                if (reply == null) throw new ArgumentException("A task reply is required.");
                var pending = await _client.GetAsync(reply.TaskId, cancellation).ConfigureAwait(false);
                if (_session != null && (scope == null || !ReferenceEquals(scope, _session.Scope)))
                    throw new InvalidOperationException("The foreground conversation changed while reading this request.");
                var response = reply.CreateRequest(pending, _session != null ? scope.ConversationId : _conversation);
                return Summary(await _client.RespondAsync(reply.TaskId, response, cancellation).ConfigureAwait(false));
            }
            if (_operation == HarnessTaskToolOperation.Continue)
            {
                var continuation = TaskHostJson.Read<TaskUpdateRequest>(Encoding.UTF8.GetBytes(input ?? ""));
                if (continuation == null || string.IsNullOrWhiteSpace(continuation.TaskId) || string.IsNullOrWhiteSpace(continuation.Text))
                    throw new ArgumentException("TaskId and Text are required for continuation.");
                var original = await ReadCurrentTaskAsync(continuation.TaskId, cancellation).ConfigureAwait(false);
                if (original.State != HarnessTaskState.Completed) throw new InvalidOperationException("Use the controls for this active or unfinished task.");
                return await SubmitAsync(continuation.Text, cancellation, original.Id).ConfigureAwait(false);
            }
            if (_operation == HarnessTaskToolOperation.Submit) return await SubmitAsync(input, cancellation).ConfigureAwait(false);
            if (_operation == HarnessTaskToolOperation.Update) return await UpdateAsync(input, cancellation).ConfigureAwait(false);
            if (_session != null && (_operation == HarnessTaskToolOperation.Pause || _operation == HarnessTaskToolOperation.Resume || _operation == HarnessTaskToolOperation.Cancel))
                await ReadCurrentTaskAsync(input, cancellation).ConfigureAwait(false);
            if (_operation == HarnessTaskToolOperation.Pause)
                return Summary(await _client.PauseAsync(input, cancellation).ConfigureAwait(false));
            if (_operation == HarnessTaskToolOperation.Resume)
                return Summary(await _client.ResumeAsync(input, cancellation).ConfigureAwait(false));
            if (_operation == HarnessTaskToolOperation.Cancel)
                return Summary(await _client.RequestCancelAsync(input, cancellation).ConfigureAwait(false));
            bool active = string.Equals(input?.Trim(), "active", StringComparison.OrdinalIgnoreCase);
            if (!active && !string.IsNullOrWhiteSpace(input)) return Summary(await ReadCurrentTaskAsync(input, cancellation).ConfigureAwait(false));
            var tasks = await _client.ListAsync(cancellation).ConfigureAwait(false);
            var selected = tasks.Where(task => (_session == null || task.DeliveryConversationId == _session.Scope?.ConversationId) && (!active || !task.IsTerminal)).OrderBy(task => task.CreatedUtcTicks).ToArray();
            return selected.Length == 0 ? (active ? "No active tasks." : "No tasks.") :
                "Tasks: " + selected.Length + "\n" + string.Join("\n", selected.Select(Summary));
        }

        private async Task<string> SubmitAsync(string input, CancellationToken cancellation, string continuedFromTaskId = null)
        {
            var request = _session != null ? _session.CreateSubmission(input, _harness, _workspace) :
                new SubmitTaskRequest { ConversationId = _conversation, HarnessId = _harness, WorkspaceKey = _workspace,
                    Objective = input, SubmissionKey = Guid.NewGuid().ToString("N") };
            request.ContinuedFromTaskId = continuedFromTaskId;
            string invocation = request.SubmissionKey;
            using (var admission = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                admission.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    var task = await _submit(request, admission.Token).ConfigureAwait(false);
                    cancellation.ThrowIfCancellationRequested();
                    if (task == null || string.IsNullOrWhiteSpace(task.Id) || task.SubmissionKey != invocation || task.ConversationId != request.ConversationId)
                        return "Admission is unconfirmed. Operation " + invocation + ". Check task status; do not submit a replacement.";
                    return "Task accepted: " + task.Id + ". State: " + task.State + ". Work continues independently of this conversation.";
                }
                catch (PendingSubmissionException) when (!cancellation.IsCancellationRequested)
                { return "Admission is unconfirmed. Operation " + invocation + " will retry from the local submission journal on reconnect. Check task status; do not submit a replacement."; }
                catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
                { return "Admission is unconfirmed. Operation " + invocation + ". The host did not confirm within 5 seconds. Check task status; do not submit a replacement."; }
            }
        }

        private async Task<string> UpdateAsync(string input, CancellationToken cancellation)
        {
            var request = TaskHostJson.Read<TaskUpdateRequest>(Encoding.UTF8.GetBytes(input ?? ""));
            if (request == null || string.IsNullOrWhiteSpace(request.TaskId) || string.IsNullOrWhiteSpace(request.RequestId) || string.IsNullOrWhiteSpace(request.Text))
                throw new ArgumentException("TaskId, RequestId and Text are required.");
            if (_session != null) await ReadCurrentTaskAsync(request.TaskId, cancellation).ConfigureAwait(false);
            var task = await _client.UpdateAsync(request.TaskId, new TaskControlRequest {
                RequestId = request.RequestId, Text = request.Text, Attachments = request.Attachments, AdmitOnly = true }, cancellation).ConfigureAwait(false);
            var receipt = task.Instructions?.FirstOrDefault(i => i.Id == request.RequestId);
            return "Instruction " + request.RequestId + ": " + (receipt == null ? "delivery unconfirmed; check task status" : receipt.State + ". " + receipt.Detail);
        }
        private async Task<HarnessTask> ReadCurrentTaskAsync(string id, CancellationToken token)
        {
            var scope = _session?.Scope;
            var task = await _client.GetAsync(id, token).ConfigureAwait(false);
            if (_session != null && (scope == null || !ReferenceEquals(scope, _session.Scope) || task.DeliveryConversationId != scope.ConversationId))
                throw new InvalidOperationException("This task does not belong to the current foreground conversation.");
            return task;
        }
        private static string Summary(HarnessTask task)
        {
            string text = HarnessExecutionStep.Clip(task.Result ?? task.Activity ?? task.Objective, 500);
            string objective = HarnessExecutionStep.Clip(task.Objective, 200);
            return task.Id + " | " + task.State + " | " + task.WaitingFor + " | " + objective + " | " + text +
                (task.WaitingFor == HarnessWaitingFor.None ? "" : " | RequestId: " + task.RequestId);
        }
    }
}
