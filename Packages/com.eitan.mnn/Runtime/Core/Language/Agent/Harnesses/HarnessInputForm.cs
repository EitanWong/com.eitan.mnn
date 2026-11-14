using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace MNN.Samples.SiriAI.Harnesses
{
    [DataContract]
    public sealed class HarnessInputChoice
    {
        [DataMember] public string Value;
        [DataMember] public string Label;
        [DataMember(EmitDefaultValue = false)] public string Description;
        internal HarnessInputChoice Copy() => (HarnessInputChoice)MemberwiseClone();
    }

    [DataContract]
    public sealed class HarnessInputField
    {
        [DataMember] public string Name;
        [DataMember] public string Type;
        [DataMember] public string Label;
        [DataMember(EmitDefaultValue = false)] public string Description;
        [DataMember] public bool Required;
        [DataMember(EmitDefaultValue = false)] public bool AllowOther;
        [DataMember(EmitDefaultValue = false)] public bool Secret;
        [DataMember(EmitDefaultValue = false)] public HarnessInputChoice[] Choices;
        [DataMember(EmitDefaultValue = false)] public string[] Defaults;
        internal HarnessInputField Copy()
        {
            var copy = (HarnessInputField)MemberwiseClone();
            copy.Choices = Choices?.Select(c => c.Copy()).ToArray();
            copy.Defaults = Defaults == null ? null : (string[])Defaults.Clone();
            return copy;
        }
    }

    /// <summary>Protocol-neutral display schema. The backend owns full constraint validation before settling a reply.</summary>
    [DataContract]
    public sealed class HarnessInputForm
    {
        [DataMember] public HarnessInputField[] Fields;
        internal HarnessInputForm Copy() => new HarnessInputForm { Fields = Fields.Select(f => f.Copy()).ToArray() };
        internal void Validate()
        {
            if (Fields == null || Fields.Length > 64 || Fields.Any(f => f == null) ||
                Fields.Select(f => f.Name).Distinct(StringComparer.Ordinal).Count() != Fields.Length)
                throw new SerializationException("Invalid input form fields.");
            foreach (var field in Fields)
            {
                HarnessText.Required(field.Name, 160); HarnessText.Required(field.Label, 512);
                if (!new[] { "string", "integer", "number", "boolean", "array" }.Contains(field.Type) ||
                    field.Description?.Length > 2048 || field.Defaults?.Length > 64 || field.Defaults?.Any(v => v == null || v.Length > 8192) == true)
                    throw new SerializationException("Invalid input form field.");
                if (field.Choices != null && (field.Choices.Length == 0 || field.Choices.Length > 64 ||
                    field.Choices.Any(c => c == null || c.Value == null || c.Value.Length > 1024 || string.IsNullOrEmpty(c.Label) || c.Label.Length > 512 || c.Description?.Length > 2048) ||
                    field.Choices.Select(c => c.Value).Distinct(StringComparer.Ordinal).Count() != field.Choices.Length))
                    throw new SerializationException("Invalid input form choices.");
                if (field.AllowOther && (field.Type != "string" || field.Choices == null) || field.Secret && field.Type != "string")
                    throw new SerializationException("Freeform choices and secret input require string fields.");
                if (field.Type == "array" && field.Choices == null) throw new SerializationException("Array input requires explicit choices.");
            }
            if (TaskHostJson.Write(this).Length > 65536) throw new SerializationException("Input form exceeds its budget.");
        }
        public Dictionary<string, string[]> CreateDraft()
        {
            Validate();
            return Fields.ToDictionary(f => f.Name, f => f.Defaults != null ? (string[])f.Defaults.Clone() :
                f.Required ? InitialValue(f) : null, StringComparer.Ordinal);
        }
        public static string[] InitialValue(HarnessInputField field) => field.Type == "array" ? new string[0] :
            new[] { field.Type == "boolean" ? "false" : "" };
        public string EncodeValues(IDictionary<string, string[]> values)
        {
            Validate();
            if (values == null || values.Keys.Any(k => !Fields.Any(f => f.Name == k))) throw new ArgumentException("Unknown form field.");
            var entries = new List<string>();
            foreach (var field in Fields)
            {
                string[] value;
                if (!values.TryGetValue(field.Name, out value) || value == null)
                {
                    if (field.Required) throw new ArgumentException(field.Label + " is required.");
                    continue;
                }
                if (value.Any(v => v == null || v.Length > 8192) || value.Length > 64 || field.Type != "array" && value.Length != 1)
                    throw new ArgumentException("Invalid value for " + field.Label);
                if (field.Choices != null && !field.AllowOther && value.Any(v => !field.Choices.Any(c => c.Value == v))) throw new ArgumentException("Choose a value for " + field.Label);
                string encoded;
                switch (field.Type)
                {
                    case "string": encoded = Json(value[0]); break;
                    case "array": encoded = Json(value); break;
                    case "boolean":
                        bool flag; if (!bool.TryParse(value[0], out flag)) throw new ArgumentException(field.Label + " must be true or false.");
                        encoded = flag ? "true" : "false"; break;
                    case "integer":
                        long integer; if (!long.TryParse(value[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out integer)) throw new ArgumentException(field.Label + " requires an integer.");
                        encoded = integer.ToString(CultureInfo.InvariantCulture); break;
                    default:
                        double number;
                        if (!double.TryParse(value[0], NumberStyles.Float, CultureInfo.InvariantCulture, out number) || double.IsNaN(number) || double.IsInfinity(number))
                            throw new ArgumentException(field.Label + " requires a finite number.");
                        encoded = number.ToString("R", CultureInfo.InvariantCulture); break;
                }
                entries.Add(Json(field.Name) + ":" + encoded);
            }
            string result = "{" + string.Join(",", entries) + "}";
            if (result.Length > 8192) throw new ArgumentException("Form response exceeds its budget.");
            return result;
        }
        private static string Json<T>(T value) => Encoding.UTF8.GetString(TaskHostJson.Write(value));
    }
}
