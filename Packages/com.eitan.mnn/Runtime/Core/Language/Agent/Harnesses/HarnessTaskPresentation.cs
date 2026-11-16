using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>UI-independent task status and controls. Keys may be localized by any frontend.
    /// Execution records describe observed work; their count is not a completion percentage.</summary>
    public sealed class HarnessTaskPresentation
    {
        public string Title { get; }
        public string StatusKey { get; }
        public string ProgressText { get; }
        public bool NeedsAttention { get; }
        public bool CanCancel { get; }
        public bool CanPause { get; }
        public bool CanResume { get; }
        public string PauseHintKey { get; }

        public HarnessTaskPresentation(HarnessTask task, HarnessCapabilities capabilities)
        {
            if (task == null) throw new ArgumentNullException(nameof(task));
            NeedsAttention = task.State == HarnessTaskState.Running && task.WaitingFor != HarnessWaitingFor.None;
            Title = string.IsNullOrWhiteSpace(task.Title) ? task.Objective : task.Title;
            StatusKey = NeedsAttention ? (task.WaitingFor == HarnessWaitingFor.Permission ? "Waiting for permission" : "Waiting for input") : task.State.ToString();
            ProgressText = task.Activity ?? (task.IsTerminal ? task.Result : null) ?? "No execution update yet.";
            CanPause = task.State == HarnessTaskState.Queued;
            CanResume = task.State == HarnessTaskState.Paused;
            CanCancel = CanPause || CanResume || task.State == HarnessTaskState.Running && (capabilities & HarnessCapabilities.Cancel) != 0;
            PauseHintKey = task.State == HarnessTaskState.Running
                ? "Running agents cannot be safely paused by this host. Queued tasks can be paused." : null;
        }

        /// <summary>Attention first, then all live work, then finished work. Most recent updates break ties.</summary>
        public static HarnessTask[] Order(IEnumerable<HarnessTask> tasks) => (tasks ?? throw new ArgumentNullException(nameof(tasks)))
            .OrderBy(task => task.State == HarnessTaskState.Running && task.WaitingFor != HarnessWaitingFor.None ? 0 : task.IsTerminal ? 2 : 1)
            .ThenByDescending(task => task.UpdatedUtcTicks).ThenBy(task => task.Id, StringComparer.Ordinal).ToArray();

        /// <summary>Desktop activity strips retain queued/paused requests and remove every terminal task.</summary>
        public static HarnessTask[] Active(IEnumerable<HarnessTask> tasks) => Order(tasks).Where(task => !task.IsTerminal).ToArray();
    }
}
