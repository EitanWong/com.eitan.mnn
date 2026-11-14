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
        private void OnEnable()
        {
            _unloading = false;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeUnload;
            EditorApplication.quitting += BeforeUnload;
            foreach (var job in _jobs)
            {
                if (string.IsNullOrWhiteSpace(job.RepositoryId))
                    job.RepositoryId = job.ModelScopeId;
                if (string.IsNullOrWhiteSpace(job.Revision))
                    job.Revision = job.Provider == MNNModelProvider.ModelScope ? "master" : "main";
                try
                {
                    job.Revision = MNNModelRevisions.Resolve((MNNModelSource)job.Provider, job.RepositoryId, job.Revision);
                }
                catch (ArgumentException error)
                {
                    job.State = MNNDownloadState.Failed;
                    job.Error = error.Message;
                }

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
    }
}
