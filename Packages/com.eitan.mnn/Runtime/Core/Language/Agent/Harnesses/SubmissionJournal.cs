using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Persist an invocation before transmission. Reconnect replays identities, never objective text deduplication.</summary>
    public sealed class SubmissionJournal : IDisposable
    {
        private readonly string _path;
        private readonly FileStream _writer;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private Dictionary<string, SubmitTaskRequest> _pending;
        private int _count;
        private bool _disposed;
        public int PendingCount => Volatile.Read(ref _count);

        public SubmissionJournal(string directory)
        {
            Directory.CreateDirectory(directory);
            _path = Path.Combine(Path.GetFullPath(directory), "submissions.json");
            _writer = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try
            {
                var requests = File.Exists(_path) ? TaskHostJson.Read<SubmitTaskRequest[]>(ReadFile()) : new SubmitTaskRequest[0];
                if (requests == null || requests.Length > 256) throw new IOException("Invalid submission journal.");
                _pending = new Dictionary<string, SubmitTaskRequest>(StringComparer.Ordinal);
                foreach (var request in requests) { Validate(request);var snapshot = Copy(request);_pending.Add(Key(snapshot), snapshot); }
                _count = _pending.Count;
            }
            catch { _writer.Dispose(); throw; }
        }

        private byte[] ReadFile()
        {
            if (new FileInfo(_path).Length > 1024 * 1024) throw new IOException("Submission journal exceeds 1 MiB.");
            return File.ReadAllBytes(_path);
        }

        public async Task<HarnessTask> SubmitAsync(SubmitTaskRequest request,
            Func<SubmitTaskRequest, CancellationToken, Task<HarnessTask>> send, CancellationToken cancellation)
        {
            Validate(request);
            if (send == null) throw new ArgumentNullException(nameof(send));
            // Snapshot before the first await; callers do not share mutable requests with the journal.
            request = Copy(request);
            await _gate.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                RequireOpen();
                string key = Key(request);
                SubmitTaskRequest pending;
                if (_pending.TryGetValue(key, out pending))
                {
                    if (!TaskHostJson.Write(pending).SequenceEqual(TaskHostJson.Write(request)))
                        throw new ArgumentException("An invocation identity cannot be reused for a different task.");
                }
                else
                {
                    if (_pending.Count >= 256) throw new IOException("Resolve pending admissions before creating more work.");
                    var next = new Dictionary<string, SubmitTaskRequest>(_pending, StringComparer.Ordinal) { [key] = request };
                    Commit(next);
                }
                return await SendPendingAsync(request, send, cancellation).ConfigureAwait(false);
            }
            finally { _gate.Release(); }
        }

        public async Task ReplayAsync(Func<SubmitTaskRequest, CancellationToken, Task<HarnessTask>> send, CancellationToken cancellation)
        {
            if (send == null) throw new ArgumentNullException(nameof(send));
            await _gate.WaitAsync(cancellation).ConfigureAwait(false);
            try
            {
                RequireOpen();
                foreach (var request in _pending.Values.ToArray())
                    await SendPendingAsync(request, send, cancellation).ConfigureAwait(false);
            }
            finally { _gate.Release(); }
        }

        private async Task<HarnessTask> SendPendingAsync(SubmitTaskRequest request,
            Func<SubmitTaskRequest, CancellationToken, Task<HarnessTask>> send, CancellationToken cancellation)
        {
            HarnessTask result;
            try { result = await send(Copy(request), cancellation).ConfigureAwait(false); }
            catch (TaskHostRequestException error) when (!error.Retryable)
            {
                Remove(request);
                throw;
            }
            catch (Exception error) when (!(error is OperationCanceledException))
            { throw new PendingSubmissionException(request.SubmissionKey, error); }
            if (result == null || string.IsNullOrEmpty(result.Id) || result.SubmissionKey != request.SubmissionKey ||
                result.ConversationId != request.ConversationId)
                throw new IOException("Host admission did not confirm the submitted identity.");
            Remove(request);
            return result;
        }

        private void Remove(SubmitTaskRequest request)
        {
            var next = new Dictionary<string, SubmitTaskRequest>(_pending, StringComparer.Ordinal);
            next.Remove(Key(request));
            Commit(next);
        }

        private void Commit(Dictionary<string, SubmitTaskRequest> next)
        {
            byte[] bytes = TaskHostJson.Write(next.Values.ToArray());
            if (bytes.Length > 1024 * 1024) throw new IOException("Submission journal exceeds 1 MiB.");
            string temporary = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { output.Write(bytes, 0, bytes.Length); output.Flush(true); }
                if (File.Exists(_path)) File.Replace(temporary, _path, null);
                else File.Move(temporary, _path);
                _pending = next;
                Volatile.Write(ref _count, next.Count);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static string Key(SubmitTaskRequest request) => request.ConversationId + "\n" + request.SubmissionKey;
        private static SubmitTaskRequest Copy(SubmitTaskRequest r) => new SubmitTaskRequest
        { ConversationId = r.ConversationId, SubmissionKey = r.SubmissionKey, HarnessId = r.HarnessId, WorkspaceKey = r.WorkspaceKey, Objective = r.Objective, Attachments = HarnessContentPart.CheckedInputs(r.Attachments) };
        private static void Validate(SubmitTaskRequest r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));
            new TaskSubmission("foreground", r.ConversationId, r.SubmissionKey, r.HarnessId, r.WorkspaceKey, r.Objective, r.Attachments);
            if (r.ConversationId.Contains("\n") || r.SubmissionKey.Contains("\n")) throw new ArgumentException("Invalid invocation identity.");
        }
        private void RequireOpen() { if (_disposed) throw new ObjectDisposedException(nameof(SubmissionJournal)); }
        public async Task DisposeAsync()
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try { if (!_disposed) { _disposed = true; _writer.Dispose(); } }
            finally { _gate.Release(); }
        }
        public void Dispose() => DisposeAsync().GetAwaiter().GetResult();
    }
}
