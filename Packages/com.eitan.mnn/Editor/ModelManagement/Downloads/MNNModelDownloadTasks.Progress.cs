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
    }
}
