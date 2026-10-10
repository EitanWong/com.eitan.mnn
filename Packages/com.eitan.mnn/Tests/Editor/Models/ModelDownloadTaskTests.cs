using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class ModelDownloadTaskTests
    {
        private string _root;
        private MNNModelDownloadTasks _manager;
        private MNNModelDownloadJob _job;
        private MNNModelDownloadService _previousService;
        private MNNModelDownloadService _service;
        private HttpClient _client;
        private Action _onChanged;
        private DownloadThreadTestWindow _window;
        private static readonly FieldInfo ServiceField = typeof(MNNModelDownloadTasks).GetField("_service", BindingFlags.Instance | BindingFlags.NonPublic);
        [SetUp]
        public void SetUp()
        {
            _job = null;
            _service = null;
            _client = null;
            _onChanged = null;
            _window = null;
            _root = Path.Combine(Path.GetFullPath("TestArtifacts~/MNNValidation"), "MNNTaskTests", Guid.NewGuid().ToString("N"));
            _manager = MNNModelDownloadTasks.instance;
            Assert.IsFalse(System.Linq.Enumerable.Any(_manager.Jobs, job => job.IsRunning), "Run background task tests in a project without active model downloads.");
            _previousService = (MNNModelDownloadService)ServiceField.GetValue(_manager);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_onChanged != null)
                _manager.Changed -= _onChanged;
            if (_window != null)
                _window.Close();
            if (_job != null)
            {
                if (_job.IsRunning || _job.CanResume)
                    _manager.Cancel(_job);
                yield return WaitUntil(() => !_job.IsRunning);
                _manager.Dismiss(_job);
            }

            ServiceField.SetValue(_manager, _previousService);
            _service?.Dispose();
            _client?.Dispose();
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }

        [UnityTest]
        public IEnumerator WorkerCompletion_NotifiesAndRepaintsOnlyOnMainThread()
        {
            var mainThread = Thread.CurrentThread.ManagedThreadId;
            var workerThread = 0;
            var notifications = 0;
            var wrongThread = false;
            _window = ScriptableObject.CreateInstance<DownloadThreadTestWindow>();
            _window.ShowUtility();
            _onChanged = () =>
            {
                wrongThread |= Thread.CurrentThread.ManagedThreadId != mainThread;
                notifications++;
                _window.Repaint();
            };
            _manager.Changed += _onChanged;
            InstallService((request, token) =>
            {
                workerThread = Thread.CurrentThread.ManagedThreadId;
                return Task.FromResult(ModelResponse());
            });
            StartJob();
            yield return WaitUntil(() => !_job.IsRunning);
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
            Assert.AreNotEqual(mainThread, workerThread, "The transfer should actually run on a worker.");
            Assert.IsFalse(wrongThread, "A UI notification escaped the editor thread.");
            Assert.That(notifications, Is.GreaterThan(1));
            Assert.AreEqual(3, _job.ReceivedBytes);
            CollectionAssert.AreEqual(new byte[]{1, 2, 3}, File.ReadAllBytes(Path.Combine(_job.InstalledPath, "model.mnn")));
            Assert.IsTrue(File.Exists(Path.Combine(_job.InstalledPath, MNNModelDownloadTasks.InstallMarker)));
        }

        [UnityTest]
        public IEnumerator ClosingWindow_DoesNotCancelBackgroundDownload()
        {
            var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            InstallService(async (request, token) =>
            {
                started.TrySetResult(true);
                using (token.Register(() => response.TrySetCanceled()))
                    return await response.Task.ConfigureAwait(false);
            });
            _window = ScriptableObject.CreateInstance<DownloadThreadTestWindow>();
            _window.ShowUtility();
            _onChanged = () => _window.Repaint();
            _manager.Changed += _onChanged;
            StartJob();
            yield return WaitUntil(() => started.Task.IsCompleted);
            _manager.Changed -= _onChanged;
            _onChanged = null;
            _window.Close();
            _window = null;
            Assert.AreEqual(MNNDownloadState.Downloading, _job.State);
            response.SetResult(ModelResponse());
            yield return WaitUntil(() => !_job.IsRunning);
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
        }

        [UnityTest]
        public IEnumerator PauseThenResume_CompletesWithoutThreadErrors()
        {
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var attempts = 0;
            InstallService(async (request, token) =>
            {
                if (Interlocked.Increment(ref attempts) == 1)
                {
                    started.TrySetResult(true);
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                }

                return ModelResponse();
            });
            StartJob();
            yield return WaitUntil(() => started.Task.IsCompleted);
            _manager.Pause(_job);
            yield return WaitUntil(() => !_job.IsRunning);
            Assert.AreEqual(MNNDownloadState.Paused, _job.State, _job.Error);
            Assert.IsTrue(Directory.Exists(_job.StagingPath));
            _manager.Resume(_job);
            yield return WaitUntil(() => !_job.IsRunning);
            Assert.AreEqual(MNNDownloadState.Completed, _job.State, _job.Error);
        }

        [UnityTest]
        public IEnumerator Cancel_StopsWorkerBeforeDeletingStaging()
        {
            var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            InstallService(async (request, token) =>
            {
                started.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                return ModelResponse();
            });
            StartJob();
            yield return WaitUntil(() => started.Task.IsCompleted);
            _manager.Cancel(_job);
            yield return WaitUntil(() => !_job.IsRunning);
            Assert.AreEqual(MNNDownloadState.Cancelled, _job.State, _job.Error);
            Assert.IsFalse(Directory.Exists(_job.StagingPath));
            Assert.IsFalse(Directory.Exists(_job.InstalledPath));
        }

        private void InstallService(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> factory)
        {
            _client = new HttpClient(new Handler(factory));
            _service = new MNNModelDownloadService(_client, "https://unit.test", new MNNModelDownloadOptions{PreservePartialDownloads = true, RetryCount = 0});
            ServiceField.SetValue(_manager, _service);
        }

        private void StartJob() => _job = _manager.StartDownload("MNN/test-" + Guid.NewGuid().ToString("N"), "Test model", Path.Combine(_root, "staging"), Path.Combine(_root, "installed"));
        private static HttpResponseMessage ModelResponse() => new HttpResponseMessage(HttpStatusCode.OK)
        {Content = new ByteArrayContent(new byte[]{1, 2, 3})};
        private static IEnumerator WaitUntil(Func<bool> condition)
        {
            var timeout = DateTime.UtcNow.AddSeconds(20);
            while (!condition() && DateTime.UtcNow < timeout)
                yield return null;
            Assert.IsTrue(condition(), "Background task did not settle within 20 seconds.");
        }

        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _factory;
            public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> factory)
            {
                _factory = factory;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                if (request.RequestUri.AbsolutePath.EndsWith("/repo/files"))
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {Content = new StringContent("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"model.mnn\",\"Type\":\"blob\",\"Size\":3,\"Revision\":\"test-commit\"}]}}")});
                return _factory(request, token);
            }
        }
    }

    public sealed class DownloadThreadTestWindow : EditorWindow
    {
    }
}
