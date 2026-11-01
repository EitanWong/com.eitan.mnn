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
    public sealed partial class MNNModelDownloadTasks
    {
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
            Action<long, long, string> progress = (received, total, file) =>
            {
                lock (job.Sync)
                {
                    job.PendingBytes = received;
                    job.PendingTotal = total;
                    job.PendingFile = file;
                }
            };
            await _service.DownloadAsync(job.Provider, job.RepositoryId, job.StagingPath, job.Revision, progress, token).ConfigureAwait(false);
        }

        // Called only by EditorApplication.update after the worker has stopped writing files.
        private void CompleteJob(MNNModelDownloadJob job)
        {
            try
            {
                job.Execution.GetAwaiter().GetResult();
                job.Cancellation.Token.ThrowIfCancellationRequested();
                ValidateDestination(job);
                File.WriteAllText(Path.Combine(job.StagingPath, InstallMarker), job.Provider == MNNModelProvider.ModelScope ? job.RepositoryId : "hf:" + job.RepositoryId + "@" + job.Revision);
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
                if (job.Provider == MNNModelProvider.HuggingFace && MNNCosyVoiceModel.IsRepository(job.RepositoryId))
                {
                    try
                    {
                        File.Delete(Path.Combine(job.InstalledPath, MNNCosyVoiceModel.Archive));
                    }
                    catch (IOException)
                    { /* Optional cleanup: the published model remains valid. */
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                }

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

        internal static void InstallDownloadedArchives(string stagingPath, MNNModelProvider provider, string repositoryId, CancellationToken token = default)
        {
            MNNModelDownloader.PrepareArchives(stagingPath, (MNNModelSource)provider, repositoryId, token);
        }

        private static void ValidateDestination(MNNModelDownloadJob job)
        {
            var identity = job.Provider == MNNModelProvider.ModelScope ? job.RepositoryId : "hf:" + job.RepositoryId + "@" + job.Revision;
            if (Directory.Exists(job.InstalledPath) && (!File.Exists(Path.Combine(job.InstalledPath, InstallMarker)) || File.ReadAllText(Path.Combine(job.InstalledPath, InstallMarker)) != identity && File.ReadAllText(Path.Combine(job.InstalledPath, InstallMarker)) != job.ModelScopeId))
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
    }
}
