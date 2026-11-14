using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Claims, renews and acknowledges results through application presentation ports.
    /// Await preserves the caller's context. Stopping delivery never cancels backend work.</summary>
    public sealed class HarnessResultFeedback
    {
        private readonly Func<HarnessResultConnection> _connection;
        private readonly IHarnessResultPresenter _presenter;
        private readonly Action<Exception> _report;
        private readonly string _clientId = Guid.NewGuid().ToString("N");
        private readonly SemaphoreSlim _owner = new SemaphoreSlim(1, 1);

        public HarnessResultFeedback(Func<HarnessResultConnection> connection, IHarnessResultPresenter presenter,
            Action<Exception> report = null)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
            _presenter = presenter ?? throw new ArgumentNullException(nameof(presenter));
            _report = report;
        }

        public async Task RunAsync(CancellationToken cancellation)
        {
            bool entered = false;
            try
            {
                await _owner.WaitAsync(cancellation); entered = true;
                while (!cancellation.IsCancellationRequested)
                {
                    try
                    {
                        var connection = _connection();
                        if (connection != null && _presenter.CanPresent)
                        {
                            var task = await RpcAsync(ct => connection.Client.ClaimAsync(new ResultClaimRequest {
                                ConversationId = connection.ConversationId, ClientId = _clientId, LeaseSeconds = 60 }, ct), cancellation);
                            if (task != null) await DeliverAsync(connection, task, cancellation);
                        }
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
                    catch (Exception error) { _report?.Invoke(error); }
                    await Task.Delay(500, cancellation);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            finally { if (entered) _owner.Release(); }
        }

        private bool IsCurrent(HarnessResultConnection connection)
        {
            var current = _connection();
            return current != null && ReferenceEquals(current.Client, connection.Client) &&
                current.ConversationId == connection.ConversationId;
        }

        private async Task DeliverAsync(HarnessResultConnection connection, HarnessTask task, CancellationToken token)
        {
            var client = connection.Client;
            IHarnessResultReply reply = null;
            using (var production = CancellationTokenSource.CreateLinkedTokenSource(token))
            try
            {
                if (!IsCurrent(connection)) return;
                string data = "Task: " + task.Id + "\nState: " + task.State +
                    "\nResult: " + Bounded(task.Result ?? task.Activity ?? task.State.ToString(), 6000) +
                    HarnessArtifactPresentation.Summary(task.Artifacts);
                reply = _presenter.TryBeginReply(task.Id, data, production.Token);
                if (reply == null) return;
                await ObserveReplyAsync(connection, task, reply, _presenter.UsesSpeech, token);
            }
            finally
            {
                production.Cancel();
                try
                {
                    if (reply != null)
                    {
                        if (!reply.PlaybackCompleted && !reply.TextDisplayed) await _presenter.InterruptAsync(reply);
                        await reply.Completion;
                    }
                }
                finally
                {
                    using (var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
                    {
                        try { await client.ReleaseAsync(task.Id, task.LeaseId, cleanup.Token); }
                        catch (Exception) { /* The host reclaims disconnected or expired delivery leases. */ }
                    }
                }
            }
        }

        private sealed class Receipts { internal bool Context, Started; }

        private async Task ObserveReplyAsync(HarnessResultConnection connection, HarnessTask task,
            IHarnessResultReply reply, bool speech, CancellationToken token)
        {
            var client = connection.Client;
            var receipts = new Receipts();
            long deadline = task.LeaseUntilUtcTicks;
            var renewed = Stopwatch.StartNew();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (!IsCurrent(connection) || DateTime.UtcNow.Ticks >= deadline) return;
                try
                {
                    if (renewed.Elapsed.TotalSeconds >= 20)
                    {
                        var lease = await RpcAsync(ct => client.RenewAsync(task.Id, task.LeaseId, 60, ct), token);
                        deadline = lease.LeaseUntilUtcTicks; renewed.Restart();
                    }
                    await RecordProgressAsync(client, task, reply, receipts, token);
                    if (await RecordTerminalAsync(client, task, reply, speech, receipts.Started, token)) return;
                }
                catch (TaskHostRequestException error) when (!error.Retryable) { throw; }
                catch (Exception error) when (!token.IsCancellationRequested)
                { _report?.Invoke(error); await Task.Delay(1000, token); }
                await Task.Delay(100, token);
            }
        }

        private static async Task RecordProgressAsync(RemoteTaskClient client, HarnessTask task,
            IHarnessResultReply reply, Receipts receipts, CancellationToken token)
        {
            if (reply.ContextInjected && !receipts.Context)
            { await ReceiptAsync(client, task, ResultReceipt.ContextInjected, token); receipts.Context = true; }
            if (reply.PlaybackStarted && !receipts.Started)
            { await ReceiptAsync(client, task, ResultReceipt.PlaybackStarted, token); receipts.Started = true; }
        }

        private static bool IsInterrupted(IHarnessResultReply reply) =>
            reply.Interrupted || reply.Completion.IsCompleted && !reply.Succeeded;

        private static async Task<bool> RecordTerminalAsync(RemoteTaskClient client, HarnessTask task,
            IHarnessResultReply reply, bool speech, bool started, CancellationToken token)
        {
            if (speech && reply.PlaybackCompleted && started)
            { await ReceiptAsync(client, task, ResultReceipt.PlaybackCompleted, token); return true; }
            if (!speech && reply.TextDisplayed)
            { await ReceiptAsync(client, task, ResultReceipt.TextDisplayed, token); return true; }
            if (!IsInterrupted(reply)) return false;
            if (started) await ReceiptAsync(client, task, ResultReceipt.Interrupted, token);
            return true;
        }

        private static Task<HarnessTask> ReceiptAsync(RemoteTaskClient client, HarnessTask task, ResultReceipt receipt, CancellationToken token) =>
            RpcAsync(ct => client.AcknowledgeAsync(task.Id, new ResultReceiptRequest { LeaseId = task.LeaseId, Receipt = receipt }, ct), token);

        private static async Task<HarnessTask> RpcAsync(Func<CancellationToken, Task<HarnessTask>> send, CancellationToken token)
        {
            using (var request = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                request.CancelAfter(TimeSpan.FromSeconds(5));
                return await send(request.Token);
            }
        }

        private static string Bounded(string text, int length)
        {
            text = text ?? "";
            return text.Length <= length ? text : text.Substring(0, char.IsHighSurrogate(text[length - 1]) ? length - 1 : length) +
                " [truncated; full result remains on the host]";
        }
    }
}
