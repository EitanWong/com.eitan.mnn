using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    internal static class MNNModelMetadata
    {
        internal static async Task<string> ReadTextAsync(HttpContent content, CancellationToken token, int maximumBytes = 16 * 1024 * 1024)
        {
            if (content == null) return string.Empty;
            long? length = content.Headers.ContentLength;
            if (length > maximumBytes) throw new InvalidDataException("Model metadata exceeded its byte budget.");
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(45));
                using (var input = await content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var output = new MemoryStream((int)Math.Min(length ?? 16384, maximumBytes)))
                using (deadline.Token.Register(input.Dispose))
                {
                    var buffer = new byte[16384];
                    while (true)
                    {
                        deadline.Token.ThrowIfCancellationRequested();
                        int count;
                        try { count = await input.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, maximumBytes - output.Length + 1), deadline.Token).ConfigureAwait(false); }
                        catch (ObjectDisposedException) when (deadline.IsCancellationRequested) { throw new OperationCanceledException(deadline.Token); }
                        catch (IOException) when (deadline.IsCancellationRequested) { throw new OperationCanceledException(deadline.Token); }
                        if (count == 0) break;
                        if (count > maximumBytes - output.Length) throw new InvalidDataException("Model metadata exceeded its byte budget.");
                        output.Write(buffer, 0, count);
                    }
                    deadline.Token.ThrowIfCancellationRequested();
                    return Encoding.UTF8.GetString(output.GetBuffer(), 0, (int)output.Length);
                }
            }
        }
    }
}
