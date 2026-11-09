using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed partial class AcpHarnessAdapter
{
    private readonly SemaphoreSlim _historyGate = new(2, 2);
    internal async Task<HarnessNativeHistory> LoadOwnedHistoryAsync(HarnessTask task, CancellationToken cancellation)
    {
        string key = ContextKey(task);
        using var lease = _contextOperations.TryEnter(key) ?? throw new InvalidOperationException("Native context is busy; load its history after current work finishes.");
        var record = _index.Get(key);
        if (record?.SessionId == null) throw new InvalidOperationException("No confirmed native context identity exists for this task.");
        if (!await _historyGate.WaitAsync(0, cancellation)) throw new InvalidOperationException("Native history loading is busy.");
        try
        {
            using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            limit.CancelAfter(TimeSpan.FromSeconds(15));
            await using var probe = new AcpSession(_configuration);
            var history = await probe.LoadHistoryAsync(task.WorkspaceKey, record.SessionId, () => _index.Fence(key), limit.Token);
            // A successful explicit load restores our pre-load certainty, including any older unresolved operation.
            if (history.Supported) _index.Put(key, record);
            history.Uncertain = record.Uncertain;
            return history;
        }
        finally { _historyGate.Release(); }
    }
}
