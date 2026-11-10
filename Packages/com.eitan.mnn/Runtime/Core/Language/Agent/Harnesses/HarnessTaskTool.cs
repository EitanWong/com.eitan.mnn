using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    public enum HarnessTaskToolOperation { Submit, Status, Cancel, Update }

    /// <summary>Plain C# model-tool operations. A supplied durable submitter owns admission identity and recovery.</summary>
    public sealed class HarnessTaskTool
    {
        private readonly RemoteTaskClient _client;
        private readonly string _unavailableMessage;
        private readonly Func<SubmitTaskRequest, CancellationToken, Task<HarnessTask>> _submit;
        private readonly string _conversation, _harness, _workspace;
        private readonly HarnessTaskToolOperation _operation;
        public string Name => _operation == HarnessTaskToolOperation.Submit ? "spawn_thinking" :
            _operation == HarnessTaskToolOperation.Status ? "get_agent_task_status" :
            _operation == HarnessTaskToolOperation.Update ? "update_agent_task" : "cancel_agent_task";
        public bool RequiresNetwork { get; }
        public bool CanUseNetwork => _client != null;
        public string Description => _operation == HarnessTaskToolOperation.Submit ?
            "Submit sustained execution, project/file changes, investigation or research reports to the background host. Ordinary questions and short text answers stay in the foreground. Input is a self-contained objective with context and acceptance criteria. Returns host admission, not completion; reply promptly and keep chatting. Never poll or wait for completion after admission." :
            _operation == HarnessTaskToolOperation.Status ?
            "Read current task status. Input is an exact system-returned task ID, or empty to list recent work. Never create work to check progress." :
            _operation == HarnessTaskToolOperation.Update ?
            "Adjust an existing running task. Input JSON requires TaskId, RequestId (stable instruction identity) and Text. Optional Attachments use bounded Kind/Payload/MediaType/FileName values. Reuse RequestId with identical Text and Attachments after uncertain delivery; never claim delivery from admission alone." :
            "Cancel a task the user explicitly asked to stop. Input must be an exact system-returned task ID. Wait for confirmed backend cancellation.";
        public HarnessTaskTool(RemoteTaskClient client, Func<SubmitTaskRequest, CancellationToken, Task<HarnessTask>> submit, string conversation, HarnessTaskToolOperation operation,
            string harness, string workspace, bool network, string unavailableMessage = null)
        {
            if (!Enum.IsDefined(typeof(HarnessTaskToolOperation), operation)) throw new ArgumentOutOfRangeException(nameof(operation));
            if (client != null && operation == HarnessTaskToolOperation.Submit)
            {
                if (submit == null) throw new ArgumentNullException(nameof(submit));
                HarnessText.Required(conversation, 128); HarnessText.Required(harness, 128); HarnessText.Required(workspace, 1024);
            }
            _client = client; _submit = submit; _conversation = conversation; _operation = operation;
            _harness = harness; _workspace = workspace; RequiresNetwork = network;
            _unavailableMessage = unavailableMessage ?? "Connect a background task host before using background work.";
        }
        public async Task<string> ExecuteAsync(string input, CancellationToken cancellation)
        {
            if (_client == null) return _unavailableMessage;
            if (_operation == HarnessTaskToolOperation.Submit) return await SubmitAsync(input, cancellation).ConfigureAwait(false);
            if (_operation == HarnessTaskToolOperation.Update) return await UpdateAsync(input, cancellation).ConfigureAwait(false);
            if (_operation == HarnessTaskToolOperation.Cancel)
                return Summary(await _client.CancelAsync(input, cancellation).ConfigureAwait(false));
            if (!string.IsNullOrWhiteSpace(input)) return Summary(await _client.GetAsync(input, cancellation).ConfigureAwait(false));
            var tasks = await _client.ListAsync(cancellation).ConfigureAwait(false);
            return tasks.Length == 0 ? "No tasks." : string.Join("\n", tasks.Reverse().Take(10).Select(Summary));
        }

        private async Task<string> SubmitAsync(string input, CancellationToken cancellation)
        {
            string invocation = Guid.NewGuid().ToString("N");
            using (var admission = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                admission.CancelAfter(TimeSpan.FromSeconds(5));
                try
                {
                    var task = await _submit(new SubmitTaskRequest { ConversationId = _conversation,
                        HarnessId = _harness, WorkspaceKey = _workspace, Objective = input,
                        SubmissionKey = invocation }, admission.Token).ConfigureAwait(false);
                    cancellation.ThrowIfCancellationRequested();
                    if (task == null || string.IsNullOrWhiteSpace(task.Id) || task.SubmissionKey != invocation || task.ConversationId != _conversation)
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
            var task = await _client.UpdateAsync(request.TaskId, new TaskControlRequest {
                RequestId = request.RequestId, Text = request.Text, Attachments = request.Attachments, AdmitOnly = true }, cancellation).ConfigureAwait(false);
            var receipt = task.Instructions?.FirstOrDefault(i => i.Id == request.RequestId);
            return "Instruction " + request.RequestId + ": " + (receipt == null ? "delivery unconfirmed; check task status" : receipt.State + ". " + receipt.Detail);
        }
        private static string Summary(HarnessTask task)
        {
            string text = task.Result ?? task.Activity ?? task.Objective;
            if (text.Length > 500) text = text.Substring(0, 500);
            return task.Id + " | " + task.State + " | " + task.WaitingFor + " | " + text;
        }
    }
}
