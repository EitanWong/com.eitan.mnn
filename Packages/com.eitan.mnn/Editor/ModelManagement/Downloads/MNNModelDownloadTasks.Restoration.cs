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
                if (job.Provider == MNNModelProvider.HuggingFace && MNNVerifiedTtsModel.IsHuggingFaceRepository(job.RepositoryId))
                {
                    try
                    {
                        job.Revision = MNNVerifiedTtsModel.ResolveHuggingFaceRevision(job.RepositoryId, job.Revision);
                    }
                    catch (ArgumentException error)
                    {
                        job.State = MNNDownloadState.Failed;
                        job.Error = error.Message;
                    }
                }

                if (job.Provider == MNNModelProvider.HuggingFace && MNNBertEmbeddingModel.IsRepository(job.RepositoryId))
                {
                    try
                    {
                        job.Revision = MNNBertEmbeddingModel.ResolveRevision(job.RepositoryId, job.Revision);
                    }
                    catch (ArgumentException error)
                    {
                        job.State = MNNDownloadState.Failed;
                        job.Error = error.Message;
                    }
                }

                if (job.Provider == MNNModelProvider.HuggingFace && MNNPaddleTextModel.IsRepository(job.RepositoryId))
                {
                    try
                    {
                        job.Revision = MNNPaddleTextModel.ResolveRevision(job.Revision);
                    }
                    catch (ArgumentException error)
                    {
                        job.State = MNNDownloadState.Failed;
                        job.Error = error.Message;
                    }
                }

                if (job.Provider == MNNModelProvider.HuggingFace && MNNReferenceSpeechModel.IsRepository(job.RepositoryId))
                {
                    try
                    {
                        job.Revision = MNNReferenceSpeechModel.ResolveRevision(job.RepositoryId, job.Revision);
                    }
                    catch (ArgumentException error)
                    {
                        job.State = MNNDownloadState.Failed;
                        job.Error = error.Message;
                    }
                }

                if (job.Provider == MNNModelProvider.HuggingFace && MNNWhisperModel.IsRepository(job.RepositoryId) || job.Provider == MNNModelProvider.ModelScope && MNNWhisperModel.IsModelScopeRepository(job.RepositoryId))
                {
                    try
                    {
                        job.Revision = MNNWhisperModel.ResolveRevision(job.Revision, job.RepositoryId);
                    }
                    catch (ArgumentException error)
                    {
                        job.State = MNNDownloadState.Failed;
                        job.Error = error.Message;
                    }
                }

                if (job.Provider == MNNModelProvider.HuggingFace && MNNQwen3AsrModel.IsRepository(job.RepositoryId) || job.Provider == MNNModelProvider.ModelScope && MNNQwen3AsrModel.IsModelScopeRepository(job.RepositoryId))
                {
                    try
                    {
                        job.Revision = MNNQwen3AsrModel.ResolveRevision(job.Revision, job.RepositoryId);
                    }
                    catch (ArgumentException error)
                    {
                        job.State = MNNDownloadState.Failed;
                        job.Error = error.Message;
                    }
                }

                if (job.Provider == MNNModelProvider.HuggingFace && MNNParaformerModel.IsHuggingFaceRepository(job.RepositoryId) || job.Provider == MNNModelProvider.ModelScope && MNNParaformerModel.IsModelScopeRepository(job.RepositoryId))
                {
                    try
                    {
                        job.Revision = MNNParaformerModel.ResolveRevision(job.Revision, job.RepositoryId, job.Provider == MNNModelProvider.ModelScope);
                    }
                    catch (ArgumentException error)
                    {
                        job.State = MNNDownloadState.Failed;
                        job.Error = error.Message;
                    }
                }

                if (job.Provider == MNNModelProvider.HuggingFace && MNNKwsModel.IsRepository(job.RepositoryId))
                {
                    try
                    {
                        job.Revision = MNNKwsModel.ResolveRevision(job.Revision);
                    }
                    catch (ArgumentException error)
                    {
                        job.State = MNNDownloadState.Failed;
                        job.Error = error.Message;
                    }
                }

                if (job.Provider == MNNModelProvider.HuggingFace && MNNSileroModel.IsRepository(job.RepositoryId))
                {
                    try
                    {
                        job.Revision = MNNSileroModel.ResolveRevision(job.Revision);
                    }
                    catch (ArgumentException error)
                    {
                        job.State = MNNDownloadState.Failed;
                        job.Error = error.Message;
                    }
                }

                if (job.Provider == MNNModelProvider.HuggingFace && MNNSmartTurnModel.IsRepository(job.RepositoryId))
                {
                    try
                    {
                        job.Revision = MNNSmartTurnModel.ResolveRevision(job.Revision);
                    }
                    catch (ArgumentException error)
                    {
                        job.State = MNNDownloadState.Failed;
                        job.Error = error.Message;
                    }
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
