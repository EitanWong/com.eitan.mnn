using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Main-thread presentation state. Scope generations fence late network responses after a host/account switch.</summary>
    public sealed class HarnessTaskDashboard
    {
        private readonly Dictionary<string, HarnessTask> _tasks = new Dictionary<string, HarnessTask>(StringComparer.Ordinal);
        public long Generation { get; private set; }
        public HarnessTask[] Tasks => _tasks.Values.OrderByDescending(p => p.CreatedUtcTicks).ThenBy(p => p.Id).Select(p => p.Copy()).ToArray();
        public long ResetScope() { _tasks.Clear(); return ++Generation; }
        public bool Apply(long generation, HarnessTask[] tasks)
        {
            if (generation != Generation) return false;
            if (tasks == null) throw new ArgumentNullException(nameof(tasks));
            bool changed = false;
            foreach (var task in tasks)
            {
                task.Validate();
                HarnessTask previous;
                if (!_tasks.TryGetValue(task.Id, out previous) || task.Revision > previous.Revision)
                { _tasks[task.Id] = task.Copy(); changed = true; }
            }
            return changed;
        }
        public string InputUrl(long generation, string taskId, string requestId)
        {
            CreateReply(generation, taskId, requestId, "", false);
            var prompt = _tasks[taskId].InputPrompt;
            if (prompt == null || prompt.Mode != HarnessInputMode.Url) throw new InvalidOperationException("This request has no external link.");
            prompt.Validate(); return prompt.Url;
        }
        public TaskControlRequest CreateReply(long generation, string taskId, string requestId, string text, bool allow,
            HarnessInputAction action = HarnessInputAction.Answer)
        {
            HarnessTask task;
            if (generation != Generation || !_tasks.TryGetValue(taskId, out task) || task.State != HarnessTaskState.Running ||
                task.WaitingFor == HarnessWaitingFor.None || string.IsNullOrEmpty(requestId) || task.RequestId != requestId)
                throw new InvalidOperationException("This task input or permission request is no longer current. Refresh the task before replying.");
            if (!Enum.IsDefined(typeof(HarnessInputAction), action) || action != HarnessInputAction.Answer && task.WaitingFor != HarnessWaitingFor.Input ||
                action == HarnessInputAction.Decline && task.InputForm == null && task.InputPrompt == null) throw new ArgumentException("Input action is not available for this request.");
            return new TaskControlRequest { RequestId = requestId, Text = task.InputPrompt != null ? HarnessInputPrompt.ReplyText(text) : HarnessText.Required(text, 8192), Allow = allow, InputAction = action };
        }
    }
}
