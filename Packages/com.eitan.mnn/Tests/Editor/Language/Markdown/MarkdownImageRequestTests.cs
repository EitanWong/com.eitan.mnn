using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class MarkdownImageRequestTests
    {
        [UnityTest]
        public IEnumerator UnknownLengthReceiveIsBoundedWithoutPollingDownloadProgress()
        {
            using (var server = new MarkdownImageLoopbackServer(new byte[1024 * 1024]))
            using (var request = new MNNMarkdownImageRequest(server.Url, maximumEncodedBytes: 32768))
            {
                double deadline = UnityEditor.EditorApplication.timeSinceStartup + 5;
                while (!server.Completion.IsCompleted && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
                yield return WaitFor(request);
                Assert.False(request.TryGetTexture(out _));
                Assert.AreEqual(MNNMarkdownImageFailure.EncodedSizeLimit, request.Failure);
                Assert.LessOrEqual(request.EncodedBytes, 32768);
            }
        }
        [UnityTest]
        public IEnumerator DeclaredOversizedBodyIsRejectedAtIngress()
        {
            using (var server = new MarkdownImageLoopbackServer(new byte[65536], contentLength: true))
            using (var request = new MNNMarkdownImageRequest(server.Url, maximumEncodedBytes: 4096))
            {
                yield return WaitFor(request); Assert.False(request.TryGetTexture(out _));
                Assert.AreEqual(MNNMarkdownImageFailure.EncodedSizeLimit, request.Failure);
                Assert.Zero(request.EncodedBytes);
            }
        }
        [UnityTest]
        public IEnumerator CompressedHeaderCannotAllocateAnOversizedTexture()
        {
            using (var server = new MarkdownImageLoopbackServer(MarkdownImageHeaderTests.Png(16384, 16384)))
            using (var request = new MNNMarkdownImageRequest(server.Url))
            {
                yield return WaitFor(request); Assert.False(request.TryGetTexture(out _));
                Assert.AreEqual(MNNMarkdownImageFailure.DecodedSizeLimit, request.Failure);
            }
        }
        [UnityTest]
        public IEnumerator ValidImageDecodesOnceAndTransfersNonReadableTextureOwnership()
        {
            var source = new Texture2D(3, 2, TextureFormat.RGBA32, false);
            byte[] bytes = source.EncodeToPNG(); UnityEngine.Object.DestroyImmediate(source);
            using (var server = new MarkdownImageLoopbackServer(bytes))
            using (var request = new MNNMarkdownImageRequest(server.Url))
            {
                yield return WaitFor(request); Assert.True(request.TryGetTexture(out var texture));
                try
                {
                    Assert.AreEqual(3, texture.width); Assert.AreEqual(2, texture.height); Assert.False(texture.isReadable);
                    Assert.False(request.TryGetTexture(out _), "Each result transfers ownership once.");
                    request.Dispose(); Assert.AreEqual(3, texture.width, "The caller retains a transferred texture.");
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
        }
        [Test]
        public void CancellationIsIdempotentAndCannotExposeAPartialImage()
        {
            using (var server = new MarkdownImageLoopbackServer(new byte[64], hold: true))
            {
                var request = new MNNMarkdownImageRequest(server.Url); request.Dispose(); request.Dispose();
                Assert.True(request.IsDone); Assert.False(request.TryGetTexture(out _));
                Assert.AreEqual(MNNMarkdownImageFailure.Cancelled, request.Failure);
            }
        }
        private static IEnumerator WaitFor(MNNMarkdownImageRequest request)
        {
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 5;
            while (!request.IsDone && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.True(request.IsDone, "The controlled loopback request did not complete.");
        }
    }
}
