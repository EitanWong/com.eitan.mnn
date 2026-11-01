using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    public sealed partial class MNNSupertonic
    {
        internal static string[] InstalledVoices(string directory) => new[]{"M1", "M2", "M3", "M4", "M5", "F1", "F2", "F3", "F4", "F5"}.Where(name => File.Exists(VoicePath(directory, name))).ToArray();
        private static string VoicePath(string directory, string name)
        {
            string path = Path.Combine(directory, "voice_styles", name + ".json");
            return File.Exists(path) ? path : Path.Combine(directory, "mnn_models", "voice_styles", name + ".json");
        }

        internal static bool IsComplete(string directory) => File.Exists(Path.Combine(directory, "mnn_models", "tts.json")) && File.Exists(Path.Combine(directory, "mnn_models", "unicode_indexer.json")) && InstalledVoices(directory).Length > 0 && new[]{"fp16", "fp32", "int8"}.Any(precision => new[]{"duration_predictor", "text_encoder", "vector_estimator", "vocoder"}.All(stage => File.Exists(Path.Combine(directory, "mnn_models", precision, stage + ".mnn"))));
        public static MNNSupertonic Load(string directory, int threads = 4, string precision = "fp16", MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (threads < 1 || threads > 16)
                throw new ArgumentOutOfRangeException(nameof(threads));
            if (precision != "fp16" && precision != "fp32" && precision != "int8")
                throw new ArgumentException("Use fp16, fp32 or int8.", nameof(precision));
            return new MNNSupertonic(Path.GetFullPath(directory), threads, precision, backendType);
        }

        internal static MNNBackendType ForAndroidDevice(MNNBackendType requested, string precision, string graphicsDevice)
        {
            // PJA110 / Adreno 740 INT8 Supertonic crashes while copying input to
            // an OpenCL tensor mapping. Mali-G57 is measured faster on CPU.
            // Neither native mapping faults nor GPU process exits can be retried.
            if (requested == MNNBackendType.Auto && precision == "int8" &&
                ((graphicsDevice ?? string.Empty).StartsWith("Mali-G57", StringComparison.OrdinalIgnoreCase) ||
                    MNNInferenceCache.IsAdreno740(graphicsDevice)))
                return MNNBackendType.CPU;
            return requested;
        }

        private MNNSupertonic(string directory, int threads, string precision, MNNBackendType backendType)
        {
            string configPath = Path.Combine(directory, "config.json");
            if (File.Exists(configPath) && (string)MNNModelData.Get(MNNModelData.Read(configPath), "model_type") != "supertonic")
                throw new NotSupportedException("Expected a Supertonic repository, not another TTS architecture.");
            var metadata = MNNModelData.Read(Path.Combine(directory, "mnn_models", "tts.json"));
            Version = (string)MNNModelData.Get(metadata, "tts_version");
            if (Version != "v1.5.0" && Version != "v1.6.0" && Version != "v1.7.3")
                throw new NotSupportedException("Unsupported Supertonic version: " + Version);
            var ae = MNNModelData.Get(metadata, "ae");
            var ttl = MNNModelData.Get(metadata, "ttl");
            _sampleRate = MNNModelData.Integer(MNNModelData.Get(ae, "sample_rate"));
            int compress = MNNModelData.Integer(MNNModelData.Get(ttl, "chunk_compress_factor"));
            _chunk = checked(MNNModelData.Integer(MNNModelData.Get(ae, "base_chunk_size")) * compress);
            _latentDim = checked(MNNModelData.Integer(MNNModelData.Get(ttl, "latent_dim")) * compress);
            if (_sampleRate != 44100 || _chunk != 3072 || _latentDim != 144)
                throw new NotSupportedException("Unsupported Supertonic graph configuration. Expected mono 44.1 kHz, chunk 3072 and latent width 144.");
            object indexer = MNNModelData.Read(Path.Combine(directory, "mnn_models", "unicode_indexer.json"));
            if (indexer is List<object>)
                _indexer = MNNModelData.Shape(indexer);
            else
            {
                _indexer = Enumerable.Repeat(-1, 65536).ToArray();
                foreach (var pair in MNNModelData.Object(indexer))
                    _indexer[int.Parse(pair.Key)] = MNNModelData.Integer(pair.Value);
            }

            foreach (string name in InstalledVoices(directory))
            {
                string path = VoicePath(directory, name);
                var style = MNNModelData.Read(path);
                var t = MNNModelData.Get(style, "style_ttl");
                var d = MNNModelData.Get(style, "style_dp");
                var voice = new Voice{Ttl = MNNModelData.Floats(MNNModelData.Get(t, "data")), Dp = MNNModelData.Floats(MNNModelData.Get(d, "data")), TtlShape = MNNModelData.Shape(MNNModelData.Get(t, "dims")), DpShape = MNNModelData.Shape(MNNModelData.Get(d, "dims"))};
                if (!voice.TtlShape.SequenceEqual(new[]{1, 50, 256}) || voice.Ttl.Length != 12800 || !voice.DpShape.SequenceEqual(new[]{1, 8, 16}) || voice.Dp.Length != 128)
                    throw new InvalidDataException("Unsupported Supertonic voice style: " + name);
                _voices.Add(name, voice);
            }

            if (_voices.Count == 0)
                throw new FileNotFoundException("No Supertonic voice styles found.");
            string graphs = Path.Combine(directory, "mnn_models", precision);
            foreach (string name in new[]{"duration_predictor", "text_encoder", "vector_estimator", "vocoder"})
                if (!File.Exists(Path.Combine(graphs, name + ".mnn")))
                    throw new FileNotFoundException("Missing Supertonic " + precision + " model: " + name);
            try
            {
                // These graphs can return finite but incorrect results on the
                // bundled Metal runtime: duration changes for long/numeric text,
                // text encoding/estimation loses speech, and vocoder emits large
                // spikes for some latent lengths. Use the validated CPU pipeline
                // when Metal is requested, including through Auto.
                var speechRequest = backendType;
#if UNITY_ANDROID && !UNITY_EDITOR
                speechRequest = ForAndroidDevice(backendType, precision, MNNInferenceCache.GraphicsDevice);
#endif
                var speechBackend = MNNAcceleration.Resolve(MNNAcceleration.ForDynamicGraph(speechRequest));
                if (speechBackend == MNNBackendType.Metal)
                    speechBackend = MNNBackendType.CPU;
                _duration = new MNNGenerationGraph(Path.Combine(graphs, "duration_predictor.mnn"), threads, backendType: speechBackend);
                _text = new MNNGenerationGraph(Path.Combine(graphs, "text_encoder.mnn"), threads, backendType: speechBackend);
                _estimator = new MNNGenerationGraph(Path.Combine(graphs, "vector_estimator.mnn"), threads, backendType: speechBackend);
                _vocoder = new MNNGenerationGraph(Path.Combine(graphs, "vocoder.mnn"), threads, backendType: speechBackend);
                _languageFallback = new MNNLocalSpeechFallback(directory, _sampleRate, threads);
            }
            catch
            {
                Dispose();
                throw;
            }
        }
    }
}
