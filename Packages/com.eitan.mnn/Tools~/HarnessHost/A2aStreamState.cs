using System.Text.Json;
using System.Text.Json.Nodes;

namespace SiriAI.HarnessHost;

/// <summary>Single stream writer merges full tasks and deltas into bounded, protocol-neutral snapshots.</summary>
internal sealed class A2aStreamState
{
    private readonly RemoteCheckpoint _binding;
    private readonly string _rpcId;
    private JsonObject _task;
    private string _id, _context;
    internal A2aStreamState(RemoteCheckpoint state, string rpcId, RemoteSnapshot seed)
    {
        _binding = state;_rpcId = rpcId;_id = state.RemoteId;_context = state.ContextId;
        if (seed?.Activity?.Artifacts != null)
        {
            EnsureTask();
            _task["artifacts"] = JsonSerializer.SerializeToNode(seed.Activity.Artifacts.Select(artifact => new
            { artifactId = artifact.Id, name = artifact.Name, description = artifact.Description, parts = artifact.Parts.Select(part => A2aWire.OutgoingPart(part, state)).ToArray() }).ToArray());
        }
    }
    internal RemoteSnapshot Apply(JsonElement root)
    {
        if (_binding.Binding == "JSONRPC")
        {
            if (A2aWire.String(root, "jsonrpc") != "2.0" || A2aWire.String(root, "id") != _rpcId || root.TryGetProperty("error", out _))
                throw new A2aStreamProtocolException("SSE JSONRPC identity or result is invalid.");
            root = root.GetProperty("result");
        }
        var value = JsonNode.Parse(root.GetRawText())?.AsObject() ?? throw new A2aStreamProtocolException("Missing A2A event.");
        string kind = A2aWire.String(root, "kind");
        if (value["task"] is JsonObject fullTask) value = (JsonObject)fullTask.DeepClone();
        else if (value["statusUpdate"] is JsonObject statusUpdate) { kind = "status-update";value = (JsonObject)statusUpdate.DeepClone(); }
        else if (value["artifactUpdate"] is JsonObject artifactUpdate) { kind = "artifact-update";value = (JsonObject)artifactUpdate.DeepClone(); }
        else if (value["message"] is JsonObject message) { kind = "message";value = (JsonObject)message.DeepClone(); }
        else if (value["msg"] is JsonObject msg) { kind = "message";value = (JsonObject)msg.DeepClone(); }
        if (kind == "message" || value["messageId"] != null)
        {
            if (_task == null && _id == null) return A2aWire.Project(JsonSerializer.SerializeToElement(new { message = value }), _binding, true);
            CheckIdentity(value["taskId"]?.GetValue<string>() ?? _id, value["contextId"]?.GetValue<string>());
            EnsureTask();_task["status"]!["message"] = value.DeepClone();
        }
        else if (kind == "status-update")
        {
            CheckIdentity(value["taskId"]?.GetValue<string>(), value["contextId"]?.GetValue<string>());
            EnsureTask();_task["status"] = value["status"]?.DeepClone() ?? throw new A2aStreamProtocolException("Missing stream task status.");
        }
        else if (kind == "artifact-update")
        {
            CheckIdentity(value["taskId"]?.GetValue<string>(), value["contextId"]?.GetValue<string>());
            EnsureTask();MergeArtifact(value);
        }
        else if (value["status"] != null && value["id"] != null)
        {
            CheckIdentity(value["id"]!.GetValue<string>(), value["contextId"]?.GetValue<string>());
            _task = (JsonObject)value.DeepClone();
        }
        else throw new A2aStreamProtocolException("Unknown A2A stream event.");
        return A2aWire.Project(JsonSerializer.SerializeToElement(_task), _binding, false);
    }
    private void CheckIdentity(string id, string context)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 128 || context?.Length > 128 ||
            (_id != null && id != _id) || (_context != null && context != null && context != _context))
            throw new A2aStreamProtocolException("A2A stream changed task/context identity.");
        _id = id;_context = context ?? _context;
        if (_task != null) _task["contextId"] = _context;
    }
    private void EnsureTask()
    {
        _task ??= new JsonObject { ["id"] = _id, ["contextId"] = _context,
            ["status"] = new JsonObject { ["state"] = A2aWire.Schema(_binding) ? "working" : "TASK_STATE_WORKING" }, ["artifacts"] = new JsonArray() };
    }
    private void MergeArtifact(JsonObject update)
    {
        var artifact = update["artifact"]?.DeepClone()?.AsObject() ?? throw new A2aStreamProtocolException("Missing stream artifact.");
        string id = artifact["artifactId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(id)) throw new A2aStreamProtocolException("Stream artifact has no identity.");
        var artifacts = _task["artifacts"]?.AsArray() ?? new JsonArray();
        if (_task["artifacts"] == null) _task["artifacts"] = artifacts;
        var previous = artifacts.OfType<JsonObject>().FirstOrDefault(item => item["artifactId"]?.GetValue<string>() == id);
        if (previous != null)
        {
            if (update["append"]?.GetValue<bool>() == true)
            {
                var merged = (JsonArray)(previous["parts"]?.DeepClone() ?? new JsonArray());
                foreach (var part in artifact["parts"]?.AsArray() ?? new JsonArray()) merged.Add(part?.DeepClone());
                artifact["parts"] = merged;
                artifact["name"] ??= previous["name"]?.DeepClone();artifact["description"] ??= previous["description"]?.DeepClone();
            }
            artifacts.Remove(previous);
        }
        artifacts.Add(artifact);
    }
}
