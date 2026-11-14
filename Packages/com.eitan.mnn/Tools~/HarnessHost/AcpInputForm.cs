using System.Globalization;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Official primitive elicitation schema; one validator owns native response constraints.</summary>
internal sealed class AcpInputForm
{
    private readonly JsonElement _schema;
    private readonly JsonElement _properties;
    internal HarnessInputForm Display { get; }
    internal AcpInputForm(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object || Encoding.UTF8.GetByteCount(schema.GetRawText()) > 65536 ||
            schema.TryGetProperty("type", out var type) && type.GetString() != "object") throw new ArgumentException("Invalid form schema.");
        _schema = schema.Clone();
        var properties = MspWire.Field(schema, "properties");
        if (properties.ValueKind == JsonValueKind.Undefined) properties = JsonSerializer.SerializeToElement(new { });
        if (properties.ValueKind != JsonValueKind.Object) throw new ArgumentException("Form properties are required.");
        if (properties.EnumerateObject().Count() > 64) throw new ArgumentException("Form field count exceeds its budget.");
        _properties = properties.Clone();
        var required = Strings(MspWire.Field(schema, "required"), true);
        if (required.Distinct(StringComparer.Ordinal).Count() != required.Length || required.Any(n => !properties.TryGetProperty(n, out _)))
            throw new ArgumentException("Invalid required form fields.");
        var fields = new List<HarnessInputField>();
        foreach (var property in properties.EnumerateObject())
        {
            var value = property.Value;
            string kind = MspWire.Text(value, "type");
            if (kind is not ("string" or "integer" or "number" or "boolean" or "array")) throw new ArgumentException("Unsupported form field type.");
            var field = new HarnessInputField { Name = property.Name, Type = kind, Label = MspWire.Text(value, "title") ?? property.Name,
                Description = MspWire.Text(value, "description"), Required = required.Contains(property.Name, StringComparer.Ordinal) };
            if (kind == "string")
            {
                field.Choices = Choices(value, "oneOf");
                Bound(value, "minLength"); Bound(value, "maxLength");
                string pattern = MspWire.Text(value, "pattern");
                if (pattern != null) Pattern(pattern);
                string format = MspWire.Text(value, "format");
                if (format != null && format is not ("email" or "uri" or "date" or "date-time")) throw new ArgumentException("Unsupported string format.");
            }
            if (kind == "array")
            {
                var items = MspWire.Field(value, "items");
                string itemType = MspWire.Text(items, "type");
                if (itemType != null && itemType != "string") throw new ArgumentException("Unsupported array item type.");
                field.Choices = Choices(items, "anyOf") ?? throw new ArgumentException("Array choices are required.");
                Bound(value, "minItems"); Bound(value, "maxItems");
            }
            if (kind is "integer" or "number")
            {
                NumberBound(value, "minimum", kind == "integer"); NumberBound(value, "maximum", kind == "integer");
            }
            if (value.TryGetProperty("default", out var initial) && initial.ValueKind != JsonValueKind.Null)
            {
                ValidateField(value, initial);
                field.Defaults = kind == "array" ? Strings(initial, false) : new[] { kind == "string" ? initial.GetString() : initial.GetRawText() };
            }
            fields.Add(field);
        }
        Display = new HarnessInputForm { Fields = fields.ToArray() }; Display.Validate();
    }
    private static HarnessInputChoice[] Choices(JsonElement source, string titled)
    {
        string[] plain = source.TryGetProperty("enum", out var items) && items.ValueKind != JsonValueKind.Null ? Strings(items, false) : null;
        if (source.TryGetProperty(titled, out var options) && options.ValueKind != JsonValueKind.Null)
        {
            if (options.ValueKind != JsonValueKind.Array) throw new ArgumentException("Invalid form choices.");
            return options.EnumerateArray().Select(o => new HarnessInputChoice { Value = MspWire.Text(o, "const"), Label = MspWire.Text(o, "title") })
                .Where(o => plain == null || plain.Contains(o.Value, StringComparer.Ordinal)).ToArray();
        }
        return plain?.Select(v => new HarnessInputChoice { Value = v, Label = v.Length == 0 ? "(empty)" : v }).ToArray();
    }
    internal JsonElement Answer(string json)
    {
        JsonDocument parsed;
        try { parsed = JsonDocument.Parse(json); }
        catch (JsonException error) { throw new ArgumentException("Form answer requires valid JSON.", error); }
        using var document = parsed;
        var answer = document.RootElement;
        if (answer.ValueKind != JsonValueKind.Object) throw new ArgumentException("Form answer must be an object.");
        var properties = _properties;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in answer.EnumerateObject())
        {
            if (!names.Add(value.Name) || !properties.TryGetProperty(value.Name, out var field)) throw new ArgumentException("Unknown or repeated form field.");
            ValidateField(field, value.Value);
        }
        if (Strings(MspWire.Field(_schema, "required"), true).Any(name => !names.Contains(name))) throw new ArgumentException("Required form field is missing.");
        return answer.Clone();
    }
    private static void ValidateField(JsonElement schema, JsonElement value)
    {
        string type = MspWire.Text(schema, "type");
        switch (type)
        {
            case "string":
                if (value.ValueKind != JsonValueKind.String) throw new ArgumentException("A string field needs text.");
                string text = value.GetString();
                int length = text.Length - text.Where((c, i) => char.IsLowSurrogate(c) && i > 0 && char.IsHighSurrogate(text[i - 1])).Count();
                CheckRange(length, Bound(schema, "minLength"), Bound(schema, "maxLength"));
                if (Choices(schema, "oneOf") is { } choices && !choices.Any(c => c.Value == text)) throw new ArgumentException("String value is outside form choices.");
                string pattern = MspWire.Text(schema, "pattern");
                if (pattern != null && !Matches(pattern, text)) throw new ArgumentException("Text does not match the requested pattern.");
                CheckFormat(MspWire.Text(schema, "format"), text); break;
            case "integer": case "number":
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double number) || !double.IsFinite(number)) throw new ArgumentException("A numeric field needs a finite number.");
                if (type == "integer")
                {
                    if (!value.TryGetDecimal(out decimal integer) || integer != decimal.Truncate(integer) || integer < long.MinValue || integer > long.MaxValue)
                        throw new ArgumentException("An integer field needs an int64 value.");
                    if (schema.TryGetProperty("minimum", out var min) && min.ValueKind != JsonValueKind.Null && integer < min.GetInt64() ||
                        schema.TryGetProperty("maximum", out var max) && max.ValueKind != JsonValueKind.Null && integer > max.GetInt64()) throw new ArgumentException("Integer is outside requested bounds.");
                }
                else CheckRange(number, NumberBound(schema, "minimum", false), NumberBound(schema, "maximum", false)); break;
            case "boolean":
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new ArgumentException("A boolean field needs true or false."); break;
            case "array":
                string[] selected = Strings(value, false);
                CheckRange(selected.Length, Bound(schema, "minItems"), Bound(schema, "maxItems"));
                var allowed = Choices(schema.GetProperty("items"), "anyOf");
                if (selected.Length > 64 || selected.Distinct(StringComparer.Ordinal).Count() != selected.Length || selected.Any(v => !allowed.Any(c => c.Value == v)))
                    throw new ArgumentException("Invalid multi-select values."); break;
            default: throw new ArgumentException("Unsupported form field.");
        }
    }
    private static string[] Strings(JsonElement value, bool optional)
    {
        if (optional && value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return [];
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > 64 || value.EnumerateArray().Any(v => v.ValueKind != JsonValueKind.String))
            throw new ArgumentException("Expected a bounded string array.");
        return value.EnumerateArray().Select(v => v.GetString()).ToArray();
    }
    private static double? Bound(JsonElement source, string name)
    {
        if (!source.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (!value.TryGetUInt64(out ulong count)) throw new ArgumentException("Invalid form length bound.");
        return count;
    }
    private static double? NumberBound(JsonElement source, string name, bool integer)
    {
        if (!source.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double number) || !double.IsFinite(number) || integer && !value.TryGetInt64(out _))
            throw new ArgumentException("Invalid numeric form bound.");
        return number;
    }
    private static void CheckRange(double value, double? minimum, double? maximum)
    { if (minimum.HasValue && value < minimum || maximum.HasValue && value > maximum) throw new ArgumentException("Form value is outside its requested bounds."); }
    private static Regex Pattern(string pattern)
    {
        if (pattern.Length > 512) throw new ArgumentException("Form pattern exceeds its budget.");
        return new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.ECMAScript, TimeSpan.FromMilliseconds(100));
    }
    private static bool Matches(string pattern, string value)
    {
        try { return Pattern(pattern).IsMatch(value); }
        catch (RegexMatchTimeoutException error) { throw new ArgumentException("Form pattern evaluation exceeded its budget.", error); }
    }
    private static void CheckFormat(string format, string text)
    {
        bool valid = format switch {
            null => true,
            "uri" => Uri.TryCreate(text, UriKind.Absolute, out _),
            "email" => MailAddress.TryCreate(text, out var email) && email.Address == text,
            "date" => DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            "date-time" => Regex.IsMatch(text, @"^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d+)?(?:Z|[+-]\d\d:\d\d)$", RegexOptions.CultureInvariant) &&
                DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
            _ => false };
        if (!valid) throw new ArgumentException("Text does not match the requested format.");
    }
}
