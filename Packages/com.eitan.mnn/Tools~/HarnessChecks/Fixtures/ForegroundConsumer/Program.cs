using System.Net;
using System.Net.Http;
using System.Text;
using MNN.Samples.SiriAI.Harnesses;

internal static class Program
{
    private static RemoteTaskClient Client;
    private static SubmissionJournal Journal;
    private static readonly List<Exception> Errors = new();
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static void Pass(string message) => Console.WriteLine("PASS " + message);
    private static async Task Wait(Func<Task<bool>> probe, int seconds = 12)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < until) { if (await probe()) return; await Task.Delay(30); }
        throw new Exception("Timed out waiting for consumer behavior");
    }
    private static Task Wait(Func<bool> probe, int seconds = 12) => Wait(() => Task.FromResult(probe()), seconds);
    private static HarnessTaskTool Tool(HarnessTaskToolOperation operation,
        Func<SubmitTaskRequest, CancellationToken, Task<HarnessTask>> submit = null) =>
        new(Client, submit ?? ((request, token) => Journal.SubmitAsync(request, Client.SubmitAsync, token)),
            "phone", operation, "acp", "test", false);
    private static async Task<HarnessTask> Submit(string objective)
    {
        string answer = await Tool(HarnessTaskToolOperation.Submit).ExecuteAsync(objective, CancellationToken.None);
        var task = (await Client.ListAsync(CancellationToken.None)).Last(t => t.Objective == objective);
        Check(answer.Contains("Task accepted: " + task.Id) && answer.Contains("Work continues independently"), answer);
        await Wait(async () => (await Client.GetAsync(task.Id, CancellationToken.None)).IsTerminal);
        return await Client.GetAsync(task.Id, CancellationToken.None);
    }
    private static HarnessResultFeedback Feedback(Presenter presenter, Func<bool> available = null) =>
        new(() => available == null || available() ? new HarnessResultConnection(Client, "phone") : null,
            presenter, error => { lock (Errors) Errors.Add(error); });
    private static async Task Stop(Task run, CancellationTokenSource owner) { owner.Cancel(); await run; }
    private static async Task Main(string[] args)
    {
        using var client = new RemoteTaskClient(new Uri(args[0]), Environment.GetEnvironmentVariable("MNN_PACKAGE_FOREGROUND_TOKEN")); Client = client;
        Journal = new SubmissionJournal(args[1]);
        try
        {
            await Tools(); await TextAndIdle(); await Speech(); await Interrupted();
            await RefusalAndCancellation(); await FaultedPresenter(); await ScopeChanges(); await Renewal(); await Observer();
            Check(Errors.Count == 0, string.Join("\n", Errors));
        }
        finally { await Journal.DisposeAsync(); }
    }

    private static async Task Tools()
    {
        var missing = new HarnessTaskTool(null, null, "phone", HarnessTaskToolOperation.Submit, "acp", "test", false);
        Check(!missing.CanUseNetwork && (await missing.ExecuteAsync("work", CancellationToken.None)).Contains("Connect"), "unconfigured tool");
        var task = await Submit("tools");
        Check(task.State == HarnessTaskState.Completed && task.Result == "fixture result: tools", "external result");
        int count = (await Client.ListAsync(CancellationToken.None)).Length;
        string status = await Tool(HarnessTaskToolOperation.Status).ExecuteAsync(task.Id, CancellationToken.None);
        Check(status.Contains(task.Id + " | Completed") && status.Contains(task.Result), status);
        Check((await Tool(HarnessTaskToolOperation.Status).ExecuteAsync("", CancellationToken.None)).Contains(task.Id), "list");
        Check((await Client.ListAsync(CancellationToken.None)).Length == count, "status created work");
        using var recording = new UpdateHandler();
        using var remote = new RemoteTaskClient(new Uri("http://127.0.0.1:1"), "test", recording);
        var update = new HarnessTaskTool(remote, null, "phone", HarnessTaskToolOperation.Update, null, null, false);
        var input = "{\"TaskId\":\"exact-task\",\"RequestId\":\"same-instruction\",\"Text\":\"调整要求\",\"Attachments\":[{\"Kind\":0,\"Payload\":\"note\"}]}";
        string receipt = await update.ExecuteAsync(input, CancellationToken.None);
        await update.ExecuteAsync(input, CancellationToken.None);
        Check(recording.Bodies.Count == 2 && recording.Bodies[0] == recording.Bodies[1], "update identity changed");
        var wire = TaskHostJson.Read<TaskControlRequest>(Encoding.UTF8.GetBytes(recording.Bodies[0]));
        Check(wire.AdmitOnly && wire.RequestId == "same-instruction" && wire.Text == "调整要求" && wire.Attachments[0].Payload == "note", "typed update wire");
        Check(receipt.Contains("Pending") && !receipt.Contains("Delivered"), receipt);
        try { await update.ExecuteAsync("{}", CancellationToken.None); throw new Exception("invalid update accepted"); }
        catch (ArgumentException) { }
        // Present the result this test has already read so later tests own their exact result.
        var lease = await Client.ClaimAsync(new ResultClaimRequest { ConversationId = "phone", ClientId = "test", LeaseSeconds = 60 }, CancellationToken.None);
        Check(lease.Id == task.Id, "tool result identity");
        await Client.AcknowledgeAsync(task.Id, new ResultReceiptRequest { LeaseId = lease.LeaseId, Receipt = ResultReceipt.TextDisplayed }, CancellationToken.None);
        await Client.ReleaseAsync(task.Id, lease.LeaseId, CancellationToken.None);
        Pass("package task tools submit real work, read without creating tasks, and preserve exact update identity/attachments/admission semantics");
    }

    private static async Task TextAndIdle()
    {
        var task = await Submit("text"); var presenter = new Presenter { CanPresent = false };
        bool available = false;
        using var owner = new CancellationTokenSource(); var run = Feedback(presenter, () => available).RunAsync(owner.Token);
        await Task.Delay(600); Check(presenter.Count == 0 && (await Client.GetAsync(task.Id, default)).LeaseId == null, "claimed while unavailable");
        available = true; await Task.Delay(600); Check(presenter.Count == 0 && (await Client.GetAsync(task.Id, default)).LeaseId == null, "claimed while foreground busy");
        presenter.CanPresent = true;
        await Wait(() => presenter.Count == 1); await Wait(async () => (await Client.GetAsync(task.Id, default)).ContextInjected);
        var before = await Client.GetAsync(task.Id, default);
        Check(before.ResultPending && !before.PlaybackStarted && presenter.Reply.Completion.IsCompleted, "generation consumed result");
        Check(presenter.Id == task.Id && presenter.Context.Contains("fixture result: text"), "wrong foreground event");
        presenter.Reply.TextDisplayed = true;
        await Wait(async () => { var t = await Client.GetAsync(task.Id, default); return !t.ResultPending && t.LeaseId == null; });
        await Stop(run, owner);
        Pass("injected foreground waits for connection and idle, uses exact result context, and requires actual text-display evidence");
    }

    private static async Task Speech()
    {
        var task = await Submit("speech"); var presenter = new Presenter { UsesSpeech = true };
        using var owner = new CancellationTokenSource(); var run = Feedback(presenter).RunAsync(owner.Token);
        await Wait(() => presenter.Count == 1);
        await Task.Delay(200); Check(!(await Client.GetAsync(task.Id, default)).PlaybackCompleted, "generated speech counted as heard");
        presenter.Reply.PlaybackStarted = true;
        await Wait(async () => (await Client.GetAsync(task.Id, default)).PlaybackStarted);
        Check(!(await Client.GetAsync(task.Id, default)).PlaybackCompleted, "start counted as complete");
        presenter.Reply.PlaybackCompleted = true;
        await Wait(async () => { var t = await Client.GetAsync(task.Id, default); return t.PlaybackCompleted && t.LeaseId == null; });
        await Stop(run, owner);
        Pass("speech generation and start never imply completion; only injected playback evidence persists a completed receipt");
    }

    private static async Task Interrupted()
    {
        var task = await Submit("interrupted"); var presenter = new Presenter { UsesSpeech = true };
        using var owner = new CancellationTokenSource(); var run = Feedback(presenter).RunAsync(owner.Token);
        await Wait(() => presenter.Count == 1); presenter.Reply.PlaybackStarted = true;
        await Wait(async () => (await Client.GetAsync(task.Id, default)).PlaybackStarted);
        presenter.CanPresent = false; presenter.Reply.Interrupted = true;
        await Wait(async () => { var t = await Client.GetAsync(task.Id, default); return t.PlaybackInterrupted && t.LeaseId == null; });
        await Stop(run, owner); Check(presenter.Interruptions == 1, "foreground not interrupted");
        Pass("interrupted presentation records interruption and releases delivery without cancelling the completed backend task");
    }

    private static async Task RefusalAndCancellation()
    {
        var task = await Submit("cancel-presentation"); var presenter = new Presenter { Refuse = true };
        using (var owner = new CancellationTokenSource())
        {
            var run = Feedback(presenter).RunAsync(owner.Token); await Wait(() => presenter.Count == 1); presenter.CanPresent = false;
            await Wait(async () => (await Client.GetAsync(task.Id, default)).LeaseId == null);
            await Stop(run, owner);
        }
        Check((await Client.GetAsync(task.Id, default)).ResultPending, "refusal consumed result");
        presenter = new Presenter { Reply = new Reply { ContextInjected = false, Succeeded = false, Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) } };
        using (var owner = new CancellationTokenSource())
        {
            var run = Feedback(presenter).RunAsync(owner.Token); await Wait(() => presenter.Count == 1); await Stop(run, owner);
        }
        var state = await Client.GetAsync(task.Id, default);
        Check(state.State == HarnessTaskState.Completed && state.ResultPending && state.LeaseId == null && presenter.Interruptions == 1, "presentation cancellation altered work");
        // Explicitly present now so subsequent cases have a separate result.
        presenter = new Presenter { Reply = new Reply { TextDisplayed = true } };
        using (var owner = new CancellationTokenSource())
        { var run = Feedback(presenter).RunAsync(owner.Token); await Wait(async () => !(await Client.GetAsync(task.Id, default)).ResultPending); await Stop(run, owner); }
        Pass("presenter refusal and foreground suspension preserve the pending backend result and release only delivery ownership");
    }

    private static async Task Renewal()
    {
        var task = await Submit("renewal"); var presenter = new Presenter();
        using var owner = new CancellationTokenSource(); var run = Feedback(presenter).RunAsync(owner.Token);
        await Wait(() => presenter.Count == 1);
        long original = (await Client.GetAsync(task.Id, default)).LeaseUntilUtcTicks;
        await Wait(async () => (await Client.GetAsync(task.Id, default)).LeaseUntilUtcTicks > original, 28);
        presenter.CanPresent = false; await Stop(run, owner);
        var state = await Client.GetAsync(task.Id, default);
        Check(state.ResultPending && state.LeaseId == null && !state.PlaybackStarted, "renewal fabricated presentation");
        var lease = await Client.ClaimAsync(new ResultClaimRequest { ConversationId = "phone", ClientId = "cleanup", LeaseSeconds = 60 }, default);
        await Client.AcknowledgeAsync(task.Id, new ResultReceiptRequest { LeaseId = lease.LeaseId, Receipt = ResultReceipt.TextDisplayed }, default);
        await Client.ReleaseAsync(task.Id, lease.LeaseId, default);
        Pass("long foreground production renews its actual HTTP lease and suspension releases it without false presentation");
    }

    private static async Task FaultedPresenter()
    {
        var task = await Submit("faulted-presenter");
        var presenter = new Presenter { Reply = new Reply { Succeeded = false, Pending = new(TaskCreationOptions.RunContinuationsAsynchronously) } };
        using (var owner = new CancellationTokenSource())
        {
            var run = Feedback(presenter).RunAsync(owner.Token); await Wait(() => presenter.Count == 1);
            presenter.CanPresent = false; presenter.Reply.Pending.SetException(new IOException("presentation failed"));
            await Wait(async () => (await Client.GetAsync(task.Id, default)).LeaseId == null);
            await Stop(run, owner);
        }
        lock (Errors)
        {
            Check(Errors.Count == 1 && Errors[0].Message == "presentation failed", "presentation failure not reported exactly once");
            Errors.Clear();
        }
        Check((await Client.GetAsync(task.Id, default)).ResultPending, "faulted completion consumed pending result");
        presenter = new Presenter { Reply = new Reply { TextDisplayed = true } };
        using (var owner = new CancellationTokenSource())
        { var run = Feedback(presenter).RunAsync(owner.Token); await Wait(async () => !(await Client.GetAsync(task.Id, default)).ResultPending); await Stop(run, owner); }
        Pass("faulted frontend completion still releases its HTTP lease and leaves the result pending for a later real presentation");
    }

    private static async Task ScopeChanges()
    {
        using var handler = new DelayedClaimHandler();
        using var client = new RemoteTaskClient(new Uri("http://127.0.0.1:1"), "test", handler);
        HarnessResultConnection current = new(client, "old"); var presenter = new Presenter();
        using var owner = new CancellationTokenSource();
        var run = new HarnessResultFeedback(() => current, presenter, e => Errors.Add(e)).RunAsync(owner.Token);
        await handler.ClaimStarted.Task;
        current = new(client, "new"); handler.Resume.TrySetResult();
        await Wait(() => handler.Released);
        await Stop(run, owner);
        Check(presenter.Count == 0 && !handler.Receipt, "old result entered replacement foreground scope");
        Pass("host/conversation switch during an in-flight claim releases the original lease without injecting into the new foreground");
    }

    private static async Task Observer()
    {
        var submit = Tool(HarnessTaskToolOperation.Submit, (request, token) => Journal.SubmitAsync(request,
            (_, _) => Task.FromException<HarnessTask>(new IOException("simulated lost admission")), token));
        string uncertain = await submit.ExecuteAsync("slow", default);
        Check(uncertain.Contains("Admission is unconfirmed") && Journal.PendingCount == 1, uncertain);
        var observer = new HarnessTaskObserver(Client, Journal); var seen = new HashSet<string>();
        Action<HarnessTask> changed = t => { lock (seen) Check(seen.Add(t.Id + ":" + t.Revision), "duplicate revision notification"); };
        using (var owner = new CancellationTokenSource())
        {
            var run = observer.RunAsync(changed, e => Errors.Add(e), TimeSpan.FromMilliseconds(100), owner.Token);
            await Wait(async () => (await Client.ListAsync(default)).Any(t => t.Objective == "slow"));
            await Stop(run, owner);
        }
        var task = (await Client.ListAsync(default)).Single(t => t.Objective == "slow");
        Check(uncertain.Contains(task.SubmissionKey) && Journal.PendingCount == 0, "replay changed invocation");
        await Wait(async () => (await Client.GetAsync(task.Id, default)).State == HarnessTaskState.Completed);
        using (var owner = new CancellationTokenSource())
        {
            var run = observer.RunAsync(changed, e => Errors.Add(e), TimeSpan.FromMilliseconds(100), owner.Token);
            await Wait(async () => { var current = await Client.GetAsync(task.Id, default); lock (seen) return seen.Contains(task.Id + ":" + current.Revision); });
            await Task.Delay(250); await Stop(run, owner);
        }
        string admitted = await Tool(HarnessTaskToolOperation.Submit).ExecuteAsync("slow", default);
        var cancellable = (await Client.ListAsync(default)).Last(t => t.Objective == "slow");
        Check(admitted.Contains(cancellable.Id), "cancel target identity");
        string cancelled = await Tool(HarnessTaskToolOperation.Cancel).ExecuteAsync(cancellable.Id, default);
        Check(cancelled.Contains(cancellable.Id + " | Cancelled") && (await Client.GetAsync(cancellable.Id, default)).State == HarnessTaskState.Cancelled, cancelled);
        Pass("package observer replays the original durable admission, deduplicates revisions across restarts, and local stop leaves external work running");
    }

    private sealed class Presenter : IHarnessResultPresenter
    {
        public bool CanPresent { get; set; } = true;
        public bool UsesSpeech { get; set; }
        public bool Refuse; public int Count, Interruptions; public string Id, Context;
        public Reply Reply = new();
        public IHarnessResultReply TryBeginReply(string eventId, string context, CancellationToken token)
        { Id = eventId; Context = context; Count++; return Refuse ? null : Reply; }
        public Task InterruptAsync(IHarnessResultReply reply) { Interruptions++; Reply.Pending?.TrySetResult(); return Task.CompletedTask; }
    }
    private sealed class Reply : IHarnessResultReply
    {
        public bool ContextInjected { get; set; } = true;
        public bool Succeeded { get; set; } = true;
        public bool TextDisplayed { get; set; }
        public bool PlaybackStarted { get; set; }
        public bool PlaybackCompleted { get; set; }
        public bool Interrupted { get; set; }
        public TaskCompletionSource Pending;
        public Task Completion => Pending?.Task ?? Task.CompletedTask;
    }
    private sealed class UpdateHandler : HttpMessageHandler
    {
        public List<string> Bodies = new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Check(request.RequestUri.AbsolutePath == "/v1/tasks/exact-task/update", "wrong update target");
            Bodies.Add(await request.Content.ReadAsStringAsync(token));
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"Id\":\"exact-task\",\"State\":1,\"Instructions\":[{\"Id\":\"same-instruction\",\"State\":0}]}") };
        }
    }
    private sealed class DelayedClaimHandler : HttpMessageHandler
    {
        public TaskCompletionSource ClaimStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Released, Receipt;
        private int claims;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            string path = request.RequestUri.AbsolutePath;
            if (path.EndsWith("/release")) Released = true;
            if (path.EndsWith("/receipt")) Receipt = true;
            if (path == "/v1/results/claim")
            {
                if (Interlocked.Increment(ref claims) > 1) return new(HttpStatusCode.NoContent);
                ClaimStarted.TrySetResult(); await Resume.Task.WaitAsync(token);
            }
            return new(HttpStatusCode.OK) { Content = new StringContent("{\"Id\":\"old-result\",\"ConversationId\":\"old\",\"Objective\":\"work\",\"Result\":\"done\",\"State\":3,\"LeaseId\":\"old-lease\",\"LeaseUntilUtcTicks\":" + DateTime.UtcNow.AddMinutes(1).Ticks + "}") };
        }
    }
}
