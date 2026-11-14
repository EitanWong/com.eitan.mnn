using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class AcpSession
{
    private JsonElement _sessionOptions;
    private HarnessSessionChoice[] _sessionModes = [];
    private string _currentSessionMode;
    private TaskCompletionSource<bool> _modeChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private HarnessSessionOption[] SnapshotOptions() { lock (_activitySync) return DisplayOptions(_sessionOptions); }
    private HarnessSessionConfiguration SessionConfiguration()
    {
        lock (_activitySync)
        {
            if (_sessionId == null) return null;
            var snapshot = new HarnessSessionConfiguration { SessionId = _sessionId, CurrentModeId = _currentSessionMode,
                Modes = _sessionModes, Options = DisplayOptions(_sessionOptions) };
            snapshot.Validate(); return snapshot;
        }
    }
    private async Task ConfigureSessionAsync(JsonElement session, CancellationToken cancellation)
    {
        SetOptions(MspWire.Field(session, "configOptions"));
        lock (_activitySync)
        {
            var modes = MspWire.Field(session, "modes");
            _sessionModes = MspWire.Field(modes, "availableModes").ValueKind == JsonValueKind.Array
                ? modes.GetProperty("availableModes").EnumerateArray().Select(v => new HarnessSessionChoice { Value = MspWire.Required(v, "id"), Name = MspWire.Required(v, "name") }).ToArray() : [];
            _currentSessionMode = MspWire.Text(modes, "currentModeId");
            SessionConfiguration();
        }
        if (_configuration.PermissionMode == "full")
        {
            string id = _configuration.Backend == "minimax" ? "permissionMode" : _configuration.Backend == "kimi" ? "mode" : null;
            if (id != null) await SetOptionAsync(id, JsonSerializer.SerializeToElement(id == "mode" ? "auto" : "bypassPermissions"), cancellation);
        }
        if (_configuration.SessionConfigOptions == null || _configuration.SessionConfigOptions.Count > 32) throw new AcpConfigurationException();
        foreach (var setting in _configuration.SessionConfigOptions) await SetOptionAsync(setting.Key, setting.Value, cancellation);
        string model = _configuration.Model?.Trim();
        if (!string.IsNullOrEmpty(model) && !model.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            var options = SnapshotOptions();
            var option = options.FirstOrDefault(v => string.Equals(v.Category, "model", StringComparison.OrdinalIgnoreCase)) ??
                options.FirstOrDefault(v => v.Id.Equals("model", StringComparison.OrdinalIgnoreCase) || v.Id.Equals("models", StringComparison.OrdinalIgnoreCase));
            if (option == null || option.IsBoolean) throw new AcpConfigurationException();
            await SetOptionAsync(option.Id, JsonSerializer.SerializeToElement(model), cancellation);
        }
        string desiredMode = _configuration.SessionMode;
        if (desiredMode == null) return;
        if (!_sessionModes.Any(v => v.Value == desiredMode)) throw new AcpConfigurationException();
        if (_currentSessionMode == desiredMode) return;
        await CallAsync("session/set_mode", new { sessionId = _sessionId, modeId = desiredMode }, cancellation);
        using var confirmation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        confirmation.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            while (true)
            {
                Task changed;
                lock (_activitySync) { if (_currentSessionMode == desiredMode) break; changed = _modeChanged.Task; }
                await changed.WaitAsync(confirmation.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested)
        { throw new IOException("ACP mode response was received but no matching selected-mode update was observed."); }
    }
    private async Task SetOptionAsync(string id, JsonElement desired, CancellationToken cancellation)
    {
        var option = SnapshotOptions().FirstOrDefault(v => v.Id == id) ?? throw new AcpConfigurationException();
        object value; string selected = null;
        if (option.IsBoolean)
        {
            if (desired.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new AcpConfigurationException();
            value = desired.GetBoolean(); if (option.BooleanValue == (bool)value) return;
        }
        else
        {
            if (desired.ValueKind != JsonValueKind.String) throw new AcpConfigurationException();
            string text = desired.GetString()?.Trim();
            var matches = option.Choices.Where(v => string.Equals(v.Value, text, StringComparison.OrdinalIgnoreCase) || string.Equals(v.Name.Trim(), text, StringComparison.OrdinalIgnoreCase)).ToArray();
            // Prefer exact value identity; ambiguous display labels must not choose a different model silently.
            selected = option.Choices.FirstOrDefault(v => string.Equals(v.Value, text, StringComparison.OrdinalIgnoreCase))?.Value;
            selected ??= matches.Length == 1 ? matches[0].Value : throw new AcpConfigurationException();
            value = selected; if (option.CurrentValue == selected) return;
        }
        object payload = option.IsBoolean ? new { sessionId = _sessionId, configId = id, type = "boolean", value } : (object)new { sessionId = _sessionId, configId = id, value };
        var envelope = await CallEnvelopeAsync("session/set_config_option", payload, cancellation);
        if (envelope.TryGetProperty("error", out var error))
        {
            if (MspWire.Field(error, "code").TryGetInt32(out int code) && code is -32601 or -32602) throw new AcpConfigurationException();
            throw new IOException("ACP session setting outcome is unconfirmed.");
        }
        var response = envelope.GetProperty("result");
        var returned = MspWire.Field(response, "configOptions");
        if (returned.ValueKind != JsonValueKind.Array) throw new IOException("ACP setting response omitted configuration confirmation.");
        SetOptions(returned);
        var updated = SnapshotOptions().FirstOrDefault(v => v.Id == id);
        if (updated == null || updated.IsBoolean != option.IsBoolean)
            throw new IOException("ACP response did not report the selected configuration option.");
        if (option.IsBoolean ? updated.BooleanValue != (bool)value : updated.CurrentValue != selected)
            throw new AcpConfigurationException(); // The reported value is known and differs from the requested setting.
    }
    private void SetOptions(JsonElement options)
    {
        lock (_activitySync)
        {
            if (options.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) { _sessionOptions = default; return; }
            DisplayOptions(options); _sessionOptions = options.Clone();
        }
    }
    private bool UpdateConfiguration(JsonElement update)
    {
        string kind = MspWire.Text(update, "sessionUpdate");
        if (kind is not ("config_option_update" or "current_mode_update")) return false;
        lock (_activitySync)
        {
            if (kind == "config_option_update") SetOptions(MspWire.Field(update, "configOptions"));
            else
            {
                string mode = MspWire.Required(update, "currentModeId");
                if (mode.Length > 256) throw new IOException("ACP mode identity exceeds its budget.");
                _currentSessionMode = mode; _modeChanged.TrySetResult(true); _modeChanged = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }
            if (_collect) PublishActivity("Backend session configuration changed.");
        }
        return true;
    }
    private static HarnessSessionOption[] DisplayOptions(JsonElement options)
    {
        if (options.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return [];
        if (options.ValueKind != JsonValueKind.Array || options.GetArrayLength() > 32 || options.GetRawText().Length > 65536) throw new AcpConfigurationException();
        var values = new List<HarnessSessionOption>();
        foreach (var v in options.EnumerateArray())
        {
            string type = MspWire.Text(v, "type");
            if (type is not ("select" or "boolean")) continue;
            var option = new HarnessSessionOption { Id = MspWire.Required(v, "id"), Name = MspWire.Text(v, "name") ?? MspWire.Required(v, "id"), Category = MspWire.Text(v, "category"), IsBoolean = type == "boolean" };
            if (option.IsBoolean) option.BooleanValue = v.GetProperty("currentValue").GetBoolean();
            else { option.CurrentValue = MspWire.Required(v, "currentValue"); option.Choices = Choices(MspWire.Field(v, "options")).ToArray(); }
            values.Add(option);
        }
        // Validate transport budgets before either use or publication; unknown future option types are not offered as controls.
        var result = values.ToArray();
        new HarnessSessionConfiguration { SessionId = "validation", Modes = [], Options = result }.Validate();
        return result;
    }
    private static IEnumerable<HarnessSessionChoice> Choices(JsonElement choices, string group = null, int depth = 0)
    {
        if (choices.ValueKind != JsonValueKind.Array || choices.GetArrayLength() > 64 || depth > 4) throw new AcpConfigurationException();
        foreach (var value in choices.EnumerateArray())
        {
            if (value.TryGetProperty("options", out var nested))
            { foreach (var child in Choices(nested, MspWire.Text(value, "name") ?? MspWire.Text(value, "group"), depth + 1)) yield return child; }
            else yield return new HarnessSessionChoice { Value = MspWire.Required(value, "value"), Name = MspWire.Text(value, "name") ?? MspWire.Text(value, "label") ?? MspWire.Required(value, "value"), Group = group };
        }
    }
}
