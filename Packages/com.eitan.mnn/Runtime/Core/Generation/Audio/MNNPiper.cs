using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    /// <summary>Runs MNN Piper voice graphs with the upstream eSpeak-NG phonemizer command.</summary>
    public sealed class MNNPiper : IDisposable
    {
        private readonly object _gate = new object ();
        private readonly string _directory, _data, _executable;
        private readonly string[] _voices;
        private readonly int _threads;
        private readonly MNNBackendType _backendType;
        private MNNGenerationGraph _graph;
        private string _voice;
        private bool _disposed;
        public int SampleRate { get; }

        public string[] Voices => (string[])_voices.Clone();
        public MNNBackendType Backend
        {
            get
            {
                lock (_gate)
                {
                    Check();
                    return _graph.Backend;
                }
            }
        }

        public static MNNPiper Load(string directory, int threads = 1, string espeakExecutable = null, string espeakDataDirectory = null, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16)
                throw new ArgumentOutOfRangeException(nameof(threads));
            return new MNNPiper(Path.GetFullPath(directory), threads, espeakExecutable, espeakDataDirectory, backendType);
        }

        private MNNPiper(string directory, int threads, string espeakExecutable, string espeakDataDirectory, MNNBackendType backendType)
        {
            var config = MNNModelData.Read(Path.Combine(directory, "config.json"));
            if ((string)MNNModelData.Get(config, "model_type") != "piper")
                throw new NotSupportedException("Expected an MNN Piper voice repository.");
            _directory = directory;
            _threads = threads;
            // The bundled Metal path for these fp16 voice exports produces a
            // finite waveform that fails transcription even with CPU ASR.
            // Avoid treating a nonzero waveform as proof of GPU compatibility.
            _backendType = MNNAcceleration.Resolve(backendType);
            if (_backendType != MNNBackendType.CPU)
            {
                UnityEngine.Debug.Log("[MNN] Piper fp16 voice graphs use CPU for validated speech quality with the bundled MNN build.");
                _backendType = MNNBackendType.CPU;
            }

            _data = ResolveDataDirectory(directory, MNNModelData.Object(config).TryGetValue("asset_folder", out var assets) ? assets as string : null, espeakDataDirectory);
            SampleRate = MNNModelData.Integer(MNNModelData.Get(config, "sample_rate"));
            if (SampleRate != 16000)
                throw new InvalidDataException("Unsupported Piper sample rate.");
            _executable = ResolveEspeak(espeakExecutable, directory);
            _voices = Directory.GetFiles(directory, "*_fp16*.mnn").Select(Path.GetFileName).Select(name => name.Replace("_fp16_public.mnn", "").Replace("_fp16.mnn", "")).Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            if (_voices.Length == 0)
                throw new FileNotFoundException("No Piper fp16 voice graphs were found.");
            string configured = Path.GetFileName((string)MNNModelData.Get(config, "model_path")).Replace("_fp16_public.mnn", "").Replace("_fp16.mnn", "");
            if (!_voices.Contains(configured, StringComparer.Ordinal))
                throw new FileNotFoundException("Piper config references a missing voice graph: " + configured);
            _voice = configured;
            LoadGraph(_voice, threads);
        }

        public MNNGeneratedAudio Synthesize(string text, string voice = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 1000)
                throw new ArgumentException("Enter 1–1000 characters for Piper.", nameof(text));
            lock (_gate)
            {
                Check();
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(voice))
                    voice = _voice;
                if (!_voices.Contains(voice, StringComparer.Ordinal))
                    throw new ArgumentException("Select an installed Piper voice.", nameof(voice));
                if (voice != _voice)
                    LoadGraph(voice, _threads);
                var ipa = new StringBuilder();
                foreach (string clause in SplitClauses(text))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string value = clause.Trim();
                    if (value.Length == 0)
                        continue;
                    char last = value[value.Length - 1];
                    bool punctuation = ".,;:!?".IndexOf(last) >= 0;
                    string words = punctuation ? value.Substring(0, value.Length - 1) : value;
                    if (!string.IsNullOrWhiteSpace(words))
                        ipa.Append(Phonemize(words)).Append(punctuation ? "" : " ");
                    if (punctuation)
                        ipa.Append(last).Append(' ');
                }

                int[] ids = MNNPiperPhonemes.Encode(ipa.ToString().Trim());
                if (ids.Length <= 3)
                    throw new InvalidDataException("eSpeak-NG returned no Piper phonemes.");
                cancellationToken.ThrowIfCancellationRequested();
                var samples = _graph.Run("output", new Input("input", ids, 1, ids.Length), new Input("input_lengths", new[]{ids.Length}, 1), new Input("scales", new[]{.667f, 1f, .8f}, 3));
                cancellationToken.ThrowIfCancellationRequested();
                if (samples.Length < SampleRate / 10 || samples.Length > SampleRate * 60)
                    throw new InvalidDataException("Piper returned an invalid waveform length.");
                for (int i = 0; i < samples.Length; ++i)
                    samples[i] = Math.Max(-1, Math.Min(1, samples[i]));
                return new MNNGeneratedAudio(samples, SampleRate, text);
            }
        }

        internal static string[] SplitClauses(string text) => Regex.Split(text, @"(?<=[.,;:!?])(?![0-9])");
        private string Phonemize(string text)
        {
            var info = new ProcessStartInfo{FileName = _executable, Arguments = "-q --ipa=3 -v en-us --path " + Quote(Path.GetDirectoryName(_data)), UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8};
            using (var process = Process.Start(info))
            {
                process.StandardInput.WriteLine(text);
                process.StandardInput.Close();
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000))
                {
                    process.Kill();
                    throw new TimeoutException("eSpeak-NG phonemization timed out.");
                }

                string output = outputTask.GetAwaiter().GetResult().Trim();
                string error = errorTask.GetAwaiter().GetResult();
                if (process.ExitCode != 0)
                    throw new InvalidOperationException("eSpeak-NG failed: " + error.Trim());
                return Regex.Replace(output, @"\([a-z-]+\)", "").Replace("\r", " ").Replace("\n", " ");
            }
        }

        private void LoadGraph(string voice, int threads)
        {
            string path = Path.Combine(_directory, voice + "_fp16_public.mnn");
            if (!File.Exists(path))
                path = Path.Combine(_directory, voice + "_fp16.mnn");
            if (!File.Exists(path))
                throw new FileNotFoundException("Missing Piper graph for voice " + voice, path);
            var graph = new MNNGenerationGraph(path, threads, express: true, backendType: _backendType);
            _graph?.Dispose();
            _graph = graph;
            _voice = voice;
        }

        internal static readonly string[] RequiredDataFiles = {"phontab", "phondata", "phonindex", "intonations", "en_dict", "lang/gmw/en"};
        internal static string ResolveDataDirectory(string directory, string configured = null, string selected = null)
        {
            if (!string.IsNullOrWhiteSpace(selected))
            {
                if (Directory.Exists(selected) && RequiredDataFiles.All(file => File.Exists(Path.Combine(selected, file))))
                    return Path.GetFullPath(selected);
                throw new DirectoryNotFoundException("Choose the complete espeak-ng-data folder containing phontab, phondata, phonindex, intonations and English language data.");
            }

            var candidates = new[]{string.IsNullOrWhiteSpace(configured) ? null : Path.Combine(directory, configured), Path.Combine(directory, "espeak-ng-data")};
            foreach (string path in candidates.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct())
                if (Directory.Exists(path) && RequiredDataFiles.All(file => File.Exists(Path.Combine(path, file))))
                    return Path.GetFullPath(path);
            throw new FileNotFoundException("Piper's espeak-ng-data is missing or incomplete. Download the complete model repository; the data folder is found automatically.", Path.Combine(directory, "espeak-ng-data"));
        }

        internal static string FindEspeak(string directory = null)
        {
            if (!string.IsNullOrWhiteSpace(directory))
                foreach (string relative in new[]{"espeak-ng", "bin/espeak-ng", "espeak-ng.exe", "bin/espeak-ng.exe"})
                    if (IsEspeakExecutable(Path.Combine(directory, relative)))
                        return Path.GetFullPath(Path.Combine(directory, relative));
            string env = Environment.GetEnvironmentVariable("MNN_ESPEAK_NG_PATH");
            if (IsEspeakExecutable(env))
                return Path.GetFullPath(env);
            foreach (string path in new[]{"/opt/homebrew/bin/espeak-ng", "/usr/local/bin/espeak-ng", "/usr/bin/espeak-ng"})
                if (IsEspeakExecutable(path))
                    return path;
            foreach (string folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p)))
                foreach (string name in new[]{"espeak-ng", "espeak-ng.exe"})
                    if (IsEspeakExecutable(Path.Combine(folder, name)))
                        return Path.GetFullPath(Path.Combine(folder, name));
            return null;
        }

        internal static bool IsEspeakExecutable(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return false;
            try
            {
                // Dictionaries and language data must never become process targets,
                // even if an older Studio preference or environment variable names one.
                string full = Path.GetFullPath(path);
                if (full.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => string.Equals(part, "espeak-ng-data", StringComparison.OrdinalIgnoreCase)))
                    return false;
                using (var stream = File.OpenRead(full))
                {
                    var header = new byte[4];
                    if (stream.Read(header, 0, header.Length) != header.Length)
                        return false;
                    // Allow upstream binaries (ELF, PE, Mach-O) and script wrappers.
                    if (header[0] == '#' && header[1] == '!')
                        return true;
                    if (header[0] == 0x7f && header[1] == 'E' && header[2] == 'L' && header[3] == 'F')
                        return true;
                    if (header[0] == 'M' && header[1] == 'Z')
                        return true;
                    uint magic = ((uint)header[0] << 24) | ((uint)header[1] << 16) | ((uint)header[2] << 8) | header[3];
                    return magic == 0xfeedface || magic == 0xcefaedfe || magic == 0xfeedfacf || magic == 0xcffaedfe || magic == 0xcafebabe || magic == 0xbebafeca || magic == 0xcafebabf || magic == 0xbfbafeca;
                }
            }
            catch (Exception error)when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException || error is NotSupportedException)
            {
                return false;
            }
        }

        internal static string ResolveEspeak(string requested, string directory = null)
        {
            if (!string.IsNullOrWhiteSpace(requested))
            {
                if (Directory.Exists(requested))
                    throw new ArgumentException("Select the eSpeak-NG executable file. The model's espeak-ng-data folder is found automatically.", nameof(requested));
                if (!File.Exists(requested))
                    throw new FileNotFoundException("The selected eSpeak-NG executable does not exist.", requested);
                if (!IsEspeakExecutable(requested))
                    throw new ArgumentException("The eSpeak-NG tool path must name a program or script wrapper, not an espeak-ng-data dictionary or language file. Studio's Browse selects the data folder; install eSpeak-NG separately or set MNN_ESPEAK_NG_PATH to its executable.", nameof(requested));
                return Path.GetFullPath(requested);
            }

            return FindEspeak(directory) ?? throw new FileNotFoundException("Piper's data folder was found, but the eSpeak-NG executable is missing. Install eSpeak-NG or choose its executable file.");
        }

        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        private void Check()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MNNPiper));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _graph?.Dispose();
                _graph = null;
            }
        }
    }
}
