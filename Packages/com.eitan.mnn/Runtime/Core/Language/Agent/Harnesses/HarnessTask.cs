using System;
using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Detached, serializable task snapshot. Runtime state is never shared with consumers.</summary>
    [DataContract]
    public sealed class HarnessTask
    {
        [DataMember(Order = 0)] public string Id { get; internal set; }
        [DataMember(Order = 1)] public string OwnerId { get; internal set; }
        [DataMember(Order = 2)] public string ConversationId { get; internal set; }
        [DataMember(Order = 3)] public string SubmissionKey { get; internal set; }
        [DataMember(Order = 4)] public string HarnessId { get; internal set; }
        [DataMember(Order = 5)] public string WorkspaceKey { get; internal set; }
        [DataMember(Order = 6)] public string Objective { get; internal set; }
        [DataMember(Order = 7)] public HarnessTaskState State { get; internal set; }
        [DataMember(Order = 8)] public long Revision { get; internal set; }
        [DataMember(Order = 9)] public string Result { get; internal set; }
        [DataMember(Order = 10)] public string Activity { get; internal set; }
        [DataMember(Order = 11)] public HarnessWaitingFor WaitingFor { get; internal set; }
        [DataMember(Order = 12)] public string RequestId { get; internal set; }
        [DataMember(Order = 13)] public long CreatedUtcTicks { get; internal set; }
        [DataMember(Order = 14)] public long UpdatedUtcTicks { get; internal set; }
        [DataMember(Order = 15)] public bool ResultPending { get; internal set; }
        [DataMember(Order = 16)] public string LeaseId { get; internal set; }
        [DataMember(Order = 17)] public long LeaseUntilUtcTicks { get; internal set; }
        [DataMember(Order = 18)] public bool ContextInjected { get; internal set; }
        [DataMember(Order = 19)] public bool PlaybackStarted { get; internal set; }
        [DataMember(Order = 20)] public bool PlaybackCompleted { get; internal set; }
        [DataMember(Order = 21)] public bool PlaybackInterrupted { get; internal set; }
        [DataMember(Order = 22, EmitDefaultValue = false)] public HarnessArtifact[] Artifacts { get; internal set; }
        [DataMember(Order = 23, EmitDefaultValue = false)] public string BackendState { get; internal set; }
        [DataMember(Order = 24, EmitDefaultValue = false)] public HarnessContentPart[] Attachments { get; internal set; }
        [DataMember(Order = 25, EmitDefaultValue = false)] public string ResultConversationId { get; internal set; }
        [DataMember(Order = 26, EmitDefaultValue = false)] public string Title { get; internal set; }
        [DataMember(Order = 27, EmitDefaultValue = false)] public HarnessInputForm InputForm { get; internal set; }

        [DataMember(Order = 28, EmitDefaultValue = false)] public HarnessExecutionStep[] Execution { get; internal set; }
        [DataMember(Order = 29, EmitDefaultValue = false)] public HarnessInstruction[] Instructions { get; internal set; }

        [DataMember(Order = 30, EmitDefaultValue = false)] public string ResultCollectedBy { get; internal set; }
        [DataMember(Order = 31, EmitDefaultValue = false)] public HarnessCollectedResult[] CollectedResults { get; internal set; }

        [DataMember(Order = 32, EmitDefaultValue = false)] public HarnessInputPrompt InputPrompt { get; internal set; }

        [DataMember(Order = 33, EmitDefaultValue = false)] public HarnessSessionConfiguration SessionConfiguration { get; internal set; }

        [DataMember(Order = 34, EmitDefaultValue = false)] public HarnessNativeToolState NativeTools { get; internal set; }

        [DataMember(Order = 35, EmitDefaultValue = false)] public bool IsolatedContext { get; internal set; }

        public string DeliveryConversationId => ResultConversationId ?? ConversationId;

        public bool IsTerminal => State == HarnessTaskState.Completed || State == HarnessTaskState.Failed ||
            State == HarnessTaskState.Cancelled || State == HarnessTaskState.Interrupted;
        internal HarnessTask Copy()
        {
            var copy = (HarnessTask)MemberwiseClone();
            copy.Artifacts = Artifacts == null ? null : (HarnessArtifact[])Artifacts.Clone();
            copy.Attachments = Attachments == null ? null : (HarnessContentPart[])Attachments.Clone();
            copy.NativeTools = NativeTools?.Copy();
            copy.SessionConfiguration = SessionConfiguration?.Copy();
            copy.InputForm = InputForm?.Copy();
            copy.Execution = Execution == null ? null : (HarnessExecutionStep[])Execution.Clone();
            copy.Instructions = Instructions == null ? null : (HarnessInstruction[])Instructions.Clone();
            copy.CollectedResults = CollectedResults == null ? null : (HarnessCollectedResult[])CollectedResults.Clone();
            return copy;
        }
        internal static HarnessTask Create(TaskSubmission request) => new HarnessTask
        {
            Id = Guid.NewGuid().ToString("N"), OwnerId = request.OwnerId,
            ConversationId = request.ConversationId, SubmissionKey = request.SubmissionKey,
            HarnessId = request.HarnessId, WorkspaceKey = request.WorkspaceKey, Objective = request.Objective,
            Attachments = request.Attachments, IsolatedContext = request.IsolatedContext,
            ResultConversationId = request.ResultConversationId, Title = request.Title,
            Revision = 1, CreatedUtcTicks = DateTime.UtcNow.Ticks, UpdatedUtcTicks = DateTime.UtcNow.Ticks,
            State = HarnessTaskState.Queued
        };

        internal void Validate()
        {
            HarnessText.Required(Id, 128);
            new TaskSubmission(OwnerId, ConversationId, SubmissionKey, HarnessId, WorkspaceKey, Objective, Attachments, ResultConversationId, Title, IsolatedContext);
            if (Revision < 1 || CreatedUtcTicks < 1 || UpdatedUtcTicks < CreatedUtcTicks ||
                !Enum.IsDefined(typeof(HarnessTaskState), State) || !Enum.IsDefined(typeof(HarnessWaitingFor), WaitingFor))
                throw new SerializationException("Invalid persisted task state.");
            if (WaitingFor != HarnessWaitingFor.None) HarnessText.Required(RequestId, 128);
            if (InputForm != null && WaitingFor != HarnessWaitingFor.Input) throw new SerializationException("A form requires a pending input identity.");
            InputForm?.Validate();
            if (InputPrompt != null && (WaitingFor != HarnessWaitingFor.Input || State != HarnessTaskState.Running || InputForm != null))
                throw new SerializationException("An input prompt requires an exclusive running input identity.");
            InputPrompt?.Validate();
            SessionConfiguration?.Validate();
            NativeTools?.Validate();
            if (Result != null && Result.Length > 65536) throw new SerializationException("Persisted result exceeds its budget.");
            if (ResultPending && !IsTerminal) throw new SerializationException("Nonterminal result in outbox.");
            HarnessArtifact.Checked(Artifacts);
            if (Execution != null)
            {
                if (Execution.Length > 128) throw new SerializationException("Execution history exceeds its budget.");
                int characters = 0;
                for (int i = 0; i < Execution.Length; ++i)
                {
                    var step = Execution[i]; if (step == null || step.Sequence != i + 1) throw new SerializationException("Execution sequence is not contiguous.");
                    step.Validate(); characters += step.Text.Length + step.Title.Length;
                }
                if (characters > 16384) throw new SerializationException("Execution payload exceeds its budget.");
            }
            HarnessInstruction.ValidateAll(Instructions);
            HarnessCollectedResult.Checked(CollectedResults);
            if (ResultCollectedBy != null)
            {
                HarnessText.Required(ResultCollectedBy, 128);
                if (!IsTerminal || ResultPending || ResultConversationId != HarnessCollectedResult.ConversationFor(ResultCollectedBy))
                    throw new SerializationException("Invalid collected child result.");
            }
            if (BackendState != null) HarnessText.Required(BackendState, 16384);
        }
    }
}
