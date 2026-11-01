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
    public sealed partial class MNNPiper : IDisposable
    {
        private readonly object _gate = new object ();
        private readonly string _directory, _data, _executable;
        private readonly string[] _voices;
        private readonly string _phonemeVoice = "en-us";
        private readonly System.Collections.Generic.Dictionary<int, int[]> _phonemeMap;
        public string[] Languages => new[]{MNNSpeechLanguages.Normalize(_phonemeVoice)};
        private readonly int _threads;
        private readonly MNNBackendType _backendType;
        private readonly bool _standard;
        private readonly float[] _scales = {.667f, 1f, .8f};
        private MNNGenerationGraph _graph;
        private string _voice;
        private bool _disposed;
        private readonly MNNLocalSpeechFallback _languageFallback;
        private string _lastSynthesisModelDirectory;
        public string LastSynthesisModelDirectory => _lastSynthesisModelDirectory ?? _directory;
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
            string phonemizerReason = PhonemizerUnsupportedReason(UnityEngine.Application.platform);
            if (phonemizerReason != null)
                throw new PlatformNotSupportedException(phonemizerReason);
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

            SampleRate = MNNModelData.Integer(MNNModelData.Get(config, "sample_rate"));
            string configuredPath = (string)MNNModelData.Get(config, "model_path");
            _standard = !configuredPath.EndsWith("_fp16.mnn", StringComparison.Ordinal) && !configuredPath.EndsWith("_fp16_public.mnn", StringComparison.Ordinal);
            if (_standard ? SampleRate < 8000 || SampleRate > 48000 : SampleRate != 16000)
                throw new InvalidDataException("Unsupported Piper sample rate.");
            _executable = ResolveEspeak(espeakExecutable, directory);
            _voices = InstalledVoices(directory);
            if (_voices.Length == 0)
                throw new FileNotFoundException("No compatible Piper voice graphs were found.");
            string configured = Path.GetFileName(configuredPath).Replace("_fp16_public.mnn", "").Replace("_fp16.mnn", "").Replace(".mnn", "");
            if (!_voices.Contains(configured, StringComparer.Ordinal))
                throw new FileNotFoundException("Piper config references a missing voice graph: " + configured);
            if (_standard)
            {
                string metadata = FindVoiceMetadata(directory, configured);
                var voiceConfig = MNNModelData.Read(metadata);
                if (MNNModelData.Integer(MNNModelData.Get(MNNModelData.Get(voiceConfig, "audio"), "sample_rate")) != SampleRate || MNNModelData.Integer(MNNModelData.Get(voiceConfig, "num_speakers")) != 1 || (string)MNNModelData.Get(voiceConfig, "phoneme_type") != "espeak")
                    throw new NotSupportedException("Expected a single-speaker eSpeak Piper configuration.");
                _phonemeVoice = (string)MNNModelData.Get(MNNModelData.Get(voiceConfig, "espeak"), "voice");
                if (string.IsNullOrWhiteSpace(_phonemeVoice) || !Regex.IsMatch(_phonemeVoice, @"^[a-zA-Z0-9_+\-]+$"))
                    throw new InvalidDataException("Invalid Piper eSpeak voice.");
                _phonemeMap = MNNPiperPhonemes.ReadMap(MNNModelData.Get(voiceConfig, "phoneme_id_map"), MNNModelData.Integer(MNNModelData.Get(voiceConfig, "num_symbols")));
                var settings = MNNModelData.Get(voiceConfig, "inference");
                _scales[0] = (float)MNNModelData.Number(MNNModelData.Get(settings, "noise_scale"));
                _scales[1] = (float)MNNModelData.Number(MNNModelData.Get(settings, "length_scale"));
                _scales[2] = (float)MNNModelData.Number(MNNModelData.Get(settings, "noise_w"));
                if (_scales.Any(v => float.IsNaN(v) || float.IsInfinity(v) || v <= 0 || v > 10))
                    throw new InvalidDataException("Invalid Piper inference scales.");
            }

            _data = ResolveDataDirectory(directory, MNNModelData.Object(config).TryGetValue("asset_folder", out var assets) ? assets as string : null, espeakDataDirectory, Languages[0] == "en");
            _voice = configured;
            LoadGraph(_voice, threads);
            _languageFallback = new MNNLocalSpeechFallback(directory, SampleRate, threads);
        }

        private void LoadGraph(string voice, int threads)
        {
            string path = Path.Combine(_directory, voice + "_fp16_public.mnn");
            if (!File.Exists(path))
                path = Path.Combine(_directory, voice + "_fp16.mnn");
            if (_standard)
                path = Path.Combine(_directory, voice + ".mnn");
            if (!File.Exists(path))
                throw new FileNotFoundException("Missing Piper graph for voice " + voice, path);
            var graph = new MNNGenerationGraph(path, threads, express: true, backendType: _backendType);
            _graph?.Dispose();
            _graph = graph;
            _voice = voice;
        }

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
                _languageFallback?.Dispose();
                _graph?.Dispose();
                _graph = null;
            }
        }
    }
}
