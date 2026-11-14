using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;

internal static class TaskControlConsumer
{
    public static void Main(string[] args) => Run(args).GetAwaiter().GetResult();
    private static async Task Run(string[] args)
    {
        using (var client = new RemoteTaskClient(new Uri(args[0]), Environment.GetEnvironmentVariable("SIRIAI_CONTROL_TOKEN")))
        {
            Func<HarnessTaskToolOperation, HarnessTaskTool> tool = operation =>
                new HarnessTaskTool(client, null, "controls", operation, null, null, false);
            var tasks = await client.ListAsync(CancellationToken.None);
            string text = await tool(HarnessTaskToolOperation.Status).ExecuteAsync("active", CancellationToken.None);
            if (tasks.Length != 13 || tasks.Any(task => !text.Contains(task.Id))) throw new Exception("Active list silently dropped tasks: " + text);
            Console.WriteLine("PASS all 13 active tasks remain visible through the foreground tool");
            string paused = await tool(HarnessTaskToolOperation.Pause).ExecuteAsync(args[1], CancellationToken.None);
            if (!paused.Contains("Paused")) throw new Exception(paused);
            string cancelled = await tool(HarnessTaskToolOperation.Cancel).ExecuteAsync(args[2], CancellationToken.None);
            if (!cancelled.Contains("Cancelled")) throw new Exception(cancelled);
            Console.WriteLine("PASS model tool confirms queued pause and queued cancellation using exact IDs");
        }
    }
}
