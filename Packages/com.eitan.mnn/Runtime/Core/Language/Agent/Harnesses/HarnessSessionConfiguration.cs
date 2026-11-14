using System;
using System.Linq;
using System.Runtime.Serialization;

namespace MNN.Samples.SiriAI.Harnesses
{
    [DataContract]
    public sealed class HarnessSessionChoice
    {
        [DataMember] public string Value;
        [DataMember] public string Name;
        [DataMember(EmitDefaultValue = false)] public string Group;
        internal HarnessSessionChoice Copy() => (HarnessSessionChoice)MemberwiseClone();
    }
    [DataContract]
    public sealed class HarnessSessionOption
    {
        [DataMember] public string Id;
        [DataMember] public string Name;
        [DataMember(EmitDefaultValue = false)] public string Category;
        [DataMember] public bool IsBoolean;
        [DataMember(EmitDefaultValue = false)] public string CurrentValue;
        [DataMember] public bool BooleanValue;
        [DataMember(EmitDefaultValue = false)] public HarnessSessionChoice[] Choices;
        internal HarnessSessionOption Copy()
        { var copy = (HarnessSessionOption)MemberwiseClone(); copy.Choices = Choices?.Select(c => c.Copy()).ToArray(); return copy; }
    }
    /// <summary>Backend-reported session state. Task results preserve their own last observed snapshot.</summary>
    [DataContract]
    public sealed class HarnessSessionConfiguration
    {
        [DataMember] public string SessionId;
        [DataMember(EmitDefaultValue = false)] public string CurrentModeId;
        [DataMember] public HarnessSessionChoice[] Modes;
        [DataMember] public HarnessSessionOption[] Options;
        internal HarnessSessionConfiguration Copy() => new HarnessSessionConfiguration { SessionId = SessionId, CurrentModeId = CurrentModeId,
            Modes = Modes.Select(c => c.Copy()).ToArray(), Options = Options.Select(o => o.Copy()).ToArray() };
        internal void Validate()
        {
            HarnessText.Required(SessionId, 4096);
            Choices(Modes);
            if (CurrentModeId != null) HarnessText.Required(CurrentModeId, 256);
            if (Options == null || Options.Length > 32 || Options.Any(o => o == null) || Options.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != Options.Length)
                throw new SerializationException("Invalid session options.");
            foreach (var option in Options)
            {
                HarnessText.Required(option.Id, 256); HarnessText.Required(option.Name, 512);
                if (option.Category != null) HarnessText.Required(option.Category, 128);
                if (option.IsBoolean)
                { if (option.CurrentValue != null || option.Choices != null) throw new SerializationException("Boolean option cannot contain selector values."); }
                else { HarnessText.Required(option.CurrentValue, 1024); Choices(option.Choices); }
            }
            if (TaskHostJson.Write(this).Length > 65536) throw new SerializationException("Session configuration exceeds its budget.");
        }
        private static void Choices(HarnessSessionChoice[] choices)
        {
            if (choices == null || choices.Length > 64 || choices.Any(c => c == null) || choices.Select(c => c.Value).Distinct(StringComparer.Ordinal).Count() != choices.Length)
                throw new SerializationException("Invalid session choices.");
            foreach (var choice in choices)
            {
                HarnessText.Required(choice.Value, 1024); HarnessText.Required(choice.Name, 512);
                if (choice.Group != null) HarnessText.Required(choice.Group, 512);
            }
        }
    }
}
