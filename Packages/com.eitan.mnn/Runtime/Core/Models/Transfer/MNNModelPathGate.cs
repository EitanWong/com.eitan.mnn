using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    // Register every waiter before awaiting; remove only after the last holder/waiter departs.
    internal static class MNNModelPathGate
    {
        private sealed class Entry { internal readonly SemaphoreSlim Semaphore = new SemaphoreSlim(1, 1); internal int Users; }
        private sealed class Lease : IDisposable
        {
            private string _path; private Entry _entry;
            internal Lease(string path, Entry entry) { _path = path; _entry = entry; }
            public void Dispose()
            {
                var entry = Interlocked.Exchange(ref _entry, null);
                if (entry == null) return;
                entry.Semaphore.Release(); Remove(_path, entry);
            }
        }
        private static readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        internal static int ActivePathCount { get { lock (Entries) return Entries.Count; } }
        internal static async Task<IDisposable> EnterAsync(string path, CancellationToken token)
        {
            path = Path.GetFullPath(path);
            int rootLength = Path.GetPathRoot(path).Length;
            while (path.Length > rootLength && (path[path.Length - 1] == Path.DirectorySeparatorChar ||
                path[path.Length - 1] == Path.AltDirectorySeparatorChar)) path = path.Substring(0, path.Length - 1);
            Entry entry;
            lock (Entries)
            {
                if (!Entries.TryGetValue(path, out entry)) Entries.Add(path, entry = new Entry());
                ++entry.Users;
            }
            try { await entry.Semaphore.WaitAsync(token).ConfigureAwait(false); return new Lease(path, entry); }
            catch { Remove(path, entry); throw; }
        }
        private static void Remove(string path, Entry entry)
        {
            lock (Entries)
            {
                if (--entry.Users != 0) return;
                Entries.Remove(path); entry.Semaphore.Dispose();
            }
        }
    }
}
