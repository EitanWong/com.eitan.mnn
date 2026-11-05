using System.Threading.Tasks;
using MNN.Unity;
using NUnit.Framework;

namespace MNN.Examples.Tests
{
    /// <summary>Required to associate each standalone-soak measurement with a newly completed turn.</summary>
    public sealed class MNNStandaloneTelemetryTests
    {
        [Test]
        public void ReobservingOneTurnDoesNotPublishAnotherCompletion()
        {
            var telemetry = new MNNConversationTelemetry();
            var turn = new MNNConversationTelemetry.Trace();
            telemetry.Complete(turn, turn.AcceptedAt + 100);
            double first = telemetry.Latest.TotalMs;
            telemetry.Complete(turn, turn.AcceptedAt + 200);
            Assert.AreEqual(1L, telemetry.CompletedCount);
            Assert.AreEqual(first, telemetry.Latest.TotalMs);
        }

        [Test]
        public void CompletedTurnSequenceSurvivesTheRollingTimingWindow()
        {
            var telemetry = new MNNConversationTelemetry();
            Assert.AreEqual(0L, telemetry.CompletedCount);
            for (int i = 0; i < 100; ++i)
            {
                var turn = new MNNConversationTelemetry.Trace { Route = "turn-" + i };
                telemetry.Complete(turn, turn.AcceptedAt + 100);
            }
            Assert.AreEqual(100L, telemetry.CompletedCount);
            Assert.AreEqual("turn-99", telemetry.Latest.Route);
        }

        [Test]
        public void ConcurrentPresentationObserversCountOneTurnOnce()
        {
            var telemetry = new MNNConversationTelemetry();
            var turn = new MNNConversationTelemetry.Trace();
            var tasks = new Task[8];
            for (int i = 0; i < tasks.Length; ++i) tasks[i] = Task.Run(() => telemetry.Complete(turn));
            Task.WaitAll(tasks);
            Assert.AreEqual(1L, telemetry.CompletedCount);
        }
    }
}
