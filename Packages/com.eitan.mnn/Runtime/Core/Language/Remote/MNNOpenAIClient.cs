using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// Reusable OpenAI-compatible HTTP transport. The default transport refuses redirects.
    /// Text and PCM are consumed incrementally with awaited backpressure; no scene or device ownership.
    /// </summary>
    public sealed class MNNOpenAIClient : IDisposable
    {
        public const int SpeechSampleRate = 24000;
        private readonly HttpClient _client;
        private readonly bool _ownsClient;
        private readonly Uri _endpoint;
        private readonly string _key;
        private readonly TimeSpan _timeout;
        private const int MaximumEventBytes = 65536;

        /// <param name="endpoint">HTTPS API base URL, or HTTP loopback for development.</param>
        /// <param name="key">Optional bearer credential.</param>
        /// <param name="timeout">Deadline for each complete request, including response consumption.</param>
        /// <param name="client">Optional borrowed client. Its owner must disable automatic redirects
        /// (HttpClientHandler.AllowAutoRedirect = false), including same-origin redirects: a 307/308
        /// can resend the conversation or audio body before this API sees the response.
        /// This API cannot inspect or change an existing client's handler policy.</param>
        public MNNOpenAIClient(Uri endpoint, string key, TimeSpan timeout, HttpClient client = null)
        {
            if (endpoint == null || !endpoint.IsAbsoluteUri ||
                !(endpoint.Scheme == "https" || endpoint.Scheme == "http" && endpoint.IsLoopback) ||
                endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
                throw new ArgumentException("Use an HTTPS base URL, or HTTP loopback for development.", nameof(endpoint));
            if (timeout <= TimeSpan.Zero || timeout.TotalSeconds > 300) throw new ArgumentOutOfRangeException(nameof(timeout));
            if (key != null && (key.Length > 16384 || key.IndexOfAny(new[] { '\r', '\n' }) >= 0)) throw new ArgumentException("Invalid API credential.");
            _endpoint = new Uri(endpoint.AbsoluteUri.TrimEnd('/') + "/");
            _key = key;
            _timeout = timeout;
            _ownsClient = client == null;
            _client = client ?? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        }

        public async Task GenerateTextAsync(string model, IReadOnlyList<MNNOpenAIMessage> history,
            Func<string, Task> onDelta, CancellationToken token, float[] monoAudio = null, int sampleRate = 16000,
            int maximumCharacters = 16384, int? maximumTokens = null, float? temperature = null)
        {
            if (history == null || history.Count > 514 || onDelta == null) throw new ArgumentException("Bounded history and a text consumer are required.");
            if (maximumCharacters < 1 || maximumCharacters > 65536) throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
            if (maximumTokens.HasValue && (maximumTokens < 1 || maximumTokens > 8192)) throw new ArgumentOutOfRangeException(nameof(maximumTokens));
            if (temperature.HasValue && (float.IsNaN(temperature.Value) || float.IsInfinity(temperature.Value) || temperature < 0 || temperature > 2))
                throw new ArgumentOutOfRangeException(nameof(temperature));
            var body = new StringBuilder("{\"model\":").Append(Json(RequireModel(model))).Append(",\"stream\":true");
            if (maximumTokens.HasValue) body.Append(",\"max_tokens\":").Append(maximumTokens.Value);
            if (temperature.HasValue) body.Append(",\"temperature\":").Append(temperature.Value.ToString("R", CultureInfo.InvariantCulture));
            body.Append(",\"messages\":[");
            for (int i = 0; i < history.Count; ++i)
            {
                if (i > 0) body.Append(',');
                var message = history[i];
                if (message.Audio != null)
                {
                    body.Append("{\"role\":\"user\",\"content\":[{\"type\":\"input_audio\",\"input_audio\":{\"format\":\"wav\",\"data\":")
                        .Append(Json(Convert.ToBase64String(MNNWaveAudio.EncodePcm16(message.Audio, message.SampleRate)))).Append("}}]}");
                }
                else
                {
                    if (message.Text == null || message.Text.Length > 65536) throw new ArgumentException("History item exceeds the text budget.");
                    body.Append("{\"role\":").Append(Json(message.Role.ToString().ToLowerInvariant())).Append(",\"content\":").Append(Json(message.Text)).Append('}');
                }
                if (body.Length > 32 * 1024 * 1024) throw new ArgumentException("Multimodal history exceeds its request budget.");
            }
            if (monoAudio != null)
            {
                if (history.Count > 0) body.Append(',');
                body.Append("{\"role\":\"user\",\"content\":[{\"type\":\"input_audio\",\"input_audio\":{\"format\":\"wav\",\"data\":")
                    .Append(Json(Convert.ToBase64String(MNNWaveAudio.EncodePcm16(monoAudio, sampleRate)))).Append("}}]}");
            }
            body.Append("]}");
            if (body.Length > 32 * 1024 * 1024) throw new ArgumentException("Multimodal history exceeds its request budget.");
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var request = CreateRequest("chat/completions", new StringContent(body.ToString(), Encoding.UTF8, "application/json")))
            {
                timeout.CancelAfter(_timeout);
                using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                {
                    RequireSuccess(response);
                    if (!string.Equals(response.Content.Headers.ContentType?.MediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("The text provider must return a streaming SSE response.");
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (timeout.Token.Register(() => stream.Dispose()))
                    {
                        int characters = 0;
                        bool finished = false;
                        await ReadEventsAsync(stream, async data =>
                        {
                            if (data == "[DONE]") { finished = true; return false; }
                            var item = JsonUtility.FromJson<Completion>(data);
                            if (item?.error != null && !string.IsNullOrEmpty(item.error.message))
                                throw new InvalidDataException("The provider reported a streaming completion error.");
                            if (item?.choices == null) return true; // Usage-only events are allowed.
                            foreach (var choice in item.choices)
                            {
                                if (choice.index != 0) continue;
                                string delta = choice.delta?.content;
                                if (!string.IsNullOrEmpty(delta))
                                {
                                    characters = checked(characters + delta.Length);
                                    if (characters > maximumCharacters) throw new InvalidDataException("Response exceeded its configured text budget.");
                                    await onDelta(delta).ConfigureAwait(false);
                                }
                                if (!string.IsNullOrEmpty(choice.finish_reason)) finished = true;
                            }
                            return true;
                        }, timeout.Token).ConfigureAwait(false);
                        if (!finished) throw new EndOfStreamException("The completion stream ended before response completion.");
                    }
                }
            }
        }

        public async Task<string> TranscribeAsync(string model, float[] monoAudio, int sampleRate, CancellationToken token)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var form = new MultipartFormDataContent())
            {
                timeout.CancelAfter(_timeout);
                var wav = new ByteArrayContent(MNNWaveAudio.EncodePcm16(monoAudio, sampleRate));
                wav.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                form.Add(wav, "file", "utterance.wav");
                form.Add(new StringContent(RequireModel(model)), "model");
                form.Add(new StringContent("json"), "response_format");
                using (var request = CreateRequest("audio/transcriptions", form))
                using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                {
                    RequireSuccess(response);
                    string json = await ReadBoundedAsync(response.Content, 65536, timeout.Token).ConfigureAwait(false);
                    string text = JsonUtility.FromJson<Transcription>(json)?.text;
                    if (text == null || text.Length > 16384) throw new InvalidDataException("Invalid ASR result.");
                    return text;
                }
            }
        }

        /// <summary>Borrowed mono 24 kHz normalized PCM, at most 480 samples per callback; no full-waveform buffering.</summary>
        public async Task SynthesizeAsync(string model, string voice, string text,
            Func<float[], int, Task> onPcm, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 4096 || onPcm == null) throw new ArgumentException("Bounded speech text and a PCM consumer are required.");
            string json = "{\"model\":" + Json(RequireModel(model)) + ",\"voice\":" + Json(voice) + ",\"input\":" + Json(text) + ",\"response_format\":\"pcm\"}";
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
            using (var request = CreateRequest("audio/speech", new StringContent(json, Encoding.UTF8, "application/json")))
            {
                timeout.CancelAfter(_timeout);
                using (var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                {
                    RequireSuccess(response);
                    string media = response.Content.Headers.ContentType?.MediaType;
                    if (media != null && (media.IndexOf("json", StringComparison.OrdinalIgnoreCase) >= 0 || media.StartsWith("text/", StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidDataException("The speech provider returned a non-PCM response.");
                    using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (timeout.Token.Register(() => stream.Dispose()))
                    {
                        var bytes = new byte[961];
                        var pcm = new float[480];
                        int pending = 0;
                        long totalSamples = 0;
                        int read;
                        while ((read = await ReadCancellableAsync(stream, bytes, pending, 960 - pending, timeout.Token).ConfigureAwait(false)) != 0)
                        {
                            int available = pending + read;
                            int samples = available / 2;
                            for (int i = 0; i < samples; ++i) pcm[i] = (short)(bytes[i * 2] | bytes[i * 2 + 1] << 8) / 32768f;
                            pending = available % 2;
                            if (pending != 0) bytes[0] = bytes[available - 1];
                            totalSamples += samples;
                            if (totalSamples > SpeechSampleRate * 120L) throw new InvalidDataException("Speech exceeded the duration budget.");
                            if (samples > 0) await onPcm(pcm, samples).ConfigureAwait(false);
                        }
                        if (pending != 0 || totalSamples == 0) throw new InvalidDataException("Empty or incomplete PCM16 speech stream.");
                    }
                }
            }
        }

        private HttpRequestMessage CreateRequest(string path, HttpContent content)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_endpoint, path)) { Content = content };
            if (!string.IsNullOrEmpty(_key)) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
            return request;
        }
        private static void RequireSuccess(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
                throw new MNNOpenAIHttpException(response.StatusCode);
        }
        private static string RequireModel(string model) => !string.IsNullOrWhiteSpace(model) && model.Length <= 256 ? model : throw new ArgumentException("Select a model ID.");
        internal static string Json(string value)
        {
            if (value == null) return "null";
            var b = new StringBuilder(value.Length + 2).Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default: if (c < 32) b.Append("\\u").Append(((int)c).ToString("x4")); else b.Append(c); break;
                }
            }
            return b.Append('"').ToString();
        }
        private static async Task<string> ReadBoundedAsync(HttpContent content, int maximum, CancellationToken token)
        {
            using (var source = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (token.Register(() => source.Dispose()))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[4096];
                int n;
                while ((n = await ReadCancellableAsync(source, buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                {
                    if (output.Length + n > maximum) throw new InvalidDataException("Provider response exceeded its size budget.");
                    output.Write(buffer, 0, n);
                }
                return new UTF8Encoding(false, true).GetString(output.ToArray());
            }
        }
        private static async Task<int> ReadCancellableAsync(Stream source, byte[] bytes, int offset, int count, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                int read = await source.ReadAsync(bytes, offset, count, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                return read;
            }
            catch (Exception) when (token.IsCancellationRequested)
            {
                throw new OperationCanceledException(token);
            }
        }
        internal static async Task ReadEventsAsync(Stream source, Func<string, Task<bool>> onData, CancellationToken token)
        {
            var bytes = new byte[4096];
            var line = new byte[MaximumEventBytes];
            var data = new StringBuilder();
            int length = 0, read;
            bool firstLine = true, previousCr = false;
            while ((read = await ReadCancellableAsync(source, bytes, 0, bytes.Length, token).ConfigureAwait(false)) > 0)
                for (int i = 0; i < read; ++i)
                {
                    byte value = bytes[i];
                    if (previousCr && value == 10) { previousCr = false; continue; }
                    previousCr = value == 13;
                    if (value != 10 && value != 13)
                    {
                        if (length == line.Length) throw new InvalidDataException("SSE line exceeded its size budget.");
                        line[length++] = value;
                        continue;
                    }
                    string text = new UTF8Encoding(false, true).GetString(line, 0, length);
                    if (firstLine) { text = text.TrimStart('\uFEFF'); firstLine = false; }
                    length = 0;
                    if (text.Length == 0)
                    {
                        if (data.Length > 0)
                        {
                            string payload = data.ToString(0, data.Length - 1);
                            data.Clear();
                            if (!await onData(payload).ConfigureAwait(false)) return;
                        }
                    }
                    else if (text.StartsWith("data:", StringComparison.Ordinal))
                    {
                        string payload = text.Substring(text.Length > 5 && text[5] == ' ' ? 6 : 5);
                        if (data.Length + payload.Length + 1 > MaximumEventBytes) throw new InvalidDataException("SSE event exceeded its size budget.");
                        data.Append(payload).Append('\n');
                    }
                }
            if (length != 0 || data.Length != 0) throw new EndOfStreamException("Incomplete SSE event.");
        }
        public void Dispose() { if (_ownsClient) _client.Dispose(); }
#pragma warning disable CS0649
        [Serializable] private sealed class Completion { public Choice[] choices; public ApiError error; }
        [Serializable] private sealed class ApiError { public string message; }
        [Serializable] private sealed class Choice { public int index; public Delta delta; public string finish_reason; }
        [Serializable] private sealed class Delta { public string content; }
        [Serializable] private sealed class Transcription { public string text; }
#pragma warning restore CS0649
    }
}
