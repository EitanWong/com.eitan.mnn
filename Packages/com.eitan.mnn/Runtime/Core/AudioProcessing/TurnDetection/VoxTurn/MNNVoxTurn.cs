using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
namespace MNN.Unity
{
    /// <summary>VoxTurn v1 multimodal turn decisions at 16 kHz, with original unmerged LoRA.</summary>
    public sealed class MNNVoxTurn : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNVoxTurnPrompt _prompt;
        private readonly MNNVoxTurnFeatures _features;
        private readonly MNNVoxTurnWeights _weights;
        private readonly MNNGenerationGraph _audio;
        private readonly MNNVoxTurnLanguage _language;
        private bool _disposed;
        public MNNBackendType AudioBackend => _audio.Backend;
        public MNNBackendType FirstLanguageBackend => _language.FirstBackend;
        public MNNBackendType SecondLanguageBackend => _language.SecondBackend;
        private MNNVoxTurn(string directory, int threads, MNNBackendType languageBackend)
        {
            MNNVoxTurnModel.Verify(directory);
            try
            {
                _prompt = new MNNVoxTurnPrompt(directory);
                _features = new MNNVoxTurnFeatures(MNNVoxTurnModel.Floats(directory, "mel-window.bin", 400), MNNVoxTurnModel.Floats(directory, "mel-filters.bin", 128 * 201));
                _weights = new MNNVoxTurnWeights(directory);
                _audio = Graph(directory, "audio_encoder.mnn", threads, MNNBackendType.CPU);
                _language = new MNNVoxTurnLanguage(directory, threads, languageBackend);
            }
            catch { DisposeCore(); throw; }
        }
        private static MNNGenerationGraph Graph(string directory, string file, int threads, MNNBackendType backend) =>
            new MNNGenerationGraph(Path.Combine(directory, file), threads, backendType: backend, precisionMode: MNNPrecisionMode.High, gpuTuningMode: MNNGpuTuningMode.None, recreateGpuSessionOnShapeChange: true);
        /// <summary>Auto uses CPU for text and available Metal language stages for audio requests. Backend properties report the latest sessions.</summary>
        public static MNNVoxTurn Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException(nameof(directory));
            if (threadCount != 4)
                throw new ArgumentOutOfRangeException(nameof(threadCount), "The qualified VoxTurn profile uses four CPU threads.");
            if (backendType != MNNBackendType.CPU && backendType != MNNBackendType.Metal && backendType != MNNBackendType.Auto)
                throw new NotSupportedException("VoxTurn supports CPU and the qualified CPU-audio/Metal-language path.");
            MNNPlatformSupport.RequireSupported();
            if (!BitConverter.IsLittleEndian)
                throw new PlatformNotSupportedException("VoxTurn constants require little endian.");
            return new MNNVoxTurn(Path.GetFullPath(directory), threadCount, backendType == MNNBackendType.Auto && !MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.CPU : backendType);
        }
        public float EndOfTurn(float[] samples = null, string text = null, string language = "en", string speaker = "user") =>
            Predict(new MNNVoxTurnRequest(samples, text, language, speaker)).Answers["eot"].Probability("yes");
        public MNNVoxTurnResult Predict(MNNVoxTurnRequest request, string[] questions = null, CancellationToken cancellationToken = default) =>
            PredictBatch(new[] { request }, questions, cancellationToken)[0];
        /// <summary>Evaluate one or two immutable requests, retaining block-isolating attention across multiple questions.</summary>
        public IReadOnlyList<MNNVoxTurnResult> PredictBatch(IReadOnlyList<MNNVoxTurnRequest> requests, string[] questions = null, CancellationToken cancellationToken = default)
        {
            if (requests == null || requests.Count < 1 || requests.Count > 2)
                throw new ArgumentException("Supply one or two requests.", nameof(requests));
            var input = requests.ToArray();
            if (input.Any(r => r == null))
                throw new ArgumentException("Requests cannot contain null.", nameof(requests));
            var tasks = questions == null ? new[] { "eot" } : (string[])questions.Clone();
            if (tasks.Length < 1 || tasks.Length > 6 || tasks.Distinct(StringComparer.Ordinal).Count() != tasks.Length || tasks.Any(t => !MNNVoxTurnPrompt.Tasks.Contains(t)))
                throw new ArgumentException("Choose unique tasks from eot, state4, state_ten, intent, invalid and resume.", nameof(questions));
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNVoxTurn));
                cancellationToken.ThrowIfCancellationRequested();
                var batch = _prompt.Build(input, tasks, _language.MinimumLength(input.Any(request => request.HasAudio)));
                float[] hidden = _weights.Embed(batch.Ids);
                EncodeAudio(input, batch, hidden, cancellationToken);
                float[] cos = _weights.Rotary(_weights.Cos, batch.Positions), sin = _weights.Rotary(_weights.Sin, batch.Positions);
                hidden = _language.Run(hidden, batch, cos, sin, cancellationToken);
                var answers = Enumerable.Range(0, input.Length).Select(_ => new Dictionary<string, MNNVoxTurnAnswer>(StringComparer.Ordinal)).ToArray();
                foreach (var option in batch.Options)
                    answers[option.Row].Add(option.Task, new MNNVoxTurnAnswer(option.Task, option.Labels, _weights.Score(hidden, batch.Length, option)));
                cancellationToken.ThrowIfCancellationRequested();
                return Array.AsReadOnly(answers.Select(a => new MNNVoxTurnResult(a)).ToArray());
            }
        }
        private void EncodeAudio(MNNVoxTurnRequest[] input, MNNVoxTurnPrompt.Batch batch, float[] embeddings, CancellationToken token)
        {
            if (!input.Any(request => request.HasAudio))
                return;
            var mel = new float[input.Length * 128 * 800];
            for (int b = 0; b < input.Length; ++b)
                Array.Copy(_features.Extract(input[b].Samples, token), 0, mel, b * 128 * 800, 128 * 800);
            token.ThrowIfCancellationRequested();
            float[] audio = _audio.Run("audio_tokens", new MNNGenerationGraph.Input("mel", mel, input.Length, 128, 800));
            if (audio.Length != input.Length * 104 * 1024)
                throw new InvalidDataException("Invalid VoxTurn audio token shape.");
            for (int b = 0; b < input.Length; ++b)
                if (batch.AudioOffsets[b] >= 0)
                    Array.Copy(audio, b * 104 * 1024, embeddings, (b * batch.Length + batch.AudioOffsets[b]) * 1024, 104 * 1024);
        }
        private void DisposeCore()
        {
            _language?.Dispose();
            _audio?.Dispose();
            _weights?.Dispose();
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                DisposeCore();
            }
        }
    }
}
