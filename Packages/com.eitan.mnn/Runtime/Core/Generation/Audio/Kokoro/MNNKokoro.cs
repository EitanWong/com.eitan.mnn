using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    /// <summary>Kokoro speech using repository IPA lexicons, voice style tables and official MNN Module APIs.</summary>
    public sealed class MNNKokoro : IDisposable
    {
        private readonly object _gate = new object();
        private readonly string _directory;
        private byte[] _graph;
        private readonly int _threads;
        private readonly bool _sherpa;
        private readonly string _graphPath;
        private MNNKokoroText _text;
        private readonly Dictionary<string, float[]> _styles = new Dictionary<string, float[]>(StringComparer.Ordinal);
        private MNNSingleInputModule _module;
        private string _voice;
        private int _length;
        private float _speed;
        private bool _disposed;
        public int SampleRate => 24000;
        private readonly string[] _voices;
        public string[] Voices => (string[])_voices.Clone();

        public static string[] InstalledVoices(string directory) => MNNKokoroModel.IsSherpa(directory) ? MNNKokoroModel.SherpaVoices(directory) : Directory.Exists(Path.Combine(directory, "voices"))
            ? Directory.GetFiles(Path.Combine(directory, "voices"), "*.bin").Where(path => new FileInfo(path).Length == 510 * 256 * 4)
                .Select(Path.GetFileNameWithoutExtension).OrderBy(name => name, StringComparer.Ordinal).ToArray()
            : MNNKokoroModel.ConstantVoiceNames(directory);

        internal static bool IsComplete(string directory) => File.Exists(Path.Combine(directory, "tts.mnn"))
            && File.Exists(Path.Combine(directory, "tokenizer.txt"))
            && (File.Exists(Path.Combine(directory, "lexicon-zh.txt")) || File.Exists(Path.Combine(directory, "lexicon-us-en.txt")))
            && InstalledVoices(directory).Length > 0;

        public static MNNKokoro Load(string directory, int threads = 4)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16)
                throw new ArgumentOutOfRangeException(nameof(threads));
            return new MNNKokoro(Path.GetFullPath(directory), threads);
        }

        private MNNKokoro(string directory, int threads)
        {
            if (!MNNKokoroModel.IsComplete(directory))
                throw new FileNotFoundException("Kokoro needs tts.mnn, tokenizer.txt, matching lexicons and voices/*.bin (510 × 256 float32).");
            _directory = directory;
            _threads = threads;
            _sherpa = MNNKokoroModel.IsSherpa(directory);
            _graphPath = Path.Combine(directory, _sherpa ? "model.mnn" : "tts.mnn");
            _voices = InstalledVoices(directory);
            _text = new MNNKokoroText(directory, _sherpa);
            _graph = File.ReadAllBytes(_graphPath);
            if (_sherpa)
                _graph = MNNGraphExternalWeights.Bind(_graph, _graphPath + ".weight");
        }

        public MNNGeneratedAudio Synthesize(string text, string voice = null, float speed = 1, CancellationToken cancellationToken = default)
        {
            if (float.IsNaN(speed) || speed < .5f || speed > 2)
                throw new ArgumentOutOfRangeException(nameof(speed));
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNKokoro));
                cancellationToken.ThrowIfCancellationRequested();
                voice = voice ?? _voices[0];
                if (!_voices.Contains(voice, StringComparer.Ordinal))
                    throw new ArgumentException("Select an installed Kokoro voice.", nameof(voice));
                var parts = _text.Encode(text); // Validate all text before any native work.
                if (!_styles.TryGetValue(voice, out float[] table))
                {
                    if (!_sherpa && !Directory.Exists(Path.Combine(_directory, "voices")))
                        table = MNNKokoroModel.ConstantVoice(_directory, Array.IndexOf(_voices, voice), _voices.Length);
                    else
                    {
                        byte[] bytes;
                        if (_sherpa)
                        {
                            using (var stream = File.OpenRead(Path.Combine(_directory, "voices.bin")))
                            using (var reader = new BinaryReader(stream))
                            {
                                stream.Position = Array.IndexOf(_voices, voice) * 510L * 256 * 4;
                                bytes = reader.ReadBytes(510 * 256 * 4);
                            }
                        }
                        else
                            bytes = File.ReadAllBytes(Path.Combine(_directory, "voices", voice + ".bin"));
                        if (bytes.Length != 510 * 256 * 4 || !BitConverter.IsLittleEndian)
                            throw new InvalidDataException("Expected a little-endian Kokoro voice table.");
                        table = new float[510 * 256];
                        Buffer.BlockCopy(bytes, 0, table, 0, bytes.Length);
                    }
                    if (table.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                        throw new InvalidDataException("Non-finite Kokoro voice style.");
                    _styles.Add(voice, table);
                }
                var waves = new List<float[]>();
                foreach (int[] phones in parts)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (_module == null || _voice != voice || _length != phones.Length || _speed != speed)
                    {
                        _module?.Dispose();
                        _module = null;
                        float[] style = new float[256];
                        Array.Copy(table, phones.Length * 256, style, 0, 256);
                        byte[] prepared = MNNGraphConstant.FreezeInputs(_graph, new Input("style", style, 1, 256), new Input("speed", new[]{speed}, 1));
                        // Control-flow exports require Module; keep the established
                        // CPU contract until another backend passes content checks.
                        _module = new MNNSingleInputModule(_graphPath, _sherpa ? "tokens" : "input_ids", _sherpa ? "audio" : "waveform", _threads, MNNBackendType.CPU, prepared, MNNPrecisionMode.High);
                        _voice = voice;
                        _length = phones.Length;
                        _speed = speed;
                    }
                    var ids = new int[phones.Length + 2];
                    Array.Copy(phones, 0, ids, 1, phones.Length);
                    float[] wave = _module.Run(ids, 1, ids.Length);
                    cancellationToken.ThrowIfCancellationRequested();
                    if (wave.Length < SampleRate / 10 || wave.Length > SampleRate * 30)
                        throw new InvalidDataException("Kokoro returned an invalid waveform length: " + wave.Length);
                    float rawPeak = wave.Max(v => Math.Abs(v));
                    if (wave.Any(v => float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 2))
                        throw new InvalidDataException("Kokoro returned a non-finite or excessive waveform (length " + wave.Length + ", peak " + rawPeak + ").");
                    float peak = rawPeak;
                    if (peak > 1)
                        for (int i = 0; i < wave.Length; ++i) wave[i] /= peak;
                    waves.Add(wave);
                }
                return new MNNGeneratedAudio(MNNSpeechDsp.JoinWaveforms(waves, SampleRate), SampleRate, text);
            }
        }

        public Task<MNNGeneratedAudio> SynthesizeAsync(string text, string voice = null, float speed = 1, CancellationToken cancellationToken = default)
            => Task.Run(() => Synthesize(text, voice, speed, cancellationToken), cancellationToken);

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _module?.Dispose();
                _module = null;
                _styles.Clear();
                _graph = null;
                _text = null;
            }
        }
    }
}
