using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

namespace MNN.Unity
{
    public enum MNNMarkdownImageFailure { None, InvalidUrl, Download, EncodedSizeLimit, UnsupportedImage, DecodedSizeLimit, Decode, Cancelled }

    /// <summary>Bounded HTTP/file image transport. Create, poll, decode and dispose on Unity's main thread.
    /// Reception stops at the encoded limit without relying on polling or Content-Length. PNG/JPEG
    /// dimensions are checked before native decode. A successful, non-readable texture belongs to the caller.</summary>
    public sealed class MNNMarkdownImageRequest : IDisposable
    {
        public const int DefaultMaximumEncodedBytes = 16 * 1024 * 1024;
        public const long DefaultMaximumPixels = 16L * 1024 * 1024;
        private readonly int _owner = Thread.CurrentThread.ManagedThreadId;
        private readonly long _maximumPixels;
        private readonly UnityWebRequest _request;
        private readonly BoundedDownload _download;
        private bool _disposed, _taken;
        public MNNMarkdownImageFailure Failure { get; private set; }
        public long EncodedBytes => _download?.ReceivedBytes ?? 0;
        public bool IsDone { get { CheckOwner(); return _disposed || _request == null || _request.isDone; } }

        public MNNMarkdownImageRequest(string url, int maximumEncodedBytes = DefaultMaximumEncodedBytes,
            long maximumPixels = DefaultMaximumPixels, int timeoutSeconds = 20)
        {
            if (maximumEncodedBytes < 1 || maximumEncodedBytes > DefaultMaximumEncodedBytes) throw new ArgumentOutOfRangeException(nameof(maximumEncodedBytes));
            if (maximumPixels < 1 || maximumPixels > DefaultMaximumPixels) throw new ArgumentOutOfRangeException(nameof(maximumPixels));
            if (timeoutSeconds < 1 || timeoutSeconds > 300) throw new ArgumentOutOfRangeException(nameof(timeoutSeconds));
            _maximumPixels = maximumPixels;
            if (!MNNMarkdownRenderer.IsSafeImageUrl(url)) { Failure = MNNMarkdownImageFailure.InvalidUrl; return; }
            _download = new BoundedDownload(maximumEncodedBytes);
            _request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbGET, _download, null) { timeout = timeoutSeconds };
            try { _request.SendWebRequest(); }
            catch { _request.Dispose(); _download.Clear(); throw; }
        }

        public bool TryGetTexture(out Texture2D texture)
        {
            CheckOwner(); texture = null;
            if (_disposed || _taken || !IsDone || _request == null) return false;
            if (_download.Exceeded) { Failure = MNNMarkdownImageFailure.EncodedSizeLimit; return false; }
            if (_request.result != UnityWebRequest.Result.Success) { Failure = MNNMarkdownImageFailure.Download; return false; }
            byte[] bytes = _download.GetCompletedBytes();
            if (!MNNMarkdownImageHeader.TryReadDimensions(bytes, out int width, out int height))
            { Failure = MNNMarkdownImageFailure.UnsupportedImage; return false; }
            if ((long)width * height > _maximumPixels || width > SystemInfo.maxTextureSize || height > SystemInfo.maxTextureSize)
            { Failure = MNNMarkdownImageFailure.DecodedSizeLimit; return false; }
            _taken = true;
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            bool accepted = false;
            try
            {
                accepted = ImageConversion.LoadImage(decoded, bytes, true) && decoded.width == width && decoded.height == height;
                if (accepted) { texture = decoded; _taken = true; _download.Clear(); }
                else Failure = MNNMarkdownImageFailure.Decode;
                return accepted;
            }
            catch (UnityException) { Failure = MNNMarkdownImageFailure.Decode; return false; }
            finally
            {
                if (!accepted)
                {
                    if (Application.isPlaying) UnityEngine.Object.Destroy(decoded);
                    else UnityEngine.Object.DestroyImmediate(decoded);
                }
            }
        }

        private void CheckOwner()
        { if (Thread.CurrentThread.ManagedThreadId != _owner) throw new InvalidOperationException("Image requests belong to their creating Unity thread."); }
        public void Dispose()
        {
            CheckOwner(); if (_disposed) return; _disposed = true;
            if (_request != null)
            {
                if (!_request.isDone) { _request.Abort(); Failure = MNNMarkdownImageFailure.Cancelled; }
                _request.Dispose();
            }
            _download?.Clear();
        }

        private sealed class BoundedDownload : DownloadHandlerScript
        {
            private const int ChunkBytes = 16384;
            private readonly int _maximum;
            private readonly List<byte[]> _chunks = new List<byte[]>();
            private byte[] _completed;
            internal int ReceivedBytes { get; private set; }
            internal bool Exceeded { get; private set; }
            internal BoundedDownload(int maximum) : base(new byte[ChunkBytes]) { _maximum = maximum; }
            protected override void ReceiveContentLengthHeader(ulong contentLength)
            { if (contentLength > (ulong)_maximum) Exceeded = true; }
            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (Exceeded) return false;
                if (data == null) return dataLength == 0;
                if (dataLength < 0 || dataLength > data.Length || dataLength > _maximum - ReceivedBytes) { Exceeded = true; return false; }
                int offset = 0;
                while (offset < dataLength)
                {
                    int chunk = ReceivedBytes / ChunkBytes, position = ReceivedBytes % ChunkBytes;
                    if (chunk == _chunks.Count) _chunks.Add(new byte[Math.Min(ChunkBytes, _maximum - chunk * ChunkBytes)]);
                    int take = Math.Min(dataLength - offset, _chunks[chunk].Length - position);
                    Buffer.BlockCopy(data, offset, _chunks[chunk], position, take); offset += take; ReceivedBytes += take;
                }
                return true;
            }
            internal byte[] GetCompletedBytes()
            {
                if (_completed != null) return _completed;
                _completed = new byte[ReceivedBytes]; int offset = 0;
                foreach (var chunk in _chunks) { int take = Math.Min(chunk.Length, ReceivedBytes - offset); Buffer.BlockCopy(chunk, 0, _completed, offset, take); offset += take; }
                _chunks.Clear(); return _completed;
            }
            internal void Clear() { _chunks.Clear(); _completed = null; }
        }
    }
}
