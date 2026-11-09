using System.Threading.Channels;

namespace SiriAI.HarnessHost;

/// <summary>The reader owns delta state. A bounded latest snapshot channel isolates it from task controls.</summary>
internal sealed class A2aStreamSession : IAsyncDisposable
{
    private readonly Channel<RemoteSnapshot> _snapshots = Channel.CreateBounded<RemoteSnapshot>(new BoundedChannelOptions(1)
        { SingleReader = true, SingleWriter = true, FullMode = BoundedChannelFullMode.DropOldest });
    private readonly CancellationTokenSource _stopping;
    private readonly Task _reader;
    private RemoteSnapshot _last;
    private int _disposed;
    internal A2aStreamSession(Func<CancellationToken, Task<HttpResponseMessage>> connect, RemoteCheckpoint state, string rpcId, RemoteSnapshot seed, CancellationToken stopping)
    {
        _stopping = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        _last = seed;
        // The producer gets its own binding snapshot; it never shares the runtime's mutable checkpoint.
        var binding = System.Text.Json.JsonSerializer.Deserialize<RemoteCheckpoint>(System.Text.Json.JsonSerializer.Serialize(state));
        _reader = PumpAsync(connect, new A2aStreamState(binding, rpcId, seed), _stopping.Token);
    }
    private async Task PumpAsync(Func<CancellationToken, Task<HttpResponseMessage>> connect, A2aStreamState state, CancellationToken stopping)
    {
        Exception failure = null;
        try
        {
            using var response = await connect(stopping);
            using var input = await response.Content.ReadAsStreamAsync(stopping);
            await foreach (var data in A2aSseReader.ReadAsync(input, stopping))
            {
                var snapshot = state.Apply(data);
                _snapshots.Writer.TryWrite(snapshot);
                if (snapshot.Outcome != null) break;
            }
        }
        catch (Exception error) { failure = error; }
        finally { _snapshots.Writer.TryComplete(failure); }
    }
    internal async Task<RemoteSnapshot> FirstAsync(CancellationToken cancellation)
    {
        try { _last = await _snapshots.Reader.ReadAsync(cancellation);return _last; }
        catch (ChannelClosedException error) { throw error.InnerException ?? new IOException("A2A stream ended before confirming a task/message."); }
    }
    internal async Task<RemoteSnapshot> NextAsync()
    {
        if (_snapshots.Reader.TryRead(out var next)) { _last = next;return next; }
        if (!_snapshots.Reader.Completion.IsCompleted) return _last;
        try { await _snapshots.Reader.Completion; }
        catch (A2aStreamProtocolException) { throw; }
        catch (Exception error) when (error is IOException || error is HttpRequestException || error is OperationCanceledException) { }
        return null; // Confirmed handle survives stream loss. Read the authoritative task instead of resending work.
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) { _stopping.Cancel();await _reader;_stopping.Dispose(); }
        else await _reader;
    }
}
