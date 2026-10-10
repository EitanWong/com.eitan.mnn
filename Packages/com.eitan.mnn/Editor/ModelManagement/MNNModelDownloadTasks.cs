using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor
{
    public enum MNNDownloadState
    {
        Downloading,
        Pausing,
        Paused,
        Cancelling,
        Cancelled,
        Completed,
        Failed
    }

    [Serializable]
    public sealed class MNNModelDownloadJob
    {
        public string Id;
        public string ModelScopeId;
        public string DisplayName;
        public string StagingPath;
        public string InstalledPath;
        public MNNDownloadState State;
        public long ReceivedBytes;
        public long TotalBytes;
        public string CurrentFile;
        public string Error;
        [NonSerialized]
        public double BytesPerSecond;
        [NonSerialized]
        internal int ProgressId = -1;
        [NonSerialized]
        internal CancellationTokenSource Cancellation;
        [NonSerialized]
        internal Task Execution;
        [NonSerialized]
        internal long PendingBytes;
        [NonSerialized]
        internal long PendingTotal;
        [NonSerialized]
        internal string PendingFile;
        [NonSerialized]
        internal long LastBytes;
        [NonSerialized]
        internal double LastSample;
        [NonSerialized]
        internal readonly object Sync = new object ();
        public bool IsRunning => State == MNNDownloadState.Downloading || State == MNNDownloadState.Pausing || State == MNNDownloadState.Cancelling;
        public bool CanResume => State == MNNDownloadState.Paused || State == MNNDownloadState.Failed;
        public float Progress => TotalBytes > 0 ? Mathf.Clamp01((float)((double)ReceivedBytes / TotalBytes)) : 0;
    }

    /// <summary>Owns downloads independently of editor windows. Unity APIs are called only on the editor thread.</summary>
    [FilePath("Library/MNN/DownloadTasks.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class MNNModelDownloadTasks : ScriptableSingleton<MNNModelDownloadTasks>
    {
        public const string InstallMarker = ".mnn-modelscope-installed";
        [SerializeField]
        private List<MNNModelDownloadJob> _jobs = new List<MNNModelDownloadJob>();
        private MNNModelDownloadService _service;
        private bool _unloading;
        private double _lastUpdate;
        public IReadOnlyList<MNNModelDownloadJob> Jobs => _jobs;
        public event Action Changed;
        private void OnEnable()
        {
            _unloading = false;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeUnload;
            EditorApplication.quitting += BeforeUnload;
            foreach (var job in _jobs)
            {
                // Managed C# transfers cannot survive a domain reload. Restore their checkpoints as resumable tasks.
                if (job.IsRunning)
                    job.State = MNNDownloadState.Paused;
                job.PendingBytes = job.ReceivedBytes;
                job.PendingTotal = job.TotalBytes;
                job.PendingFile = job.CurrentFile;
                if (job.CanResume)
                    RegisterProgress(job);
            }
        }

        public bool HasJobFor(string modelScopeId) => _jobs.Any(job => job.ModelScopeId == modelScopeId && (job.IsRunning || job.CanResume));
        public MNNModelDownloadJob StartDownload(string modelScopeId, string displayName, string stagingPath, string installedPath)
        {
            var existing = _jobs.FirstOrDefault(job => job.ModelScopeId == modelScopeId && (job.IsRunning || job.CanResume));
            if (existing != null)
            {
                if (existing.CanResume)
                    Resume(existing);
                return existing;
            }

            var staging = Path.GetFullPath(stagingPath);
            var installed = Path.GetFullPath(installedPath);
            if (_jobs.Any(job => (job.IsRunning || job.CanResume) && (job.InstalledPath == installed || job.StagingPath == staging)))
                throw new IOException("Another download already uses this destination.");
            var job = new MNNModelDownloadJob{Id = Guid.NewGuid().ToString("N"), ModelScopeId = modelScopeId, DisplayName = displayName, StagingPath = staging, InstalledPath = installed, State = MNNDownloadState.Downloading};
            _jobs.Add(job);
            Begin(job);
            return job;
        }

        public void Pause(MNNModelDownloadJob job)
        {
            if (job.State != MNNDownloadState.Downloading)
                return;
            job.State = MNNDownloadState.Pausing;
            job.Cancellation?.Cancel();
            Notify();
        }

        public void Resume(MNNModelDownloadJob job)
        {
            if (!job.CanResume || job.Execution != null)
                return;
            Begin(job);
        }

        public void Cancel(MNNModelDownloadJob job)
        {
            if (job.State == MNNDownloadState.Completed || job.State == MNNDownloadState.Cancelled)
                return;
            job.State = MNNDownloadState.Cancelling;
            if (job.Execution != null)
                job.Cancellation?.Cancel();
            else
                FinishCancellation(job);
            Notify();
        }

        public void Dismiss(MNNModelDownloadJob job)
        {
            if (job.IsRunning || job.CanResume)
                return;
            if (Progress.Exists(job.ProgressId))
                Progress.Remove(job.ProgressId);
            _jobs.Remove(job);
            Notify();
        }

        private void Begin(MNNModelDownloadJob job)
        {
            job.State = MNNDownloadState.Downloading;
            job.Error = null;
            job.BytesPerSecond = 0;
            job.LastSample = 0;
            job.Cancellation = new CancellationTokenSource();
            RegisterProgress(job);
            // One shared service bounds all simultaneous models to four HTTP connections.
            if (_service == null)
                _service = new MNNModelDownloadService(options: new MNNModelDownloadOptions{PreservePartialDownloads = true, MaxConnections = 4});
            job.Execution = Task.Run(() => TransferAsync(job, job.Cancellation.Token));
            Notify();
        }

        // This method runs on a worker. It must never change editor state or raise UI events.
        private async Task TransferAsync(MNNModelDownloadJob job, CancellationToken token)
        {
            ValidateDestination(job);
            await _service.DownloadRepositoryAsync(job.ModelScopeId, job.StagingPath, (received, total, file) =>
            {
                lock (job.Sync)
                {
                    job.PendingBytes = received;
                    job.PendingTotal = total;
                    job.PendingFile = file;
                }
            }, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
        }

        // Called only by EditorApplication.update after the worker has stopped writing files.
        private void CompleteJob(MNNModelDownloadJob job)
        {
            try
            {
                job.Execution.GetAwaiter().GetResult();
                job.Cancellation.Token.ThrowIfCancellationRequested();
                ValidateDestination(job);
                File.WriteAllText(Path.Combine(job.StagingPath, InstallMarker), job.ModelScopeId);
                Directory.CreateDirectory(Path.GetDirectoryName(job.InstalledPath));
                var backup = job.InstalledPath + ".mnn-backup";
                var hadInstall = Directory.Exists(job.InstalledPath);
                if (hadInstall)
                    Directory.Move(job.InstalledPath, backup);
                try
                {
                    Directory.Move(job.StagingPath, job.InstalledPath);
                }
                catch
                {
                    if (hadInstall)
                        Directory.Move(backup, job.InstalledPath);
                    throw;
                }

                if (hadInstall)
                    Directory.Delete(backup, true);
                job.State = MNNDownloadState.Completed;
                if (Progress.Exists(job.ProgressId))
                    Progress.Finish(job.ProgressId);
                AssetDatabase.Refresh();
            }
            catch (Exception error)
            {
                if (job.State == MNNDownloadState.Cancelling)
                    FinishCancellation(job);
                else if (job.State == MNNDownloadState.Pausing || error is OperationCanceledException)
                {
                    job.State = MNNDownloadState.Paused;
                    if (Progress.Exists(job.ProgressId))
                        Progress.Pause(job.ProgressId);
                }
                else
                {
                    job.State = MNNDownloadState.Failed;
                    job.Error = error.Message;
                    if (Progress.Exists(job.ProgressId))
                    {
                        Progress.SetDescription(job.ProgressId, error.Message);
                        Progress.Finish(job.ProgressId, Progress.Status.Failed);
                    }

                    Debug.LogWarning($"[MNN] Download stopped for {job.ModelScopeId}: {error.Message}. Partial files are retained for retry.");
                }
            }
            finally
            {
                job.Execution = null;
                job.Cancellation.Dispose();
                job.Cancellation = null;
                job.BytesPerSecond = 0;
            }
        }

        private static void ValidateDestination(MNNModelDownloadJob job)
        {
            if (Directory.Exists(job.InstalledPath) && (!File.Exists(Path.Combine(job.InstalledPath, InstallMarker)) || File.ReadAllText(Path.Combine(job.InstalledPath, InstallMarker)) != job.ModelScopeId))
                throw new IOException("The destination exists and is not managed by this model manager: " + job.InstalledPath);
            if (Directory.Exists(job.InstalledPath + ".mnn-backup"))
                throw new IOException("A previous installation backup needs to be recovered: " + job.InstalledPath + ".mnn-backup");
        }

        private void FinishCancellation(MNNModelDownloadJob job)
        {
            try
            {
                if (Directory.Exists(job.StagingPath))
                    Directory.Delete(job.StagingPath, true);
                job.State = MNNDownloadState.Cancelled;
                job.ReceivedBytes = job.PendingBytes = 0;
                if (Progress.Exists(job.ProgressId))
                    Progress.Finish(job.ProgressId, Progress.Status.Canceled);
            }
            catch (Exception error)
            {
                job.State = MNNDownloadState.Failed;
                job.Error = error.Message;
                if (Progress.Exists(job.ProgressId))
                    Progress.Finish(job.ProgressId, Progress.Status.Failed);
            }
        }

        private void RegisterProgress(MNNModelDownloadJob job)
        {
            if (Progress.Exists(job.ProgressId))
                Progress.Remove(job.ProgressId);
            job.ProgressId = Progress.Start("MNN · " + job.DisplayName, MNNLocalization.Get("download." + job.State.ToString().ToLowerInvariant()), Progress.Options.Sticky);
            Progress.SetPriority(job.ProgressId, Progress.Priority.Low);
            Progress.RegisterCancelCallback(job.ProgressId, () =>
            {
                Cancel(job);
                return true;
            });
            Progress.RegisterPauseCallback(job.ProgressId, paused =>
            {
                if (paused)
                {
                    if (job.State == MNNDownloadState.Paused)
                        return true;
                    Pause(job);
                    return true;
                }

                if (!job.CanResume || job.Execution != null)
                    return false;
                // Defer startup until Unity finishes changing the existing indicator's state.
                EditorApplication.delayCall += () => Resume(job);
                return true;
            });
            Progress.Report(job.ProgressId, job.Progress);
            if (job.State == MNNDownloadState.Paused)
                Progress.Pause(job.ProgressId);
        }

        private void Tick()
        {
            if (_unloading)
                return;
            var now = EditorApplication.timeSinceStartup;
            if (now - _lastUpdate < 0.2)
                return;
            _lastUpdate = now;
            var completed = false;
            foreach (var job in _jobs.ToArray())
            {
                if (!job.IsRunning)
                    continue;
                lock (job.Sync)
                {
                    job.ReceivedBytes = job.PendingBytes;
                    job.TotalBytes = job.PendingTotal;
                    job.CurrentFile = job.PendingFile;
                }

                if (job.LastSample > 0 && job.State == MNNDownloadState.Downloading)
                    job.BytesPerSecond = Math.Max(0, job.ReceivedBytes - job.LastBytes) / (now - job.LastSample);
                job.LastSample = now;
                job.LastBytes = job.ReceivedBytes;
                if (job.Execution?.IsCompleted == true)
                {
                    CompleteJob(job);
                    completed = true;
                    continue;
                }

                if (Progress.Exists(job.ProgressId))
                    Progress.Report(job.ProgressId, job.Progress, MNNLocalization.Get("download." + job.State.ToString().ToLowerInvariant()) + " · " + job.CurrentFile);
            }

            if (completed)
                Save(true);
            Changed?.Invoke();
        }

        private void Notify()
        {
            Save(true);
            Changed?.Invoke();
        }

        private void BeforeUnload()
        {
            _unloading = true;
            foreach (var job in _jobs)
            {
                if (job.IsRunning)
                {
                    job.State = MNNDownloadState.Paused;
                    job.Cancellation?.Cancel();
                }

                if (Progress.Exists(job.ProgressId))
                    Progress.Remove(job.ProgressId);
            }

            Save(true);
        }

        private void OnDisable()
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeUnload;
            EditorApplication.quitting -= BeforeUnload;
        }
    }
}
