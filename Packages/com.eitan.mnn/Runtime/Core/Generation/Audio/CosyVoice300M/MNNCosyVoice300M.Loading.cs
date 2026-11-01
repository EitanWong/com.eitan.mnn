using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Unity
{
    public sealed partial class MNNCosyVoice300M
    {
        private void LoadCore(string root, int threads, MNNBackendType acousticBackend)
        {
            _threads = threads;
            _modelRoot = root;
            _flowPath = Path.Combine(root, "fp16/flow_estimator.mnn");
            string conservativeGraph = MNNCosyVoice300MModel.ConservativeGraph(DecoderMode);
            if (conservativeGraph != null && (!File.Exists(Path.Combine(root, conservativeGraph)) ||
                !File.Exists(Path.Combine(root, conservativeGraph + ".weight"))))
                throw new FileNotFoundException("The requested INT8 CosyVoice-300M candidate is missing.", conservativeGraph);
            if (ExperimentalQuantizedDecoder && (!File.Exists(Path.Combine(root, MNNCosyVoice300MModel.QuantizedGraph)) ||
                !File.Exists(Path.Combine(root, MNNCosyVoice300MModel.QuantizedGraph + ".weight"))))
                throw new FileNotFoundException("The experimental CosyVoice-300M decoder is missing.");
            string common = Path.Combine(root, "common");
            _tokenizer = new MNNCosyVoice300MTokenizer(common);
            float[] speaker;
            using (var zip = ZipFile.OpenRead(Path.Combine(common, "prompt.npz")))
            {
                _promptText = MNNNumpy.Ints(zip, "text_tokens", 1, 16);
                _promptSpeech = MNNNumpy.Ints(zip, "speech_tokens", 1, 174);
                _promptMel = MNNNumpy.Floats(zip, "mel", 1, 299, 80);
                speaker = MNNNumpy.Floats(zip, "speaker", 1, 192);
            }
            double norm = 0;
            foreach (float v in speaker) norm += v * v;
            if (norm < 1e-12) throw new InvalidDataException("Invalid reference speaker.");
            for (int i = 0; i < speaker.Length; ++i) speaker[i] /= (float)Math.Sqrt(norm);
            using (var zip = ZipFile.OpenRead(Path.Combine(common, "llm_embeddings.npz")))
            {
                _specialEmbedding = MNNNumpy.Floats(zip, "llm_embedding_weight", 2, 1024);
                _speechEmbedding = MNNNumpy.Floats(zip, "speech_embedding_weight", 4096, 1024);
                _speakerEmbedding = Project(speaker, MNNNumpy.Floats(zip, "spk_embed_affine_layer_weight", 1024, 192),
                    MNNNumpy.Floats(zip, "spk_embed_affine_layer_bias", 1024));
            }
            using (var zip = ZipFile.OpenRead(Path.Combine(common, "flow_speaker.npz")))
                _flowSpeaker = Project(speaker, MNNNumpy.Floats(zip, "spk_embed_affine_layer_weight", 80, 192),
                    MNNNumpy.Floats(zip, "spk_embed_affine_layer_bias", 80));
            using (var zip = ZipFile.OpenRead(Path.Combine(common, "hift_source.npz")))
            {
                _sourceWeight = MNNNumpy.Floats(zip, "weight", 1, 9);
                _sourceBias = MNNNumpy.Floats(zip, "bias", 1);
            }
            _decoder = new MNNCosyVoice300MDecoder(root, threads, ExperimentalQuantizedDecoder,
                MNNNumpy.Floats(Path.Combine(common, "projected_positions.npy"), 14, 16, 4095, 64), DecoderMode);
            _text = new MNNGenerationGraph(Path.Combine(root, "fp16/text_encoder.mnn"), threads,
                backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High);
            _flowEncoder = new MNNGenerationGraph(Path.Combine(root, "fp16/flow_encoder.mnn"), threads,
                preparedGraph: MetalGraphBytes(root, "flow_encoder", acousticBackend), backendType: acousticBackend, precisionMode: MNNPrecisionMode.High);
            _regulator = new MNNGenerationGraph(Path.Combine(root, "fp16/flow_regulator.mnn"), threads,
                preparedGraph: MetalGraphBytes(root, "flow_regulator", acousticBackend), backendType: acousticBackend, precisionMode: MNNPrecisionMode.High);
            // Prepared bytes keep this stage out of the shared persistent GPU tuning
            // cache. External weight offsets are resolved by MNNGenerationGraph.
            _flow = new MNNGenerationGraph(_flowPath, threads, preparedGraph: File.ReadAllBytes(_flowPath),
                backendType: acousticBackend, precisionMode: MNNPrecisionMode.High);
            _f0 = new MNNGenerationGraph(Path.Combine(root, "fp16/hift_f0.mnn"), threads,
                preparedGraph: MetalGraphBytes(root, "hift_f0", acousticBackend), backendType: acousticBackend, precisionMode: MNNPrecisionMode.High);
            _hift = new MNNGenerationGraph(Path.Combine(root, "fp16/hift_core.mnn"), threads,
                preparedGraph: MetalGraphBytes(root, "hift_core", acousticBackend), backendType: acousticBackend, precisionMode: MNNPrecisionMode.High);
        }

        private void RetryAcousticsOnCpu()
        {
            // Preserve AR tokens and reference assets. Replace only acoustic sessions;
            // the caller restores the managed RNG before rendering again.
            ClearMetalShapeCaches();
            _hift.Dispose(); _f0.Dispose(); _flow.Dispose(); _regulator.Dispose(); _flowEncoder.Dispose();
            MNNGenerationGraph Cpu(string name) => new MNNGenerationGraph(Path.Combine(_modelRoot, "fp16", name + ".mnn"),
                _threads, backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High);
            _flowEncoder = Cpu("flow_encoder"); _regulator = Cpu("flow_regulator");
            _flow = Cpu("flow_estimator"); _f0 = Cpu("hift_f0"); _hift = Cpu("hift_core");
            _flowLength = _vocoderLength = 0;
        }

        private static byte[] MetalGraphBytes(string root, string stage, MNNBackendType backend)
            => MNNAcceleration.Resolve(backend) == MNNBackendType.Metal ? File.ReadAllBytes(Path.Combine(root, "fp16", stage + ".mnn")) : null;

        private void RecreateMetalStage(ref MNNGenerationGraph stage, string name)
        {
            if (stage.Backend != MNNBackendType.Metal) return;
            string path = Path.Combine(_modelRoot, "fp16", name + ".mnn");
            var replacement = new MNNGenerationGraph(path, _threads, preparedGraph: File.ReadAllBytes(path),
                backendType: MNNBackendType.Metal, precisionMode: MNNPrecisionMode.High);
            stage.Dispose(); stage = replacement;
        }

        private void SelectMetalShape(ref MNNGenerationGraph stage, Dictionary<int, MNNGenerationGraph> shapes, int length, string name)
        {
            if (stage.Backend != MNNBackendType.Metal) return;
            if (shapes.TryGetValue(length, out var cached)) { stage = cached; return; }
            if (shapes.Count != 0)
            {
                string path = Path.Combine(_modelRoot, "fp16", name + ".mnn");
                var replacement = new MNNGenerationGraph(path, _threads, preparedGraph: File.ReadAllBytes(path),
                    backendType: MNNBackendType.Metal, precisionMode: MNNPrecisionMode.High);
                // Bound resident weights/sessions. An evicted length is rebuilt
                // next time; a cached session never sees a second input shape.
                if (shapes.Count == _metalShapeCacheCapacity)
                {
                    int oldest = shapes.Keys.First(); shapes[oldest].Dispose(); shapes.Remove(oldest);
                }
                stage = replacement;
            }
            shapes.Add(length, stage);
        }

        private void ClearMetalShapeCaches()
        {
            foreach (var stage in _metalEncoders.Values) stage.Dispose();
            foreach (var stage in _metalVocoderCores.Values) stage.Dispose();
            _metalEncoders.Clear(); _metalVocoderCores.Clear();
        }

        private static float[] Project(float[] speaker, float[] weight, float[] bias)
        {
            var result = new float[bias.Length];
            for (int i = 0; i < result.Length; ++i)
            {
                double sum = bias[i];
                for (int j = 0; j < speaker.Length; ++j) sum += weight[i * speaker.Length + j] * speaker[j];
                result[i] = (float)sum;
            }
            return result;
        }
    }
}
