using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Managed task-specific cloud speech protocols. Buffered mono PCM, bounded HTTP and cancellation.</summary>
    public sealed partial class MNNCloudSpeechProvider : IMNNConversationAsrProvider, IMNNConversationTtsProvider, IMNNConversationSpeechLanguages
    {
        private readonly MNNCloudSpeechOptions _settings;
        private readonly HttpClient _http;
        private readonly string _key;
        public int SampleRate => 24000;
        public IReadOnlyList<string> SupportedSpeechLanguages => Array.AsReadOnly(_settings.SpeechLanguages);
        public MNNRealtimeCapabilities Capabilities { get; }
        public MNNCloudSpeechProvider(MNNCloudSpeechOptions settings, string key = null, HttpMessageHandler handler = null)
        {
            if (key != null && (key.Length > 16384 || key.IndexOfAny(new[] { '\r', '\n' }) >= 0)) throw new ArgumentException("Invalid API credential.");
            _settings = (settings ?? throw new ArgumentNullException(nameof(settings))).Snapshot();
            _key = key;
            _http = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
            Capabilities = Describe(_settings);
        }
        public static MNNRealtimeCapabilities Describe(MNNCloudSpeechOptions settings)
        {
            var options = (settings ?? throw new ArgumentNullException(nameof(settings))).Snapshot();
            return new MNNRealtimeCapabilities(MNNRealtimeFeature.BoundedCancellation |
                (options.Task == MNNCloudSpeechTask.Recognition ? MNNRealtimeFeature.MemoryAudioInput | MNNRealtimeFeature.SpeechRecognition : MNNRealtimeFeature.IndependentTts),
                options.DisplayName + " / managed cloud speech",
                "Task-specific HTTP protocol; buffered PCM16 at 24 kHz; bounded payloads and cancellation.",
                "Account/model access must be verified with the deployed service. No measured realtime claim.", execution: MNNRealtimeExecution.RemoteApi);
        }
        public Task PrepareAsync(CancellationToken token) { token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
        public async Task<string> TranscribeAsync(float[] audio, int sampleRate, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_settings.Task != MNNCloudSpeechTask.Recognition) throw new InvalidOperationException("This service is configured for synthesis.");
            if (audio == null || audio.Length == 0 || sampleRate < 8000 || sampleRate > 96000 || audio.Length > (long)sampleRate * 120)
                throw new ArgumentException("Recognition requires at most 120 seconds of mono audio at 8–96 kHz.");
            string data = Convert.ToBase64String(MNNWaveAudio.EncodePcm16(audio, sampleRate));
            using (var timeout = Timeout(token))
            {
                if (_settings.Protocol == MNNCloudSpeechProtocol.DashScopeRecognition)
                {
                    var body = new RecognitionRequest { model = _settings.Model, messages = new[] { new AudioMessage { content = new[] { new AudioContent { input_audio = new AudioInput { data = "data:audio/wav;base64," + data } } } } } };
                    string json = await PostJsonAsync("chat/completions", body, timeout.Token).ConfigureAwait(false);
                    var result = Parse<ChatResult>(json);
                    if (result.choices == null || result.choices.Length == 0 || result.choices[0].message?.content == null) throw InvalidResponse();
                    return result.choices[0].message.content.Trim();
                }
                if (_settings.Protocol != MNNCloudSpeechProtocol.VolcengineRecognition) throw new InvalidOperationException("Unsupported recognition protocol.");
                string response = await PostJsonAsync("auc/bigmodel/recognize/flash", new VolcRecognitionRequest
                {
                    audio = new VolcAudio { data = data },
                    request = new VolcRecognitionOptions { model_name = _settings.Model }
                }, timeout.Token).ConfigureAwait(false);
                var parsed = Parse<VolcRecognitionResult>(response);
                if (parsed.result?.text == null) throw InvalidResponse();
                return parsed.result.text.Trim();
            }
        }
        public async Task SynthesizeAsync(string text, Func<float[], int, Task> pcm, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (_settings.Task != MNNCloudSpeechTask.Synthesis) throw new InvalidOperationException("This service is configured for recognition.");
            if (pcm == null) throw new ArgumentNullException(nameof(pcm));
            string spoken = MNNSpeechTextCleaner.Clean(text);
            if (string.IsNullOrWhiteSpace(spoken)) { await pcm(new float[240], 240).ConfigureAwait(false); return; }
            if (spoken.Length > 6000) throw new ArgumentException("Synthesize at most 6000 characters per request.");
            using (var timeout = Timeout(token))
            {
                byte[] bytes = await SynthesizeBytesAsync(spoken, timeout.Token).ConfigureAwait(false);
                float[] samples = MNNCloudSpeechAudio.Decode(bytes, _settings.Protocol == MNNCloudSpeechProtocol.DashScopeSpeech, SampleRate);
                timeout.Token.ThrowIfCancellationRequested();
                await pcm(samples, samples.Length).ConfigureAwait(false);
                timeout.Token.ThrowIfCancellationRequested();
            }
        }
        private async Task<byte[]> SynthesizeBytesAsync(string text, CancellationToken token)
        {
            switch (_settings.Protocol)
            {
                case MNNCloudSpeechProtocol.OpenAISpeech:
                    return await PostBytesAsync("audio/speech", new CompatibleSpeech { model = _settings.Model, voice = _settings.Voice, input = text }, token).ConfigureAwait(false);
                case MNNCloudSpeechProtocol.MiniMaxSpeech:
                    var mini = Parse<MiniResult>(await PostJsonAsync("t2a_v2", new MiniSpeech
                    {
                        model = _settings.Model,
                        text = text,
                        voice_setting = new MiniVoice { voice_id = _settings.Voice }
                    }, token).ConfigureAwait(false));
                    if (mini.base_resp == null || mini.base_resp.status_code != 0 || string.IsNullOrEmpty(mini.data?.audio)) throw InvalidResponse();
                    return MNNCloudSpeechAudio.Hex(mini.data.audio);
                case MNNCloudSpeechProtocol.VolcengineSpeech:
                    string lines = await PostJsonAsync("tts/unidirectional", new VolcSpeech { req_params = new VolcSpeechOptions { text = text, speaker = _settings.Voice } }, token).ConfigureAwait(false);
                    return MNNCloudSpeechAudio.VolcChunks(lines);
                case MNNCloudSpeechProtocol.DashScopeSpeech:
                    string json = await PostJsonAsync("services/aigc/multimodal-generation/generation", new QwenSpeech
                    {
                        model = _settings.Model,
                        input = new QwenSpeechInput { text = text, voice = _settings.Voice, language_type = string.IsNullOrWhiteSpace(_settings.Language) ? "Chinese" : _settings.Language }
                    }, token).ConfigureAwait(false);
                    var audio = Parse<QwenResult>(json).output?.audio;
                    if (!string.IsNullOrEmpty(audio?.data)) return Convert.FromBase64String(audio.data);
                    return await DownloadAudioAsync(audio?.url, token).ConfigureAwait(false);
                default: throw new InvalidOperationException("Unsupported synthesis protocol.");
            }
        }
        private CancellationTokenSource Timeout(CancellationToken token)
        {
            var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));
            return timeout;
        }
        private static T Parse<T>(string json)
        {
            try { return JsonUtility.FromJson<T>(json); }
            catch (ArgumentException) { throw InvalidResponse(); }
        }
        private static InvalidOperationException InvalidResponse() => new InvalidOperationException("The speech service returned an invalid response. Check model access, voice and service credentials.");
        public void Dispose() => _http.Dispose();
    }
}
