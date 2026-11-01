using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    /// <summary>Reference-conditioned IndicF5 speech on the bounded macOS Mono MNN ABI.</summary>
    public sealed class MNNIndicF5Tts : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _root;
        private readonly int _threads;
        private byte[] _source;
        private readonly MNNIndicF5Frontend _frontend;
        private MNNGenerationGraph _flow, _vocos;
        private int _frames;
        private bool _disposed;

        internal static bool IsComplete(string root) => new[]{"indicf5_dit_int8wq.mnn", "indicf5_vocos_fp32_dynamic.mnn", "checkpoints/vocab.txt"}.All(file => File.Exists(Path.Combine(root, file)));

        public static MNNIndicF5Tts Load(string directory, int threads = 4)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16) throw new ArgumentOutOfRangeException(nameof(threads));
            return new MNNIndicF5Tts(Path.GetFullPath(directory), threads);
        }

        private MNNIndicF5Tts(string root, int threads)
        {
            if (!IsComplete(root)) throw new InvalidDataException("Incomplete IndicF5 assets.");
            string[] files = {"indicf5_dit_int8wq.mnn", "indicf5_vocos_fp32_dynamic.mnn", "checkpoints/vocab.txt"};
            string[] hashes = {"7d19cdb4ef19b6209b0945c900f285031c2c36882256ec10b4afeb0f45f460dd", "574450cfc77f08ca76c08f876b34a5b241ddfa8c4d2f8f7d93b53d53fcfdbf61", "d3a5ff6aac12ea4fb50628a66a97b20c1e83b9e5ca356e5afae17b511cda96df"};
            using (var sha = SHA256.Create())
            for (int i = 0; i < files.Length; ++i)
            using (var stream = File.OpenRead(Path.Combine(root, files[i])))
                if (BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != hashes[i]) throw new InvalidDataException("Unsupported IndicF5 export: " + files[i]);
            _root = root; _threads = threads; _source = File.ReadAllBytes(Path.Combine(root, files[0]));
            _frontend = new MNNIndicF5Frontend(Path.Combine(root, files[2]));
        }

        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, MNNReferenceAudio reference, string referenceText, int seed = 42, int steps = 32, float speed = 1, CancellationToken cancellationToken = default)
            => Task.Run(() => Synthesize(text, reference, referenceText, seed, steps, speed, cancellationToken), cancellationToken);

        public MNNGeneratedAudio Synthesize(string text, MNNReferenceAudio reference, string referenceText, int seed = 42, int steps = 32, float speed = 1, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNIndicF5Tts));
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(text) || text.Length > 512) throw new ArgumentException("Provide at most 512 target characters.", nameof(text));
                if (reference == null) throw new ArgumentNullException(nameof(reference));
                if (reference.Duration > 12) throw new ArgumentException("IndicF5 reference must be at most 12 seconds.", nameof(reference));
                if (string.IsNullOrWhiteSpace(referenceText) || referenceText.Length > 1024) throw new ArgumentException("Provide the exact reference transcript, at most 1024 characters.", nameof(referenceText));
                if (steps < 8 || steps > 64) throw new ArgumentOutOfRangeException(nameof(steps));
                if (float.IsNaN(speed) || speed < .5f || speed > 2) throw new ArgumentOutOfRangeException(nameof(speed));
                if (referenceText[referenceText.Length - 1] < 128) referenceText += " ";
                float[] pcm = MNNSpeechDsp.Resample(reference.Samples, reference.SampleRate, 24000, cancellationToken);
                double rms = Math.Sqrt(pcm.Select(v => (double)v * v).Average());
                if (rms < .1) for (int i = 0; i < pcm.Length; ++i) pcm[i] *= (float)(.1 / rms);
                float[] mel = _frontend.Mel(pcm, out int conditionFrames, cancellationToken);
                int referenceFrames = pcm.Length / 256;
                int[] tokens = _frontend.Encode(referenceText + text);
                int frames = Math.Max(Math.Max(tokens.Length, conditionFrames) + 1, referenceFrames + (int)(referenceFrames * (double)Encoding.UTF8.GetByteCount(text) / Encoding.UTF8.GetByteCount(referenceText) / speed));
                if (frames > 2048 || frames - referenceFrames < 32) throw new ArgumentException("Estimated speech duration must fit 2048 total frames and at least 32 new frames.", nameof(text));
                var cond = new float[frames * 100]; Array.Copy(mel, cond, mel.Length);
                var ids = Enumerable.Repeat(-1, frames).ToArray(); Array.Copy(tokens, ids, tokens.Length);
                string path = Path.Combine(_root, "indicf5_dit_int8wq.mnn");
                if (_flow == null || frames != _frames)
                {
                    _flow?.Dispose(); _flow = null;
                    _flow = new MNNGenerationGraph(path, _threads, MNNIndicF5Graph.Prepare(_source, frames), backendType: MNNBackendType.CPU); _frames = frames;
                }
                var x = MNNGenerationMath.Gaussian(frames * 100, seed);
                var emptyCond = new float[cond.Length]; var emptyIds = Enumerable.Repeat(-1, frames).ToArray();
                for (int step = 0; step < steps; ++step)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    float time = (float)(1 - Math.Cos(Math.PI * .5 * step / steps));
                    float next = (float)(1 - Math.Cos(Math.PI * .5 * (step + 1) / steps));
                    Input[] Inputs(float[] audio, int[] chars) => new[]{new Input("x", x, 1, frames, 100), new Input("cond", audio, 1, frames, 100), new Input("text", chars, 1, frames), new Input("time", new[]{time}, 1)};
                    float[] pred = _flow.Run("pred", Inputs(cond, ids));
                    cancellationToken.ThrowIfCancellationRequested();
                    float[] unconditional = _flow.Run("pred", Inputs(emptyCond, emptyIds));
                    if (pred.Length != x.Length || unconditional.Length != x.Length) throw new InvalidDataException("Invalid IndicF5 flow shape.");
                    for (int i = 0; i < x.Length; ++i) x[i] += (3 * pred[i] - 2 * unconditional[i]) * (next - time);
                }
                int generated = frames - referenceFrames;
                var outputMel = new float[generated * 100];
                for (int t = 0; t < generated; ++t)
                for (int c = 0; c < 100; ++c)
                    outputMel[c * generated + t] = t + referenceFrames < conditionFrames ? mel[(t + referenceFrames) * 100 + c] : x[(t + referenceFrames) * 100 + c];
                cancellationToken.ThrowIfCancellationRequested();
                if (_vocos == null) _vocos = new MNNGenerationGraph(Path.Combine(_root, "indicf5_vocos_fp32_dynamic.mnn"), _threads, backendType: MNNBackendType.CPU);
                float[] audio = _vocos.Run("wav", new Input("mel", outputMel, 1, 100, generated));
                if (audio.Length != (generated - 1) * 256) throw new InvalidDataException("Invalid IndicF5 vocoder length.");
                if (rms < .1) for (int i = 0; i < audio.Length; ++i) audio[i] *= (float)(rms / .1);
                cancellationToken.ThrowIfCancellationRequested();
                return MNNSpeechDsp.Result(audio, 24000, text);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _flow?.Dispose(); _vocos?.Dispose(); _source = null;
            }
        }
    }
}
