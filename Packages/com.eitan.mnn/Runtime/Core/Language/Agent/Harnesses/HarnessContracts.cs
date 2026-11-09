using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    [Flags]
    public enum HarnessCapabilities { None = 0, Cancel = 1, Input = 2, Permission = 4, Update = 8, Attachments = 16, UpdateAttachments = 32 }
    public enum HarnessTaskState { Queued, Running, Cancelling, Completed, Failed, Cancelled, Interrupted }
    public enum HarnessWaitingFor { None, Input, Permission }
    public enum HarnessInputAction { Answer, Clarify, Cancel, Decline }
    public enum ResultReceipt { ContextInjected, PlaybackStarted, PlaybackCompleted, Interrupted, TextDisplayed }

    public sealed class TaskSubmission
    {
        public string OwnerId { get; }
        public string ConversationId { get; }
        public string SubmissionKey { get; }
        public string HarnessId { get; }
        public string WorkspaceKey { get; }
        public string Objective { get; }
        public string ResultConversationId { get; }
        public string Title { get; }
        private readonly HarnessContentPart[] _attachments;
        public HarnessContentPart[] Attachments => (HarnessContentPart[])_attachments.Clone();
        public TaskSubmission(string ownerId, string conversationId, string submissionKey,
            string harnessId, string workspaceKey, string objective, HarnessContentPart[] attachments = null,
            string resultConversationId = null, string title = null)
        {
            OwnerId = HarnessText.Required(ownerId, 128);
            ConversationId = HarnessText.Required(conversationId, 128);
            SubmissionKey = HarnessText.Required(submissionKey, 256);
            HarnessId = HarnessText.Required(harnessId, 128);
            WorkspaceKey = HarnessText.Required(workspaceKey, 1024);
            Objective = HarnessText.Required(objective, 32768);
            ResultConversationId = resultConversationId == null ? null : HarnessText.Required(resultConversationId, 128);
            Title = title == null ? null : HarnessText.Required(title, 256);
            _attachments = HarnessContentPart.CheckedInputs(attachments);
        }
    }

    public sealed class HarnessActivity
    {
        public HarnessWaitingFor WaitingFor { get; }
        public string RequestId { get; }
        public string Summary { get; }
        public HarnessExecutionKind ExecutionKind { get; }
        public string ExecutionDetail { get; }
        public string ExecutionState { get; }
        private readonly HarnessArtifact[] _artifacts;
        public HarnessArtifact[] Artifacts => _artifacts == null ? null : (HarnessArtifact[])_artifacts.Clone();
        private readonly HarnessInputForm _inputForm;
        public HarnessInputForm InputForm => _inputForm?.Copy();
        public HarnessInputPrompt InputPrompt { get; }
        private readonly HarnessSessionConfiguration _sessionConfiguration;
        public HarnessSessionConfiguration SessionConfiguration => _sessionConfiguration?.Copy();
        private readonly HarnessNativeToolState _nativeTools;
        public HarnessNativeToolState NativeTools => _nativeTools?.Copy();
        public HarnessActivity(HarnessWaitingFor waitingFor, string requestId, string summary, HarnessArtifact[] artifacts = null,
            HarnessInputForm inputForm = null, HarnessExecutionKind executionKind = HarnessExecutionKind.Activity,
            string executionDetail = null, string executionState = "Running", HarnessInputPrompt inputPrompt = null, HarnessSessionConfiguration sessionConfiguration = null, HarnessNativeToolState nativeTools = null)
        {
            if (!Enum.IsDefined(typeof(HarnessWaitingFor), waitingFor)) throw new ArgumentOutOfRangeException(nameof(waitingFor));
            WaitingFor = waitingFor;
            RequestId = waitingFor == HarnessWaitingFor.None ? null : HarnessText.Required(requestId, 128);
            Summary = HarnessText.Required(summary, 4096);
            if (!Enum.IsDefined(typeof(HarnessExecutionKind), executionKind)) throw new ArgumentOutOfRangeException(nameof(executionKind));
            ExecutionKind = executionKind; ExecutionDetail = executionDetail == null ? Summary : HarnessExecutionStep.Clip(executionDetail, 4096);
            ExecutionState = HarnessText.Required(executionState, 32);
            _artifacts = artifacts == null ? null : HarnessArtifact.Checked(artifacts);
            if (inputForm != null && waitingFor != HarnessWaitingFor.Input) throw new ArgumentException("A form must belong to a pending input request.");
            inputForm?.Validate(); _inputForm = inputForm?.Copy();
            if (inputPrompt != null && (waitingFor != HarnessWaitingFor.Input || inputForm != null))
                throw new ArgumentException("Input prompt must exclusively belong to a pending input request.");
            inputPrompt?.Validate(); InputPrompt = inputPrompt;
            sessionConfiguration?.Validate(); _sessionConfiguration = sessionConfiguration?.Copy();
            nativeTools?.Validate(); _nativeTools = nativeTools?.Copy();
        }
    }

    public sealed class HarnessOutcome
    {
        public HarnessTaskState State { get; }
        public string Text { get; }
        private readonly HarnessArtifact[] _artifacts;
        public HarnessArtifact[] Artifacts => (HarnessArtifact[])_artifacts.Clone();
        private HarnessCollectedResult[] _collectedResults = new HarnessCollectedResult[0];
        public HarnessCollectedResult[] CollectedResults => (HarnessCollectedResult[])_collectedResults.Clone();
        public HarnessOutcome WithCollectedResults(HarnessCollectedResult[] sources)
        {
            var outcome = new HarnessOutcome(State, Text, _artifacts);
            outcome._collectedResults = HarnessCollectedResult.Checked(sources); return outcome;
        }
        private HarnessOutcome(HarnessTaskState state, string text, HarnessArtifact[] artifacts = null)
        { State = state; Text = HarnessText.Required(text, 65536); _artifacts = HarnessArtifact.Checked(artifacts); }
        public static HarnessOutcome Completed(string text, HarnessArtifact[] artifacts = null) => new HarnessOutcome(HarnessTaskState.Completed, text, artifacts);
        public static HarnessOutcome Failed(string text) => new HarnessOutcome(HarnessTaskState.Failed, text);
        public static HarnessOutcome Interrupted(string text) => new HarnessOutcome(HarnessTaskState.Interrupted, text);
        public static HarnessOutcome Cancelled() => new HarnessOutcome(HarnessTaskState.Cancelled, "Cancelled by backend.");
        public static HarnessOutcome Cancelled(string scopeDescription) => new HarnessOutcome(HarnessTaskState.Cancelled, scopeDescription);
        public HarnessOutcome WithArtifacts(HarnessArtifact[] artifacts) => new HarnessOutcome(State, Text, artifacts).WithCollectedResults(_collectedResults);
    }

    /// <summary>Owned by a host, never by a foreground response or connection.</summary>
    public interface IHarnessAdapter
    {
        string Id { get; }
        HarnessCapabilities Capabilities { get; }
        Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken hostStopping);
        Task<bool> CancelAsync(HarnessTask task, CancellationToken rpcCancellation);
        Task RespondAsync(HarnessTask task, string requestId, string reply, bool allow, CancellationToken rpcCancellation);
        Task UpdateAsync(HarnessTask task, string instruction, CancellationToken rpcCancellation);
    }

    /// <summary>Runtime owns a cancellation token per execution. Run returns the observed terminal outcome;
    /// aborting a local request does not establish cancellation of work at its remote provider.</summary>
    public interface ILocalCancellationHarnessAdapter : IHarnessAdapter
    {
        string CancellationScope { get; }
        Task<HarnessOutcome> RunCancellableAsync(HarnessTask task, Action<HarnessActivity> activity,
            CancellationToken cancellationRequested, CancellationToken hostStopping);
    }

    /// <summary>A native cancel command is only admission. Its observed terminal outcome owns the result.</summary>
    public interface IHarnessCancellationOutcomeAdapter : IHarnessAdapter
    {
        string CancellationScope { get; }
        Task<HarnessOutcome> CancelWithOutcomeAsync(HarnessTask task, CancellationToken rpcCancellation);
    }

    public interface IHarnessSubmissionValidator
    {
        void ValidateSubmission(TaskSubmission submission);
    }

    public interface IHarnessInputActionAdapter
    {
        Task RespondInputAsync(HarnessTask task, string requestId, string reply, HarnessInputAction action, CancellationToken cancellation);
    }

    /// <summary>Opaque bounded checkpoint contains remote handles, never credentials. Recovery must not replay uncertain side effects.</summary>
    public interface IRecoverableHarnessAdapter : IHarnessAdapter
    {
        bool CanRecover(string checkpoint);
        Task<HarnessOutcome> RunDurableAsync(HarnessTask task, Action<HarnessActivity> activity,
            Action<string> checkpoint, CancellationToken hostStopping);
    }

    internal static class HarnessText
    {
        internal static string Required(string value, int limit)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > limit)
                throw new ArgumentException("Expected nonempty bounded text.", nameof(value));
            return value;
        }
    }
}
