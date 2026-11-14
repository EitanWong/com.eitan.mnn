using MNN.Samples.SiriAI.Harnesses;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static async Task<HarnessTask> Terminal(PersistentTaskRuntime runtime, string id)
{
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    while (true)
    {
        var task = runtime.Get(id, "owner");
        if (task.IsTerminal) return task;
        await Task.Delay(5, deadline.Token);
    }
}

string phase = args[0];
var adapter = new EchoAdapter();
using var store = new FileTaskStore(args[1]);
using var runtime = new PersistentTaskRuntime(store, new[] { adapter });
if (phase == "run")
{
    var request = new TaskSubmission("owner", "phone", "stable-request", "echo", "workspace", "payload");
    var first = runtime.Submit(request);
    var result = await Terminal(runtime, first.Id);
    Require(result.State == HarnessTaskState.Completed && result.Result == "ECHO:payload", "Result mismatch");
    Require(runtime.Submit(request).Id == first.Id && adapter.Runs == 1, "Submission replayed");
    try { runtime.Get(first.Id, "other-owner"); throw new InvalidOperationException("Owner boundary missing"); }
    catch (UnauthorizedAccessException) { }
    Console.WriteLine("PASS package-only task execution, submission identity and owner boundary");
    runtime.Submit(new TaskSubmission("owner", "phone", "uncertain-request", "echo", "workspace", "hold"));
    await adapter.Holding.Task.WaitAsync(TimeSpan.FromSeconds(10));
    Console.WriteLine("PASS uncertain running task persisted before abrupt process exit");
    Console.Out.Flush();
    Environment.Exit(0);
}
else if (phase == "recover")
{
    var tasks = runtime.List("owner");
    Require(tasks.Length == 2 && adapter.Runs == 0, "Recovery replayed work");
    var done = tasks.Single(task => task.Objective == "payload");
    var uncertain = tasks.Single(task => task.Objective == "hold");
    Require(done.Result == "ECHO:payload" && done.State == HarnessTaskState.Completed, "Durable result lost");
    Require(uncertain.State == HarnessTaskState.Interrupted && uncertain.Result.Contains("not replayed"), "Uncertainty not retained");
    Console.WriteLine("PASS completed result survives restart and uncertain side effects never replay");
    Require(runtime.ClaimResult("other-owner", "phone", "other", TimeSpan.FromSeconds(30)) == null, "Foreign result leaked");
    var claimed = runtime.ClaimResult("owner", "phone", "reconnected-phone", TimeSpan.FromSeconds(30));
    Require(claimed.Id == done.Id && claimed.Result == "ECHO:payload", "Foreground result mismatch");
    runtime.AcknowledgeResult(claimed.Id, "owner", claimed.LeaseId, ResultReceipt.TextDisplayed);
    claimed = runtime.ClaimResult("owner", "phone", "reconnected-phone", TimeSpan.FromSeconds(30));
    Require(claimed.Id == uncertain.Id, "Interrupted result lost");
    runtime.AcknowledgeResult(claimed.Id, "owner", claimed.LeaseId, ResultReceipt.TextDisplayed);
    Console.WriteLine("PASS reconnected foreground claims exact owner-scoped results and persists receipts");
}
else if (phase == "verify")
{
    Require(adapter.Runs == 0 && runtime.List("owner").Length == 2, "Unexpected replay");
    Require(runtime.ClaimResult("owner", "phone", "phone-after-second-restart", TimeSpan.FromSeconds(30)) == null, "Receipt lost");
    Console.WriteLine("PASS presentation receipts survive another process restart");
}
else throw new ArgumentException("Unknown phase");

sealed class EchoAdapter : IHarnessAdapter
{
    public string Id => "echo";
    public HarnessCapabilities Capabilities => HarnessCapabilities.None;
    public int Runs;
    public TaskCompletionSource<bool> Holding { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<HarnessOutcome> RunAsync(HarnessTask task, Action<HarnessActivity> activity, CancellationToken stopping)
    {
        Interlocked.Increment(ref Runs);
        if (task.Objective == "hold")
        {
            Holding.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, stopping);
        }
        return HarnessOutcome.Completed("ECHO:" + task.Objective);
    }
    public Task<bool> CancelAsync(HarnessTask task, CancellationToken cancellation) => Task.FromResult(false);
    public Task RespondAsync(HarnessTask task, string requestId, string reply, bool allow, CancellationToken cancellation) => throw new NotSupportedException();
    public Task UpdateAsync(HarnessTask task, string instruction, CancellationToken cancellation) => throw new NotSupportedException();
}
