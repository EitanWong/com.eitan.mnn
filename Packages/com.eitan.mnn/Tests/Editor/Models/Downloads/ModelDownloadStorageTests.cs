using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ModelDownloadStorageTests
    {
        private string _root;
        private static readonly byte[] Payload = Encoding.UTF8.GetBytes("model fixture bytes");
        [SetUp] public void SetUp() => _root = Path.GetFullPath("TestArtifacts~/ModelDownloadStorageTests/" + Guid.NewGuid().ToString("N"));
        [TearDown] public void TearDown() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        private static MNNModelTransferOptions Options(long available) => new MNNModelTransferOptions
        { MaxConnections = 1, PreservePartialDownloads = true, DiskSpaceReserveBytes = 8, AvailableSpaceBytes = _ => available };
        private static HttpResponseMessage Json(string value) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(value) };
        private static string Hash(byte[] data) { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "").ToLowerInvariant(); }
        private static MNNModelTransferFile FilePlan() => new MNNModelTransferFile { Path = "model.mnn", Revision = "commit", Size = Payload.Length, Sha256 = Hash(Payload) };

        [Test]
        public async Task ModelScopeChecksAggregateCapacityAndExposesApplicationError()
        {
            int requests = 0;
            using (var client = new HttpClient(new Handler(_ =>
            {
                ++requests;
                return Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"one.mnn\",\"Type\":\"blob\",\"Size\":100},{\"Path\":\"two.mnn\",\"Type\":\"blob\",\"Size\":100}]}}");
            })))
            using (var downloader = new MNNModelDownloader(client, "https://space-ms.test", Options(180)))
            {
                var error = await CapacityError(() => downloader.DownloadRepositoryAsync("MNN/fixture", _root));
                Assert.AreEqual("insufficient_disk_space", MNNInsufficientDiskSpaceException.ErrorCode);
                Assert.AreEqual("MNN/fixture", error.RepositoryId);
                Assert.AreEqual(_root, error.DirectoryPath);
                Assert.AreEqual(208, error.RequiredBytes);
                Assert.AreEqual(180, error.AvailableBytes);
                Assert.AreEqual(28, error.BytesToFree);
                Assert.AreEqual(1, requests, "Only metadata may be requested.");
                Assert.False(Directory.Exists(_root), "Capacity rejection must precede manifest writes.");
            }
        }

        [Test]
        public async Task HuggingFaceUsesTheSameCapacityContract()
        {
            int requests = 0;
            var pool = new MNNHuggingFaceSourcePool(new[] { new MNNHuggingFaceSource("Fixture", "https://space-hf.test") });
            using (var client = new HttpClient(new Handler(_ =>
            {
                ++requests;
                return Json("{\"siblings\":[{\"rfilename\":\"one.mnn\",\"size\":100},{\"rfilename\":\"two.mnn\",\"size\":100}]}");
            })))
            using (var downloader = new MNNModelDownloader(client, options: Options(180), mirrorPool: pool))
            {
                var error = await CapacityError(() => downloader.DownloadHuggingFaceRepositoryAsync("publisher/fixture", _root));
                Assert.AreEqual(208, error.RequiredBytes);
                Assert.AreEqual(1, requests);
                Assert.False(Directory.Exists(_root));
            }
        }

        [Test]
        public async Task VerifiedCompletedFilesNeedOnlyMetadataReserve()
        {
            Directory.CreateDirectory(_root);
            string destination = Path.Combine(_root, "model.mnn");
            System.IO.File.WriteAllBytes(destination, Payload);
            using (var client = new HttpClient(new Handler(_ =>
                Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"model.mnn\",\"Type\":\"blob\",\"Size\":" + Payload.Length + ",\"Sha256\":\"" + Hash(Payload) + "\"}]}}"))))
            using (var downloader = new MNNModelDownloader(client, "https://reuse-space.test", Options(8)))
                Assert.AreEqual(Payload.Length, await downloader.DownloadRepositoryAsync("MNN/fixture", _root));
            CollectionAssert.AreEqual(Payload, System.IO.File.ReadAllBytes(destination));
        }

        [Test]
        public async Task MatchingCheckpointCountsOnlyRemainingBytesAndResumes()
        {
            string destination = SeedParts(1, true, 5);
            long available = Payload.Length - 5 + 8;
            int requests = 0;
            using (var client = new HttpClient(new Handler(request =>
            {
                ++requests;
                Assert.AreEqual(5, FirstRange(request).From);
                var remaining = new byte[Payload.Length - 5]; Array.Copy(Payload, 5, remaining, 0, remaining.Length);
                var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(remaining) };
                response.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(5, Payload.Length - 1, Payload.Length);
                return response;
            })))
            using (var transfer = new MNNModelFileTransfer(client, Options(available)))
                Assert.AreEqual(Payload.Length, await transfer.DownloadAsync("MNN/fixture", FilePlan(), destination, null, CancellationToken.None, () => "https://resume.test/model"));
            Assert.AreEqual(1, requests);
            CollectionAssert.AreEqual(Payload, System.IO.File.ReadAllBytes(destination));
            Assert.False(Directory.Exists(destination + ".mnn-parts"));
        }

        [Test]
        public async Task ForeignCheckpointCannotUnderestimateRequiredCapacity()
        {
            string destination = SeedParts(1, false, 5);
            using (var client = new HttpClient(new Handler(_ => throw new Exception("No payload request expected"))))
            using (var transfer = new MNNModelFileTransfer(client, Options(Payload.Length - 5 + 8)))
            {
                var error = await CapacityError(() => transfer.DownloadAsync("MNN/fixture", FilePlan(), destination, null, CancellationToken.None, () => "https://fixture.test/model"));
                Assert.AreEqual(Payload.Length + 8, error.RequiredBytes);
            }
            Assert.AreEqual(5, new FileInfo(Path.Combine(destination + ".mnn-parts", "0.part")).Length);
        }

        [Test]
        public async Task CompleteParallelSegmentsStillRequireAssemblyWorkspace()
        {
            string destination = SeedParts(2, true, Payload.Length);
            var options = Options(Payload.Length + 7); options.MaxConnections = 2; options.ParallelThresholdBytes = 1;
            using (var client = new HttpClient(new Handler(_ => throw new Exception("No payload request expected"))))
            using (var transfer = new MNNModelFileTransfer(client, options))
            {
                var error = await CapacityError(() => transfer.DownloadAsync("MNN/fixture", FilePlan(), destination, null, CancellationToken.None, () => "https://fixture.test/model"));
                Assert.AreEqual(Payload.Length + 8, error.RequiredBytes);
            }
            Assert.True(System.IO.File.Exists(Path.Combine(destination + ".mnn-parts", "1.part")));
        }

        [Test]
        public async Task SingleSegmentPublishesWithoutNeedingASecondPayloadCopy()
        {
            string destination = SeedParts(1, true, Payload.Length);
            using (var client = new HttpClient(new Handler(_ => throw new Exception("Complete segment must not be downloaded"))))
            using (var transfer = new MNNModelFileTransfer(client, Options(8)))
                Assert.AreEqual(Payload.Length, await transfer.DownloadAsync("MNN/fixture", FilePlan(), destination, null, CancellationToken.None, () => "https://fixture.test/model"));
            CollectionAssert.AreEqual(Payload, System.IO.File.ReadAllBytes(destination));
        }

        [Test]
        public async Task UnknownListingSizeChecksResponseLengthBeforeWritingPayload()
        {
            string destination = Path.Combine(_root, "model.mnn");
            using (var client = new HttpClient(new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Payload) })))
            using (var transfer = new MNNModelFileTransfer(client, Options(8)))
            {
                var file = FilePlan(); file.Size = 0;
                var error = await CapacityError(() => transfer.DownloadAsync("MNN/fixture", file, destination, null, CancellationToken.None, () => "https://fixture.test/model"));
                Assert.AreEqual(Payload.Length + 8, error.RequiredBytes);
            }
            Assert.False(System.IO.File.Exists(destination));
            Assert.False(System.IO.File.Exists(Path.Combine(destination + ".mnn-parts", "0.part")));
        }

        [Test]
        public async Task CapacityErrorPropagatesThroughInstallationWithFailedPhase()
        {
            var model = new MNNRemoteModel { provider = MNNModelSource.ModelScope, repositoryId = "MNN/fixture", isRepository = true };
            MNNModelInstallation.Phase last = MNNModelInstallation.Phase.Waiting;
            using (var client = new HttpClient(new Handler(_ => Json("{\"Code\":200,\"Data\":{\"Files\":[{\"Path\":\"model.mnn\",\"Type\":\"blob\",\"Size\":100}]}}"))))
            using (var downloader = new MNNModelDownloader(client, "https://installation-space.test", Options(8)))
                await CapacityError(() => MNNModelInstallation.EnsureAsync(_root, model, downloader: downloader, state: (phase, _) => last = phase));
            Assert.AreEqual(MNNModelInstallation.Phase.Failed, last);
            Assert.False(System.IO.File.Exists(Path.Combine(_root, MNNModelInstallation.ReceiptName)));
        }

        [Test]
        public Task DiskFullDuringStreamingIsTypedAndNotRetried() => DiskFullDuringStreaming(false);

        [Test]
        public Task DiskFullDuringParallelStreamingPreservesSegmentsWithoutFallback() => DiskFullDuringStreaming(true);

        private async Task DiskFullDuringStreaming(bool parallel)
        {
            string destination = parallel ? SeedParts(2, true, 5) : Path.Combine(_root, "model.mnn");
            int requests = 0;
            var options = Options(1024);
            options.MaxConnections = parallel ? 2 : 1;
            options.ParallelThresholdBytes = 1;
            using (var client = new HttpClient(new Handler(request =>
            {
                Interlocked.Increment(ref requests);
                var response = new HttpResponseMessage(request.Headers.Range == null ? HttpStatusCode.OK : HttpStatusCode.PartialContent)
                { Content = new StreamContent(new DiskFullStream()) };
                if (request.Headers.Range != null)
                {
                    var range = FirstRange(request);
                    response.Content.Headers.ContentRange = new System.Net.Http.Headers.ContentRangeHeaderValue(range.From.Value, range.To.Value, Payload.Length);
                }
                return response;
            })))
            using (var transfer = new MNNModelFileTransfer(client, options))
            {
                var error = await CapacityError(() => transfer.DownloadAsync("MNN/fixture", FilePlan(), destination, null, CancellationToken.None, () => "https://disk-full.test/model"));
                Assert.AreEqual("MNN/fixture", error.RepositoryId);
                Assert.IsInstanceOf<IOException>(error.InnerException);
                Assert.AreEqual(112, error.InnerException.HResult & 0xffff);
            }
            Assert.That(requests, Is.InRange(1, parallel ? 2 : 1), "Disk-full errors must not retry or fall back.");
            Assert.False(System.IO.File.Exists(destination));
            if (parallel)
            {
                string parts = destination + ".mnn-parts";
                Assert.AreEqual(5, new FileInfo(Path.Combine(parts, "0.part")).Length);
                StringAssert.Contains("\"Parts\":2", System.IO.File.ReadAllText(Path.Combine(parts, "checkpoint.json")));
            }
        }

        private sealed class DiskFullIOException : IOException
        {
            internal DiskFullIOException() : base("Injected native disk-full failure") { HResult = unchecked((int)0x80070070); }
        }
        private sealed class DiskFullStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count) => throw new DiskFullIOException();
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token) => Task.FromException<int>(new DiskFullIOException());
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private string SeedParts(int count, bool matching, int bytes)
        {
            string destination = Path.Combine(_root, "model.mnn"), parts = destination + ".mnn-parts";
            Directory.CreateDirectory(parts);
            var plan = FilePlan();
            string identity = (matching ? "MNN/fixture" : "MNN/other") + "\n" + plan.Path + "\ncommit\n" + plan.Size + "\n" + plan.Sha256;
            string escaped = identity.Replace("\\", "\\\\").Replace("\n", "\\n");
            System.IO.File.WriteAllText(Path.Combine(parts, "checkpoint.json"), "{\"Identity\":\"" + escaped + "\",\"Parts\":" + count + "}");
            for (int i = 0; i < count; ++i)
            {
                int start = Payload.Length * i / count, size = Math.Min(bytes, Payload.Length * (i + 1) / count - start);
                var segment = new byte[size]; Array.Copy(Payload, start, segment, 0, size);
                System.IO.File.WriteAllBytes(Path.Combine(parts, i + ".part"), segment);
                bytes -= size;
            }
            return destination;
        }
        private static System.Net.Http.Headers.RangeItemHeaderValue FirstRange(HttpRequestMessage request)
        { foreach (var range in request.Headers.Range.Ranges) return range; throw new Exception("Missing Range"); }
        private static async Task<MNNInsufficientDiskSpaceException> CapacityError(Func<Task> action)
        {
            try { await action(); }
            catch (MNNInsufficientDiskSpaceException error) { return error; }
            Assert.Fail("Expected structured insufficient disk space error."); return null;
        }
        private sealed class Handler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;
            internal Handler(Func<HttpRequestMessage, HttpResponseMessage> response) { _response = response; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(_response(request));
        }
    }
}
