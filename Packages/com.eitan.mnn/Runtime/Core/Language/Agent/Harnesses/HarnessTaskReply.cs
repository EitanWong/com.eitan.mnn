using System;
using System.Linq;
using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Explicit frontend reply. Missing permission decisions never become approvals.</summary>
    [DataContract]
    public sealed class HarnessTaskReply
    {
        [DataMember] public string TaskId;
        [DataMember] public string RequestId;
        [DataMember] public string Text;
        [DataMember(EmitDefaultValue = false)] public bool? Allow;
        public TaskControlRequest CreateRequest(HarnessTask task, string conversationId)
        {
            if (task == null || task.Id != TaskId || task.DeliveryConversationId != conversationId ||
                task.State != HarnessTaskState.Running || task.WaitingFor == HarnessWaitingFor.None ||
                string.IsNullOrWhiteSpace(RequestId) || task.RequestId != RequestId)
                throw new InvalidOperationException("This task request is no longer current in this conversation.");
            if (task.WaitingFor == HarnessWaitingFor.Permission && !Allow.HasValue)
                throw new ArgumentException("An explicit allow or deny decision is required.");
            if (task.InputForm?.Fields.Any(field => field.Secret) == true || task.InputPrompt?.Mode == HarnessInputMode.Url)
                throw new NotSupportedException("Use the task details for private input or external authorization.");
            return new TaskControlRequest { RequestId = RequestId, Text = HarnessText.Required(Text, 8192), Allow = Allow ?? false };
        }
    }
}
