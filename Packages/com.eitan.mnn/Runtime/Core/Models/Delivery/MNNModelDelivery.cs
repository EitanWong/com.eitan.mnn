using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace MNN.Unity
{
    /// <summary>Call from Unity's main thread. Prepares verified model files before invoking any inference API.</summary>
    public sealed class MNNModelDelivery
    {
        private readonly MNNModelDeliveryManifest _manifest;
        private readonly string _streamingRoot;
        private readonly string _persistentRoot;

        public MNNModelDelivery(MNNModelDeliveryManifest manifest, string streamingAssetsRoot, string persistentModelsRoot)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            manifest.Validate();
            // Own the snapshot so callers cannot change paths while preparation is in flight.
            _manifest = JsonUtility.FromJson<MNNModelDeliveryManifest>(JsonUtility.ToJson(manifest));
            _streamingRoot = streamingAssetsRoot ?? throw new ArgumentNullException(nameof(streamingAssetsRoot));
            _persistentRoot = Path.GetFullPath(persistentModelsRoot);
        }

        public static async Task<MNNModelDelivery> LoadAsync(CancellationToken token = default)
        {
            var streaming = Application.streamingAssetsPath;
            var persistent = Path.Combine(Application.persistentDataPath, MNNModelDeliveryManifest.ModelsPath);
            var path = StreamingFile(streaming, MNNModelDeliveryManifest.StreamingPath);
            string json;
            if (!IsUrl(path)) json = await RunFileOperationAsync(() => File.ReadAllText(path), token);
            else
            {
                using (var request = UnityWebRequest.Get(path))
                {
                    request.timeout = 300;
                    await SendAsync(request, token);
                    json = request.downloadHandler.text;
                }
            }
            var manifest = JsonUtility.FromJson<MNNModelDeliveryManifest>(json);
            return new MNNModelDelivery(manifest, streaming, persistent);
        }

        public Task<string> PrepareFileAsync(string relativePath, Action<long, long> progress = null,
            CancellationToken token = default, bool copyToPersistentData = false)
        {
            MNNModelAssetDownloader.ResolvePath(_persistentRoot, relativePath);
            var file = _manifest.files.FirstOrDefault(item => item.path == relativePath);
            if (file == null) throw new FileNotFoundException("Model file is absent from the build delivery manifest: " + relativePath);
            return PrepareAsync(file, copyToPersistentData, progress, token);
        }

        /// <summary>Seeds every declared dependency into one writable directory, including files inside Android APKs.</summary>
        public async Task<string> PrepareDirectoryAsync(string relativeDirectory, Action<long, long> progress = null,
            CancellationToken token = default)
        {
            var directory = MNNModelAssetDownloader.ResolvePath(_persistentRoot, relativeDirectory);
            var prefix = relativeDirectory + "/";
            var files = _manifest.files.Where(file => file.path.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            if (files.Length == 0) throw new FileNotFoundException("No model files declared for " + relativeDirectory);
            long completed = 0, total = files.Sum(file => file.bytes);
            foreach (var file in files)
            {
                await PrepareAsync(file, true, (received, _) => progress?.Invoke(completed + received, total), token);
                completed += file.bytes;
                progress?.Invoke(completed, total);
            }
            return directory;
        }

        private async Task<string> PrepareAsync(MNNModelDeliveryFile file, bool forceCopy, Action<long, long> progress, CancellationToken token)
        {
            var destination = MNNModelAssetDownloader.ResolvePath(_persistentRoot, file.path);
            var source = StreamingFile(_streamingRoot, MNNModelDeliveryManifest.ModelsPath + "/" + file.path);
            using (var pathLease = await MNNModelPathGate.EnterAsync(destination, token))
            {
                if (await IsValidAsync(destination, file, token)) return destination;
                if (file.bundled)
                {
                    if (!forceCopy && _manifest.bundledStorage == MNNBundledModelStorage.Automatic && !IsUrl(source))
                    {
                        if (!await IsValidAsync(source, file, token)) throw new InvalidDataException("Bundled model verification failed: " + file.path);
                        return source;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        if (!IsUrl(source))
                            await RunFileOperationAsync(() =>
                            {
                                using (var input = File.OpenRead(source))
                                using (var output = File.Create(temporary))
                                {
                                    var buffer = new byte[128 * 1024];
                                    long copied = 0;
                                    int read;
                                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                                    {
                                        token.ThrowIfCancellationRequested();
                                        output.Write(buffer, 0, read);
                                        copied += read;
                                        progress?.Invoke(copied, file.bytes);
                                    }
                                }
                                return true;
                            }, token);
                        else
                        {
                            using (var request = UnityWebRequest.Get(source))
                            {
                                request.timeout = _manifest.requestTimeout;
                                request.downloadHandler = new DownloadHandlerFile(temporary) { removeFileOnAbort = true };
                                await SendAsync(request, token, progress, file.bytes);
                            }
                        }
                        if (!await IsValidAsync(temporary, file, token)) throw new InvalidDataException("Bundled model verification failed: " + file.path);
                        token.ThrowIfCancellationRequested();
                        if (File.Exists(destination)) File.Replace(temporary, destination, null);
                        else File.Move(temporary, destination);
                    }
                    finally { if (File.Exists(temporary)) File.Delete(temporary); }
                    return destination;
                }
                if (!_manifest.allowDownloads) throw new InvalidOperationException("MNN runtime downloads are disabled: " + file.path);
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(_manifest.requestTimeout));
                    pathLease.Dispose(); // The transfer re-enters the same file gate and verifies after acquiring it.
                    return await MNNModelAssetDownloader.EnsureAsync(_persistentRoot, new MNNModelAssetFile
                    {
                        path = file.path,
                        url = file.url,
                        sha256 = file.sha256,
                        bytes = file.bytes
                    }, progress, timeout.Token);
                }
            }
        }

        private static Task<bool> IsValidAsync(string path, MNNModelDeliveryFile file, CancellationToken token) => RunFileOperationAsync(() =>
        {
            token.ThrowIfCancellationRequested();
            return File.Exists(path) && new FileInfo(path).Length == file.bytes &&
                string.Equals(MNNModelDeliveryManifest.ComputeSha256(path, token), file.sha256, StringComparison.OrdinalIgnoreCase);
        }, token);

        private static Task<T> RunFileOperationAsync<T>(Func<T> operation, CancellationToken token)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL's default runtime has no managed worker threads. Its local
            // files live in the virtual filesystem; URL assets still use UWR.
            if (token.IsCancellationRequested)
                return Task.FromCanceled<T>(token);
            try { return Task.FromResult(operation()); }
            catch (Exception error) { return Task.FromException<T>(error); }
#else
            return Task.Run(operation, token);
#endif
        }

        private static bool IsUrl(string path) => path.Contains("://");

        private static string StreamingFile(string root, string relative) => IsUrl(root)
            ? root.TrimEnd('/') + "/" + string.Join("/", relative.Split('/').Select(Uri.EscapeDataString))
            : Path.Combine(root, relative);

        private static async Task SendAsync(UnityWebRequest request, CancellationToken token, Action<long, long> progress = null, long total = 0)
        {
            token.ThrowIfCancellationRequested();
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                if (token.IsCancellationRequested) { request.Abort(); token.ThrowIfCancellationRequested(); }
                progress?.Invoke((long)request.downloadedBytes, total);
                await Task.Yield();
            }
            token.ThrowIfCancellationRequested();
            if (request.result != UnityWebRequest.Result.Success) throw new IOException("MNN model request failed: " + request.error);
        }
    }
}
