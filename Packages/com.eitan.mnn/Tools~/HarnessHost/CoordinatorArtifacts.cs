using System.Text;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Copy exact output bytes; models cannot choose, replace or acknowledge child artifacts.</summary>
internal static class CoordinatorArtifacts
{
    internal static HarnessOutcome Collect(HarnessTask parent, HarnessOutcome initial, HarnessOutcome result, HarnessTask[] children)
    {
        children = children.OrderBy(t => t.CreatedUtcTicks).ThenBy(t => t.Id, StringComparer.Ordinal).ToArray();
        try
        {
            var own = Phase(initial.Artifacts, "initial").Concat(Phase(result.Artifacts, "result")).ToArray();
            var artifacts = HarnessCollectedResult.MergeArtifacts(own, children);
            var collected = children.Select(HarnessCollectedResult.Capture).ToArray();
            var snapshot = parent.Copy(); snapshot.Artifacts = artifacts; snapshot.Result = result.Text;
            snapshot.State = result.State; snapshot.CollectedResults = collected;
            if (TaskHostJson.Write(snapshot).Length > 900000) throw new ArgumentException("Serialized collection exceeds the foreground response budget.");
            var outcome = result.WithArtifacts(artifacts);
            return result.State == HarnessTaskState.Cancelled ? outcome : outcome.WithCollectedResults(collected);
        }
        catch (ArgumentException)
        {
            return HarnessOutcome.Failed("Coordinator artifact collection exceeded its count, byte or foreground response budget. No child result was consumed. Full outputs remain on original child tasks: " + string.Join(", ", children.Select(t => t.Id)));
        }
    }
    private static IEnumerable<HarnessArtifact> Phase(HarnessArtifact[] artifacts, string phase) => artifacts.Select(artifact =>
        new HarnessArtifact("coordinator-" + phase + "-" + HarnessCollectedResult.Hash(Encoding.UTF8.GetBytes(artifact.Id)),
            artifact.Name, artifact.Description, artifact.Parts));
}
