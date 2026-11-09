using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class CoordinatorMcpTools
{
    // The durable scoped tasks are the ownership grant. A backend-global listing never grants execution.
    private string ResolveSession(CoordinatorMcpScope scope, CoordinatorTurn turn, string identity)
    {
        var tasks = Tasks(scope, turn);
        var matches = tasks.GroupBy(task => task.ConversationId)
            .Where(group => group.Key == identity || NativeSessionId(group, group.Key) == identity)
            .Select(group => group.Key).ToArray();
        if (matches.Length != 1) throw new UnauthorizedAccessException("Exact delegated session is unavailable or ambiguous in this scope.");
        return matches[0];
    }
    private static string NativeSessionId(IEnumerable<HarnessTask> tasks, string conversation) =>
        tasks.Where(task => task.ConversationId == conversation && task.SessionConfiguration?.SessionId != null)
            .OrderByDescending(task => task.CreatedUtcTicks).ThenByDescending(task => task.Id)
            .Select(task => task.SessionConfiguration.SessionId).FirstOrDefault();
}
