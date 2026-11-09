using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>A captured host scope owns replay and revision observations. Runs serialize without sharing writers.</summary>
    public sealed class HarnessTaskObserver
    {
        private readonly RemoteTaskClient _client;
        private readonly SubmissionJournal _journal;
        private readonly Dictionary<string, long> _revisions = new Dictionary<string, long>();
        private readonly SemaphoreSlim _owner = new SemaphoreSlim(1, 1);

        public HarnessTaskObserver(RemoteTaskClient client, SubmissionJournal journal)
        { _client = client ?? throw new ArgumentNullException(nameof(client)); _journal = journal ?? throw new ArgumentNullException(nameof(journal)); }

        public async Task RunAsync(Action<HarnessTask> changed, Action<Exception> report, TimeSpan interval, CancellationToken cancellation)
        {
            if (changed == null) throw new ArgumentNullException(nameof(changed));
            if (interval <= TimeSpan.Zero || interval > TimeSpan.FromSeconds(30)) throw new ArgumentOutOfRangeException(nameof(interval));
            bool entered = false;
            try
            {
                await _owner.WaitAsync(cancellation); entered = true;
                while (!cancellation.IsCancellationRequested)
                {
                    try
                    {
                        await _journal.ReplayAsync(_client.SubmitAsync, cancellation);
                        var tasks = await _client.ListAsync(cancellation);
                        foreach (var task in tasks)
                        {
                            cancellation.ThrowIfCancellationRequested();
                            if (_revisions.TryGetValue(task.Id, out var revision) && revision == task.Revision) continue;
                            _revisions[task.Id] = task.Revision; changed(task);
                        }
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
                    catch (Exception error)
                    { if (cancellation.IsCancellationRequested) return; report?.Invoke(error); }
                    await Task.Delay(interval, cancellation);
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            finally { if (entered) _owner.Release(); }
        }
    }
}
