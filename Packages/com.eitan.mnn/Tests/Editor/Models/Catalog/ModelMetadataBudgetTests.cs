using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class ModelMetadataBudgetTests
    {
        private static void Run(Func<Task> action) => Task.Run(action).GetAwaiter().GetResult();
        private sealed class ChunkStream : Stream
        {
            private readonly int _length; public int ReadBytes;
            internal ChunkStream(int length) { _length = length; }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => ReadBytes; set => throw new NotSupportedException(); }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = Math.Min(count, _length - ReadBytes);
                for (int i = 0; i < read; ++i) buffer[offset + i] = (byte)'x';
                ReadBytes += read; return read;
            }
            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
            { token.ThrowIfCancellationRequested(); return Task.FromResult(Read(buffer, offset, count)); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
        [Test] public void UnknownLengthBodyStopsReadingAtBudgetPlusOneInsteadOfBufferingTheWholeResponse()
        {
            var input = new ChunkStream(1000000);
            using (var content = new StreamContent(input))
            {
                Assert.Throws<InvalidDataException>(() => MNNModelMetadata.ReadTextAsync(content, CancellationToken.None, 32).GetAwaiter().GetResult());
                Assert.AreEqual(33, input.ReadBytes);
            }
        }
        [Test] public void DeclaredOversizeBodyIsRejectedBeforeTheFirstRead()
        {
            var input = new ChunkStream(1000000);
            using (var content = new StreamContent(input))
            {
                content.Headers.ContentLength = 1000000;
                Assert.Throws<InvalidDataException>(() => MNNModelMetadata.ReadTextAsync(content, CancellationToken.None, 32).GetAwaiter().GetResult());
                Assert.AreEqual(0, input.ReadBytes);
            }
        }
        [Test] public void CancellationBeforeConsumptionDoesNotReadTheBody()
        {
            var input = new ChunkStream(100);
            using (var content = new StreamContent(input))
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Assert.Catch<OperationCanceledException>(() => MNNModelMetadata.ReadTextAsync(content, cancelled.Token).GetAwaiter().GetResult());
                Assert.AreEqual(0, input.ReadBytes);
            }
        }
        [Test] public void ExactBudgetBodyRemainsReadable() => Run(async () =>
        {
            using (var content = new StreamContent(new ChunkStream(32)))
                Assert.AreEqual(new string('x', 32), await MNNModelMetadata.ReadTextAsync(content, CancellationToken.None, 32));
        });
    }
}
