using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public class MNNContextRequestTool : IMNNAgentTool
    {
        public string Name { get; }
        public string Description { get; }
        public bool RequiresNetwork => false;
        private readonly Func<string, string> _execute;
        private readonly SynchronizationContext _context;
        private readonly int _thread;
        public MNNContextRequestTool(string name, string description, Func<string, string> execute, SynchronizationContext context = null)
        {
            Name = name; Description = description;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _context = context ?? SynchronizationContext.Current;
            _thread = Thread.CurrentThread.ManagedThreadId;
        }
        public Task<string> ExecuteAsync(string input, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (Thread.CurrentThread.ManagedThreadId == _thread) return Task.FromResult(_execute(input));
            if (_context == null) throw new InvalidOperationException("An owning synchronization context is required for this operation.");
            return DispatchAsync(input, token);
        }
        private async Task<string> DispatchAsync(string input, CancellationToken token)
        {
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => completion.TrySetCanceled(token)))
            {
                _context.Post(state =>
                {
                    if (completion.Task.IsCompleted || token.IsCancellationRequested) return;
                    try { completion.TrySetResult(_execute(input)); }
                    catch (Exception error) { completion.TrySetException(error); }
                }, null);
                return await completion.Task.ConfigureAwait(false);
            }
        }
    }
}
