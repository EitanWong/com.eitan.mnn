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
    /// <summary>Owns downloads independently of editor windows. Unity APIs are called only on the editor thread.</summary>
    [FilePath("Library/MNN/DownloadTasks.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed partial class MNNModelDownloadTasks : ScriptableSingleton<MNNModelDownloadTasks>
    {
        public const string InstallMarker = ".mnn-modelscope-installed";
        [SerializeField]
        private List<MNNModelDownloadJob> _jobs = new List<MNNModelDownloadJob>();
        private MNNModelDownloadService _service;
        private bool _unloading;
        private double _lastUpdate;
        public IReadOnlyList<MNNModelDownloadJob> Jobs => _jobs;
        public event Action Changed;
        public bool HasJobFor(string modelScopeId) => _jobs.Any(job => job.ModelScopeId == modelScopeId && (job.IsRunning || job.CanResume));
        public bool HasJobFor(MNNModelInfo model)
        {
            var repositoryId = model?.repositoryId ?? model?.modelScopeId;
            return _jobs.Any(job => job.Provider == model?.provider && job.RepositoryId == repositoryId && (job.IsRunning || job.CanResume));
        }

        public MNNModelDownloadJob StartDownload(string modelScopeId, string displayName, string stagingPath, string installedPath)
        {
            return StartDownload(new MNNModelInfo{provider = MNNModelProvider.ModelScope, repositoryId = modelScopeId, modelScopeId = modelScopeId, revision = "master", name = displayName, displayName = displayName}, stagingPath, installedPath);
        }

        public MNNModelDownloadJob StartDownload(MNNModelInfo model, string stagingPath, string installedPath)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            var repositoryId = string.IsNullOrWhiteSpace(model.repositoryId) ? model.modelScopeId : model.repositoryId;
            if (string.IsNullOrWhiteSpace(repositoryId))
                throw new ArgumentException("The model has no repository ID.", nameof(model));
            var existing = _jobs.FirstOrDefault(job => job.Provider == model.provider && job.RepositoryId == repositoryId && (job.IsRunning || job.CanResume));
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
            var job = new MNNModelDownloadJob{Id = Guid.NewGuid().ToString("N"), Provider = model.provider, RepositoryId = repositoryId, Revision = string.IsNullOrWhiteSpace(model.revision) ? (model.provider == MNNModelProvider.ModelScope ? "master" : "main") : model.revision, ModelScopeId = model.modelScopeId ?? (model.provider == MNNModelProvider.ModelScope ? repositoryId : null), DisplayName = model.displayName ?? model.name ?? repositoryId, StagingPath = staging, InstalledPath = installed, State = MNNDownloadState.Downloading};
            if (job.Provider == MNNModelProvider.HuggingFace && MNNCosyVoiceModel.IsRepository(repositoryId) && job.Revision == "main")
                job.Revision = MNNCosyVoiceModel.Revision;
            if (job.Provider == MNNModelProvider.HuggingFace && MNNVerifiedTtsModel.IsHuggingFaceRepository(repositoryId))
                job.Revision = MNNVerifiedTtsModel.ResolveHuggingFaceRevision(repositoryId, job.Revision);
            if (job.Provider == MNNModelProvider.HuggingFace && MNNBertEmbeddingModel.IsRepository(repositoryId))
                job.Revision = MNNBertEmbeddingModel.ResolveRevision(repositoryId, job.Revision);
            if (job.Provider == MNNModelProvider.HuggingFace && MNNPaddleTextModel.IsRepository(repositoryId))
                job.Revision = MNNPaddleTextModel.ResolveRevision(job.Revision);
            if (job.Provider == MNNModelProvider.HuggingFace && MNNReferenceSpeechModel.IsRepository(repositoryId))
                job.Revision = MNNReferenceSpeechModel.ResolveRevision(repositoryId, job.Revision);
            if (job.Provider == MNNModelProvider.HuggingFace && MNNWhisperModel.IsRepository(repositoryId) || job.Provider == MNNModelProvider.ModelScope && MNNWhisperModel.IsModelScopeRepository(repositoryId))
                job.Revision = MNNWhisperModel.ResolveRevision(job.Revision, job.RepositoryId);
            if (job.Provider == MNNModelProvider.HuggingFace && MNNQwen3AsrModel.IsRepository(repositoryId) || job.Provider == MNNModelProvider.ModelScope && MNNQwen3AsrModel.IsModelScopeRepository(repositoryId))
                job.Revision = MNNQwen3AsrModel.ResolveRevision(job.Revision, job.RepositoryId);
            if (job.Provider == MNNModelProvider.HuggingFace && MNNParaformerModel.IsHuggingFaceRepository(repositoryId) || job.Provider == MNNModelProvider.ModelScope && MNNParaformerModel.IsModelScopeRepository(repositoryId))
                job.Revision = MNNParaformerModel.ResolveRevision(job.Revision, repositoryId, job.Provider == MNNModelProvider.ModelScope);
            if (job.Provider == MNNModelProvider.HuggingFace && MNNKwsModel.IsRepository(repositoryId))
                job.Revision = MNNKwsModel.ResolveRevision(job.Revision);
            if (job.Provider == MNNModelProvider.HuggingFace && MNNSileroModel.IsRepository(repositoryId))
                job.Revision = MNNSileroModel.ResolveRevision(job.Revision);
            if (job.Provider == MNNModelProvider.HuggingFace && MNNSmartTurnModel.IsRepository(repositoryId))
                job.Revision = MNNSmartTurnModel.ResolveRevision(job.Revision);
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
