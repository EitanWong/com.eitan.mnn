using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;

namespace MNN.Unity
{
    /// <summary>One serialized language stage. Owns local/cloud providers and optional transport; tools and policy delegates are borrowed. The caller serializes requests and waits for completion before disposal.</summary>
    public sealed partial class MNNAgentProvider : IMNNConversationVoiceIntentProvider, IMNNConversationProviderDiagnostics, IMNNConversationResponseProvider, IMNNConversationEventProvider
    {
        private readonly IMNNConversationTextProvider _local;
        private readonly MNNAgentOptions _config;
        private readonly Func<IMNNConversationTextProvider> _createCloud;
        private readonly Func<string, CancellationToken, Task<bool>> _router;
        private readonly Dictionary<string, IMNNAgentTool> _tools = new Dictionary<string, IMNNAgentTool>(StringComparer.Ordinal);
        private readonly string _instructions;
        private readonly IDisposable _transport;
        private IMNNConversationTextProvider _cloud;
        private bool _cloudPrepared, _releaseStarted, _disposed;
        private List<IDisposable> _releasedResources;
        private string _route = "Hybrid · not requested";
        public MNNRealtimeCapabilities Capabilities => _local.Capabilities;
        public string RuntimeBackend => Volatile.Read(ref _route) + "\n" +
            (_local is IMNNConversationProviderDiagnostics diagnostics ? diagnostics.RuntimeBackend : "Local language provider");
        private readonly Func<string, MNNAgentPlan> _policy;
        private readonly Func<string> _planningInstructions;
        public MNNAgentProvider(IMNNConversationTextProvider local, MNNAgentOptions config,
            Func<IMNNConversationTextProvider> cloud, IEnumerable<IMNNAgentTool> tools = null,
            Func<string, CancellationToken, Task<bool>> router = null, string instructions = "", IDisposable transport = null,
            Func<string, MNNAgentPlan> policy = null, Func<string> planningInstructions = null)
        {
            _local = local ?? throw new ArgumentNullException(nameof(local));
            _config = (config ?? throw new ArgumentNullException(nameof(config))).Snapshot();
            if (_config.Strategy == MNNAgentRoutingStrategy.Policy && policy == null) throw new ArgumentException("Policy routing requires an explicit application policy.");
            _policy = policy; _planningInstructions = planningInstructions;
            _createCloud = cloud; _router = router; _instructions = instructions ?? ""; _transport = transport;
            foreach (var tool in tools ?? Array.Empty<IMNNAgentTool>())
            {
                if (tool == null || string.IsNullOrWhiteSpace(tool.Name) || tool.Name.Length > 64 ||
                    string.IsNullOrWhiteSpace(tool.Description) || tool.Description.Length > 2048 || _tools.ContainsKey(tool.Name))
                    throw new ArgumentException("Hybrid tools require unique bounded names and descriptions.");
                _tools.Add(tool.Name, tool);
            }
        }
        public Task PrepareAsync(CancellationToken token)
        {
            RequireActive();
            return _local.PrepareAsync(token);
        }
        public Task GenerateResponseAsync(IReadOnlyList<MNNOpenAIMessage> history, string instructions,
            Func<string, Task> delta, CancellationToken token) => GenerateCoreAsync(history, 16000, instructions, delta, token);

        public Task GenerateEventResponseAsync(IReadOnlyList<MNNOpenAIMessage> history, MNNConversationEvent input,
            Func<string, Task> delta, CancellationToken token)
        {
            RequireActive();
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (delta == null) throw new ArgumentNullException(nameof(delta));
            token.ThrowIfCancellationRequested();
            var prompt = input.CreateHistory(history, _config.LocalContextCharacters);
            SetRoute("Local event response");
            return _local.GenerateAsync(prompt, null, 16000, value => { token.ThrowIfCancellationRequested(); return delta(value); }, token);
        }

        public Task GenerateAsync(IReadOnlyList<MNNOpenAIMessage> history, float[] audio, int sampleRate,
            Func<string, Task> delta, CancellationToken token)
        {
            if (audio != null) throw new ArgumentException("Hybrid language routing accepts ASR text only.");
            return GenerateCoreAsync(history, sampleRate, _instructions, delta, token);
        }

        private async Task GenerateCoreAsync(IReadOnlyList<MNNOpenAIMessage> history, int sampleRate, string instructions,
            Func<string, Task> delta, CancellationToken token)
        {
            RequireActive();
            if (delta == null) throw new ArgumentNullException(nameof(delta));
            if (instructions == null || instructions.Length > 32768) throw new ArgumentException("Response instructions exceed their budget.");
            token.ThrowIfCancellationRequested();
            var current = CurrentRequest(history);
            Func<string, Task> publish = value => { token.ThrowIfCancellationRequested(); return delta(value); };
            if (current.Length > _config.LocalContextCharacters && _config.Strategy == MNNAgentRoutingStrategy.ModelPlanner)
            {
                await GenerateCloudAsync(history, instructions, publish, token).ConfigureAwait(false);
                return;
            }
            var plan = await PlanAsync(history, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            if (plan.Route == "clarify")
            {
                SetRoute("Clarify");
                await publish(string.IsNullOrWhiteSpace(plan.Message) ? "Please clarify what you need and provide the missing details." : plan.Message).ConfigureAwait(false);
                return;
            }
            if (plan.Route == "tool")
            {
                await ExecuteToolAsync(plan, instructions, publish, token).ConfigureAwait(false);
                return;
            }
            bool cloud = plan.Route == "cloud";
            if (plan.Route == "auto" && _config.UseRouter)
            {
                if (!_config.AllowCloud) { await DeniedAsync(publish).ConfigureAwait(false); return; }
                if (_router == null) { await RouterUnavailableAsync(publish).ConfigureAwait(false); return; }
                try { cloud = await _router(current, token).ConfigureAwait(false); }
                catch (Exception) when (!token.IsCancellationRequested)
                { await RouterUnavailableAsync(publish).ConfigureAwait(false); return; }
            }
            if (cloud) { await GenerateCloudAsync(history, instructions, publish, token).ConfigureAwait(false); return; }
            SetRoute("Local");
            var localHistory = new List<MNNOpenAIMessage>(LocalContext(history));
            if (!string.IsNullOrEmpty(instructions)) localHistory.Insert(0, new MNNOpenAIMessage(MNNChatRole.System, instructions));
            await _local.GenerateAsync(localHistory, null, sampleRate, publish, token).ConfigureAwait(false);
        }

        private async Task ExecuteToolAsync(MNNAgentPlan plan, string instructions, Func<string, Task> publish, CancellationToken token)
        {
            if (!_tools.TryGetValue(plan.Tool ?? "", out var tool))
            { SetRoute("Unavailable tool"); await publish("That operation is not configured. Please choose an available task.").ConfigureAwait(false); return; }
            if (tool.RequiresNetwork && !_config.AllowCloud && !(tool is IMNNAgentNetworkPermission permission && permission.CanUseNetwork))
            { await DeniedAsync(publish).ConfigureAwait(false); return; }
            SetRoute(tool.RequiresNetwork ? "Network tool · " + tool.Name : "Local tool · " + tool.Name);
            if (tool is MNNLocalKnowledgeTool knowledge && _config.KnowledgeAnswers)
            {
                await GenerateKnowledgeAsync(knowledge, plan.Input, instructions, publish, token).ConfigureAwait(false);
                return;
            }
            string result;
            try
            {
                result = await tool.ExecuteAsync(plan.Input ?? "", token).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(result) || result.Length > 8192)
                    throw new InvalidOperationException("Tool returned an invalid result.");
            }
            catch (Exception) when (!token.IsCancellationRequested)
            { result = "The tool could not complete that request. Please check the details or try again."; }
            await publish(result).ConfigureAwait(false);
        }

        public Task ClassifyVoiceIntentAsync(IReadOnlyList<MNNOpenAIMessage> prompt, Func<string, Task> delta, CancellationToken token)
        {
            RequireActive();
            return _local is IMNNConversationVoiceIntentProvider classifier ? classifier.ClassifyVoiceIntentAsync(prompt, delta, token) :
                _local.GenerateAsync(prompt, null, 16000, delta, token);
        }

        private void RequireActive()
        {
            if (_releaseStarted) throw new ObjectDisposedException(nameof(MNNAgentProvider));
        }

        private void SetRoute(string route) => Volatile.Write(ref _route, "Hybrid · " + route);
        private Task DeniedAsync(Func<string, Task> publish)
        { SetRoute("Cloud disabled"); return publish("Cloud processing is disabled. Enable it to use this capability, or choose a task that can run locally."); }
        private Task RouterUnavailableAsync(Func<string, Task> publish)
        { SetRoute("Router unavailable"); return publish("The request router is unavailable. Please try again or choose an explicit local task."); }

        public void Dispose()
        {
            if (_disposed) return;
            _releaseStarted = true;
            if (_releasedResources == null) _releasedResources = new List<IDisposable>(3);
            var attempted = new List<IDisposable>(3);
            List<Exception> failures = null;
            ReleaseOwned(_cloud, attempted, ref failures);
            ReleaseOwned(_local, attempted, ref failures);
            ReleaseOwned(_transport, attempted, ref failures);
            if (_cloud != null && ContainsReference(_releasedResources, _cloud)) _cloud = null;
            if (failures != null) throw new AggregateException("Hybrid provider cleanup failed; failed resources remain owned for retry.", failures);
            _disposed = true;
            _releasedResources = null;
        }

        private void ReleaseOwned(IDisposable resource, List<IDisposable> attempted, ref List<Exception> failures)
        {
            if (resource == null || ContainsReference(_releasedResources, resource) || ContainsReference(attempted, resource)) return;
            attempted.Add(resource);
            try { resource.Dispose(); _releasedResources.Add(resource); }
            catch (Exception error) { (failures ?? (failures = new List<Exception>())).Add(error); }
        }

        private static bool ContainsReference(List<IDisposable> resources, IDisposable resource)
        {
            foreach (var item in resources)
                if (ReferenceEquals(item, resource)) return true;
            return false;
        }
    }
}
