using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
namespace MNN.Unity.Tests
{
    public sealed class AgentReuseTests
    {
        private sealed class Model : IMNNConversationTextProvider
        {
            internal int Prepared, Calls, Disposed;
            internal IReadOnlyList<MNNOpenAIMessage> Received;
            internal string Reply = "answer";
            public MNNRealtimeCapabilities Capabilities => new MNNRealtimeCapabilities(MNNRealtimeFeature.TextInput, "Agent fixture", "Managed behavior only", execution: MNNRealtimeExecution.Managed);
            public Task PrepareAsync(CancellationToken token) { ++Prepared; return Task.CompletedTask; }
            public Task GenerateAsync(IReadOnlyList<MNNOpenAIMessage> history, float[] audio, int rate, Func<string, Task> delta, CancellationToken token)
            { ++Calls; Received = history; return delta(Reply); }
            public void Dispose() { ++Disposed; }
        }
        private sealed class Tool : IMNNAgentTool
        {
            public string Name => "fixture";
            public string Description => "A fixture operation";
            public bool RequiresNetwork { get; set; }
            internal int Calls;
            public Task<string> ExecuteAsync(string input, CancellationToken token) { ++Calls; return Task.FromResult(input); }
        }
        private static MNNOpenAIMessage[] History(string current = "question") => new[] { new MNNOpenAIMessage(MNNChatRole.User, current) };
        private static string Ask(MNNAgentProvider agent, IReadOnlyList<MNNOpenAIMessage> history = null)
        { var text = new StringBuilder(); agent.GenerateAsync(history ?? History(), null, 16000, v => { text.Append(v); return Task.CompletedTask; }, CancellationToken.None).GetAwaiter().GetResult(); return text.ToString(); }
        [Test]
        public void ToolAndClarificationRoutesSkipModelsAndCloudCreation()
        {
            var model = new Model(); var tool = new Tool(); int created = 0;
            using (var agent = new MNNAgentProvider(model, new MNNAgentOptions { Strategy = MNNAgentRoutingStrategy.Policy }, () => { ++created; return new Model(); }, new[] { tool },
                policy: request => request == "question" ? MNNAgentPlan.InvokeTool("fixture", "verified") : MNNAgentPlan.Clarify("Which document?")))
            { Assert.AreEqual("verified", Ask(agent)); Assert.AreEqual("Which document?", Ask(agent, History("missing"))); Assert.AreEqual(1, tool.Calls); Assert.AreEqual(0, model.Calls + created); }
        }
        [Test]
        public void OptionsAreSnapshottedAndNetworkPermissionPrecedesAnyTransmission()
        {
            var options = new MNNAgentOptions { Strategy = MNNAgentRoutingStrategy.Policy }; var tool = new Tool { RequiresNetwork = true }; int created = 0;
            using (var agent = new MNNAgentProvider(new Model(), options, () => { ++created; return new Model(); }, new[] { tool }, policy: _ => MNNAgentPlan.InvokeTool("fixture", "data")))
            { options.AllowCloud = true; StringAssert.Contains("disabled", Ask(agent)); Assert.AreEqual(0, tool.Calls + created); }
        }
        [Test]
        public void CloudReceivesOnlyOptedInTextAndPreparesOnce()
        {
            var cloud = new Model(); int created = 0;
            using (var agent = new MNNAgentProvider(new Model(), new MNNAgentOptions { Strategy = MNNAgentRoutingStrategy.Policy, AllowCloud = true }, () => { ++created; return cloud; }, policy: _ => MNNAgentPlan.Cloud()))
            {
                Assert.AreEqual(0, created);
                var history = new[] { new MNNOpenAIMessage(MNNChatRole.System, "private"), new MNNOpenAIMessage(MNNChatRole.User, "old"), new MNNOpenAIMessage(new float[8], 16000), new MNNOpenAIMessage(MNNChatRole.User, "current") };
                Assert.AreEqual("answer", Ask(agent, history)); Assert.AreEqual("answer", Ask(agent, history));
                Assert.AreEqual(1, cloud.Prepared); Assert.AreEqual(1, created); Assert.AreEqual(1, cloud.Received.Count); Assert.AreEqual("current", cloud.Received[0].Text); Assert.True(cloud.Received.All(m => m.Audio == null));
            }
            Assert.AreEqual(1, cloud.Disposed);
        }
        [Test]
        public void WholeMessageContextBudgetsPreserveCommands()
        {
            var local = new Model();
            using (var agent = new MNNAgentProvider(local, new MNNAgentOptions { Strategy = MNNAgentRoutingStrategy.Policy, LocalContextCharacters = 512 }, null, policy: _ => MNNAgentPlan.Local()))
            { Ask(agent, new[] { new MNNOpenAIMessage(MNNChatRole.User, new string('x', 510)), new MNNOpenAIMessage(MNNChatRole.User, "current") }); Assert.AreEqual(1, local.Received.Count); Assert.AreEqual("current", local.Received[0].Text); }
        }
        [Test]
        public void ToolRegistrationAndPlanPayloadsRejectAmbiguousValues()
        {
            Assert.Throws<ArgumentException>(() => new MNNAgentProvider(new Model(), new MNNAgentOptions(), null, new[] { new Tool(), new Tool() }));
            Assert.Throws<ArgumentException>(() => MNNAgentPlan.InvokeTool("fixture", " "));
            Assert.Throws<ArgumentException>(() => MNNAgentPlan.Clarify(new string('x', 513)));
        }
        [Test]
        public void KnowledgeRetrievalAndArithmeticAreReusableWithoutAConversationRuntime()
        {
            var docs = new MNNLocalKnowledgeTool(new[] { new KeyValuePair<string, string>("setup", "The model folder belongs under StreamingAssets/MNN.") });
            Assert.AreEqual(1, docs.Search("model folder", CancellationToken.None).Count);
            Assert.AreEqual(0, docs.Search("unrelated", CancellationToken.None).Count);
            Assert.AreEqual("(2+3)*4 = 20", new MNNArithmeticTool().ExecuteAsync("(2+3)*4", CancellationToken.None).Result);
            Assert.Throws<ArgumentException>(() => new MNNArithmeticTool().ExecuteAsync("print(1)", CancellationToken.None));
        }
    }
}
