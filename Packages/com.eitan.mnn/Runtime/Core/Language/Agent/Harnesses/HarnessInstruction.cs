using System;
using System.Linq;
using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    [DataContract]
    public sealed class TaskUpdateRequest
    {
        [DataMember] public string TaskId;
        [DataMember] public string RequestId;
        [DataMember] public string Text;
        [DataMember(EmitDefaultValue = false)] public HarnessContentPart[] Attachments;
    }
    public enum HarnessInstructionState { Pending, Delivered, Rejected, Uncertain }
    [DataContract]
    public sealed class HarnessInstruction
    {
        [DataMember(Order = 0)] public string Id { get; private set; }
        [DataMember(Order = 1)] public string Text { get; private set; }
        [DataMember(Order = 2)] public HarnessInstructionState State { get; private set; }
        [DataMember(Order = 3)] public string Detail { get; private set; }
        [DataMember(Name = "Attachments", Order = 4, EmitDefaultValue = false)] private HarnessContentPart[] _attachments;
        public HarnessContentPart[] Attachments => _attachments?.ToArray() ?? new HarnessContentPart[0];
        internal HarnessInstruction(string id, string text, HarnessInstructionState state, string detail, HarnessContentPart[] attachments = null)
        { Id = id; Text = text; State = state; Detail = detail; _attachments = HarnessContentPart.CheckedInputs(attachments); Validate(); }
        internal HarnessInstruction WithState(HarnessInstructionState state, string detail) => new HarnessInstruction(Id, Text, state, detail, _attachments);
        internal void Validate()
        {
            HarnessText.Required(Id, 128); HarnessText.Required(Text, 32768); HarnessText.Required(Detail, 512);
            HarnessContentPart.CheckedInputs(_attachments);
            if (!Enum.IsDefined(typeof(HarnessInstructionState), State)) throw new SerializationException("Unknown instruction state.");
        }
        internal static void ValidateAll(HarnessInstruction[] values)
        {
            if (values == null) return;
            if (values.Length > 16 || values.Any(v => v == null)) throw new SerializationException("Instruction count exceeds its budget.");
            foreach (var value in values) value.Validate();
            if (values.Select(v => v.Id).Distinct(StringComparer.Ordinal).Count() != values.Length || values.Sum(v => v.Text.Length) > 65536 ||
                values.Sum(v => v.Attachments.Sum(p => p.Bytes)) > 262144)
                throw new SerializationException("Instruction identity or payload budget is invalid.");
        }
    }
}
