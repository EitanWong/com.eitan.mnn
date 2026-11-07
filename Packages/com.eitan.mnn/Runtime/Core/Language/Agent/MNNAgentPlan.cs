using System;
using UnityEngine;
namespace MNN.Unity
{
    /// <summary>A validated single action. Tools, clarifications and model routes have distinct payloads.</summary>
    public sealed class MNNAgentPlan
    {
        public string Route { get; }
        public string Tool { get; }
        public string Input { get; }
        public string Message { get; }
        private MNNAgentPlan(string route, string tool = null, string input = null, string message = null)
        { Route = route; Tool = tool; Input = input; Message = message; }
        public static MNNAgentPlan Local() => new MNNAgentPlan("local");
        public static MNNAgentPlan Cloud() => new MNNAgentPlan("cloud");
        public static MNNAgentPlan Automatic() => new MNNAgentPlan("auto");
        public static MNNAgentPlan InvokeTool(string name, string input)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 64 || string.IsNullOrWhiteSpace(input) || input.Length > 1024) throw new ArgumentException("Tool plans require a bounded name and input.");
            return new MNNAgentPlan("tool", name, input);
        }
        public static MNNAgentPlan Clarify(string question)
        {
            if (string.IsNullOrWhiteSpace(question) || question.Length > 512) throw new ArgumentException("Clarification requires a bounded question.");
            return new MNNAgentPlan("clarify", message: question);
        }
        [Serializable] private sealed class WirePlan { public string route, tool, input, message; }
        public static MNNAgentPlan Parse(string text)
        {
            if (text == null || text.Length > 2048) throw new ArgumentException("Expected a bounded routing JSON object.");
            text = text.Trim();
            if (!text.StartsWith("{", StringComparison.Ordinal) || !text.EndsWith("}", StringComparison.Ordinal)) throw new ArgumentException("Expected a routing JSON object.");
            var result = JsonUtility.FromJson<WirePlan>(text);
            if (result == null) throw new ArgumentException("Invalid routing plan.");
            if (result.route == "tool" && string.IsNullOrEmpty(result.message)) return InvokeTool(result.tool, result.input);
            if (result.route == "clarify" && string.IsNullOrEmpty(result.tool) && string.IsNullOrEmpty(result.input)) return Clarify(result.message);
            if (!string.IsNullOrEmpty(result.tool) || !string.IsNullOrEmpty(result.input) || !string.IsNullOrEmpty(result.message)) throw new ArgumentException("The route contains unrelated payload fields.");
            switch (result.route) { case "local": return Local(); case "cloud": return Cloud(); case "auto": return Automatic(); default: throw new ArgumentException("Invalid routing plan."); }
        }
    }
}
