using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity.Tests
{
    internal sealed class MarkdownImageLoopbackServer : IDisposable
    {
        private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new CancellationTokenSource();
        internal string Url { get; }
        internal Task Completion { get; }
        internal MarkdownImageLoopbackServer(byte[] body, bool contentLength = false, bool hold = false)
        {
            _listener.Start(); Url = "http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/image.png";
            Completion = RespondAsync(body, contentLength, hold, _stop.Token);
        }
        private async Task RespondAsync(byte[] body, bool contentLength, bool hold, CancellationToken token)
        {
            try
            {
                using (var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false))
                using (var stream = client.GetStream())
                {
                    var buffer = new byte[1024]; var header = new StringBuilder();
                    while (header.ToString().IndexOf("\r\n\r\n", StringComparison.Ordinal) < 0)
                    { int read = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false); if (read == 0) return; header.Append(Encoding.ASCII.GetString(buffer, 0, read)); }
                    if (hold) await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                    byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: image/png\r\n" +
                        (contentLength ? "Content-Length: " + body.Length + "\r\n" : "") + "Connection: close\r\n\r\n");
                    await stream.WriteAsync(response, 0, response.Length, token).ConfigureAwait(false);
                    for (int offset = 0; offset < body.Length; offset += 8192)
                        await stream.WriteAsync(body, offset, Math.Min(8192, body.Length - offset), token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (IOException) { }
            catch (SocketException) { }
        }
        public void Dispose() { _stop.Cancel(); _listener.Stop(); _stop.Dispose(); }
    }
}
