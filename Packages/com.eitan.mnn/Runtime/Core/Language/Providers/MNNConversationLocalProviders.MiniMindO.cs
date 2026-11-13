using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public static partial class MNNConversationLocalProviders
    {
        private sealed class MiniMindO : Provider, IMNNConversationTextProvider, IMNNConversationSpeechModelProvider
        {
            private readonly MNNConversationLocalModelOptions _settings;
            private MNNMiniMindO _model;
            internal MiniMindO(MNNRealtimeCapabilities caps, string directory, MNNConversationLocalModelOptions settings)
                : base(caps, null, directory) { _settings = settings; }
            protected override string ReadBackend() => _model == null ? "Not loaded" : "MiniMind-o: CPU / Low precision / Low memory";
            protected override void Load()
            {
                if (!MNNMiniMindO.IsComplete(Directory)) throw new InvalidOperationException("Install the complete MiniMind-o export before loading.");
                _model = MNNMiniMindO.Load(Directory, _settings.Threads);
            }
            public async Task GenerateAsync(IReadOnlyList<MNNOpenAIMessage> history, float[] audio, int sampleRate,
                Func<string, Task> textDelta, CancellationToken token) =>
                await GenerateCore(history, audio, sampleRate, textDelta, false, token).ConfigureAwait(false);
            public Task<MNNMultimodalResult> GenerateSpeechAsync(IReadOnlyList<MNNOpenAIMessage> history,
                Func<string, Task> textDelta, CancellationToken token) => GenerateCore(history, null, 16000, textDelta, true, token);
            private async Task<MNNMultimodalResult> GenerateCore(IReadOnlyList<MNNOpenAIMessage> history, float[] audio,
                int rate, Func<string, Task> delta, bool speech, CancellationToken token)
            {
                if (history == null || history.Count == 0) throw new ArgumentException("Supply the current user turn.", nameof(history));
                var last = history[history.Count - 1];
                if (last.Role != MNNChatRole.User) throw new ArgumentException("MiniMind-o requires a final user turn.", nameof(history));
                if (audio == null && last.Audio != null) { audio = last.Audio; rate = last.SampleRate; }
                var request = new MNNMiniMindORequest
                {
                    Text = last.Text ?? "请听取这条语音并简短回答。",
                    TrimHistoryToFit = true,
                    History = history.Take(history.Count - 1).Select(message => new MNNChatMessage(message.Role, message.Text ?? (message.Audio == null ? "" : "[先前语音输入，未转写]"))).ToArray(),
                    Audio16Khz = audio == null ? null : MNNAudioResampling.Resample(audio, rate, 16000, token)
                };
                string previous = "";
                var result = await _model.GenerateAsync(request,
                    new MNNMiniMindOOptions { MaxNewTokens = Math.Min(256, _settings.MaximumReplyTokens), Seed = _settings.SynthesisSeed, GenerateSpeech = speech }, update =>
                    {
                        token.ThrowIfCancellationRequested();
                        if (update.Text.Length <= previous.Length) return;
                        string added = update.Text.Substring(previous.Length); previous = update.Text;
                        delta?.Invoke(added).GetAwaiter().GetResult();
                    }, token).ConfigureAwait(false);
                return new MNNMultimodalResult(result.Text, result.GeneratedTokens, result.TokenLimitReached,
                    result.Waveform, result.SampleRate, 0, 0, audio == null ? 0 : audio.Length / (float)rate);
            }
            protected override void ReleaseModels() { _model?.Dispose(); _model = null; }
        }
    }
}
