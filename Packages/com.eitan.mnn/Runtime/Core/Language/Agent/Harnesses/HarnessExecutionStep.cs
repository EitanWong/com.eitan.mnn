using System;
using System.Linq;
using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    public enum HarnessExecutionKind { Activity, Plan, ToolCall, ToolResult, Result, Error }
    [DataContract]
    public sealed class HarnessExecutionStep
    {
        [DataMember(Order = 0)] public long Sequence { get; private set; }
        [DataMember(Order = 1)] public HarnessExecutionKind Kind { get; private set; }
        [DataMember(Order = 2)] public string Title { get; private set; }
        [DataMember(Order = 3)] public string Text { get; private set; }
        [DataMember(Order = 4)] public string State { get; private set; }
        internal HarnessExecutionStep(long sequence, HarnessExecutionKind kind, string title, string text, string state)
        { Sequence = sequence; Kind = kind; Title = title; Text = text; State = state; Validate(); }
        internal void Validate()
        {
            if (Sequence < 1 || !Enum.IsDefined(typeof(HarnessExecutionKind), Kind)) throw new SerializationException("Invalid execution step.");
            HarnessText.Required(Title, 256); HarnessText.Required(State, 32);
            if (Text == null || Text.Length > 4096) throw new SerializationException("Execution detail exceeds its display budget.");
        }
        internal static void Append(HarnessTask task, HarnessExecutionKind kind, string title, string text, string state)
        {
            var previous = task.Execution ?? new HarnessExecutionStep[0];
            if (previous.Length >= 128) return;
            int used = previous.Sum(step => step.Text.Length + step.Title.Length);
            if (previous.Length == 127 || used > 14000)
            {
                if (previous.Any(step => step.Title == "Execution history limit")) return;
                kind = HarnessExecutionKind.Error; title = "Execution history limit";
                text = "Further execution details exceeded the bounded history budget. The final task result is retained separately.";
                state = "Failed";
            }
            title = Clip(title ?? "Agent activity", 256); text = Clip(text ?? "", Math.Min(4096, Math.Max(0, 14000 - used)));
            var next = new HarnessExecutionStep[previous.Length + 1]; Array.Copy(previous, next, previous.Length);
            next[previous.Length] = new HarnessExecutionStep(previous.Length + 1, kind, title, text, state);
            task.Execution = next;
        }
        internal static string Clip(string text, int limit)
        {
            if (text.Length <= limit) return text;
            int end = limit > 0 && char.IsHighSurrogate(text[limit - 1]) ? limit - 1 : limit;
            return text.Substring(0, end);
        }
    }
}
