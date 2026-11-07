using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity.Tests
{
    /// <summary>A real loopback endpoint: redirects the initial POST and accepts any followed request.</summary>
    internal sealed class OpenAIRedirectFixture : IDisposable
    {
        private readonly TcpListener _listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _shutdown = new CancellationTokenSource();
        private readonly Task _server;
        private readonly int _status;
        private int _requests;
        internal Uri Endpoint { get; }
        internal int Requests => Volatile.Read(ref _requests);
        internal string InitialHeaders { get; private set; }
        internal string InitialBody { get; private set; }

        internal OpenAIRedirectFixture(int status)
        {
            _status = status;
            _listener.Start();
            Endpoint = new Uri("http://127.0.0.1:" + ((IPEndPoint)_listener.LocalEndpoint).Port + "/v1/");
            _server = ServeAsync();
        }

        private async Task ServeAsync()
        {
            try
            {
                while (!_shutdown.IsCancellationRequested)
                    using (var connection = await _listener.AcceptTcpClientAsync().ConfigureAwait(false))
                    using (_shutdown.Token.Register(connection.Close))
                        await RespondAsync(connection).ConfigureAwait(false);
            }
            catch (Exception error) when (_shutdown.IsCancellationRequested &&
                (error is SocketException || error is ObjectDisposedException || error is IOException)) { }
        }

        private async Task RespondAsync(TcpClient connection)
        {
            using (var stream = connection.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true))
            {
                string request = await reader.ReadLineAsync().ConfigureAwait(false);
                var headers = new StringBuilder(request).Append('\n');
                int length = 0;
                string line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync().ConfigureAwait(false)))
                {
                    headers.Append(line).Append('\n');
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        length = int.Parse(line.Substring("Content-Length:".Length).Trim());
                }
                if (length < 0 || length > 65536) throw new InvalidDataException("Unexpected test request size.");
                var body = new char[length];
                int count = 0;
                while (count < body.Length)
                {
                    int read = await reader.ReadAsync(body, count, body.Length - count).ConfigureAwait(false);
                    if (read == 0) throw new EndOfStreamException();
                    count += read;
                }
                bool initial = Interlocked.Increment(ref _requests) == 1;
                if (initial) { InitialHeaders = headers.ToString(); InitialBody = new string(body); }
                string response = initial
                    ? "HTTP/1.1 " + _status + " Redirect\r\nLocation: " + new Uri(Endpoint, "redirected") + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                    : "HTTP/1.1 200 OK\r\nContent-Type: application/octet-stream\r\nContent-Length: 2\r\nConnection: close\r\n\r\n\0\0";
                byte[] bytes = Encoding.ASCII.GetBytes(response);
                await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            _shutdown.Cancel();
            _listener.Stop();
            try { _server.GetAwaiter().GetResult(); }
            finally { _shutdown.Dispose(); }
        }
    }
}
