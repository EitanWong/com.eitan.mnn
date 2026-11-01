// Adapted from Nian27/CosyVoice3-MNN (Copyright 2026 Nian27), Apache-2.0.
// Modified for managed execution, validation and cancellation. See LICENSE.md.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Interop;
using MNN.Unity.Interop.Handles;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    public sealed partial class MNNCosyVoice
    {
        [Serializable]
        private sealed class SpeechOptions
        {
            public string backend_type, tmp_path;
            public int thread_num;
            public string sampler_type = "greedy", precision = "high";
            public bool reuse_kv;
        }

        public static MNNCosyVoice Load(string directory, int threads = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException(nameof(directory));
            if (threads < 1 || threads > 16)
                throw new ArgumentOutOfRangeException(nameof(threads));
            MNNAcceleration.Validate(backendType);
            if (!MNNCosyVoiceModel.IsComplete(directory))
                throw new InvalidDataException("CosyVoice complete release is missing or incomplete. Download it with Model Manager.");
            MNNPlatformSupport.RequireSupported();
            var model = new MNNCosyVoice();
            try
            {
                model.LoadCore(MNNCosyVoiceModel.ResolveDirectory(Path.GetFullPath(directory)), threads, backendType);
                return model;
            }
            catch
            {
                model.Dispose();
                throw;
            }
        }

        private void LoadCore(string directory, int threads, MNNBackendType backendType)
        {
            _prompt = File.ReadAllText(Path.Combine(directory, "prompt-speech-tokens.csv")).Split(new[]{',', ' ', '\r', '\n'}, StringSplitOptions.RemoveEmptyEntries).Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
            if (_prompt.Length != 87 || _prompt.Any(id => id < 0 || id >= MNNCosyVoiceSampler.Count))
                throw new InvalidDataException("Invalid CosyVoice reference tokens.");
            _cond = ReadFloats(directory, "prompt-cond.bin");
            _speaker = ReadFloats(directory, "spks.bin");
            _noise = ReadFloats(directory, "rand-noise.bin");
            _weight = ReadFloats(directory, "source-linear-weight.bin");
            _bias = ReadFloats(directory, "source-linear-bias.bin")[0];
            // The upstream sampler requires a patched MNN. Use official forward()
            // logits and managed RAS instead; do not load its unsupported sampler.
            var backend = MNNAcceleration.Resolve(backendType);
            string cache = Path.Combine(MNNInferenceCache.DirectoryFor("Cache"), "CosyVoice", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(cache);
            _llmOptions = new MNNModelOptions{ConfigPath = Path.Combine(directory, "config-cpu-cosyvoice-ras.json"), Json = UnityEngine.JsonUtility.ToJson(new SpeechOptions{backend_type = MNNAcceleration.ConfigName(backend), thread_num = threads, tmp_path = cache + Path.DirectorySeparatorChar}), Backend = backend, RequestedBackend = backendType, OwnedCache = cache};
            _llm = _llmOptions.Load(MNNInterop.MNN_Llm_create);
            _conditioner = new MNNGenerationGraph(Path.Combine(directory, "flow-conditioner.fp32.mnn"), threads, backendType: MNNBackendType.CPU);
            // The complete release's Flow returns non-finite values on Metal,
            // including High precision. Keep it on CPU instead of retrying every load.
            _flow = new MNNGenerationGraph(Path.Combine(directory, "flow.cfg-student-2step.batch1.fp16.mnn"), threads, backendType: MNNBackendType.CPU);
            _f0 = new MNNGenerationGraph(Path.Combine(directory, "hift-f0.fp32.mnn"), threads, backendType: MNNBackendType.CPU);
            // Metal HiFT produced nearly silent PCM in Normal and High precision.
            _hift = new MNNGenerationGraph(Path.Combine(directory, "hift-core.fp32.mnn"), threads, backendType: MNNBackendType.CPU);
        }

        private static float[] ReadFloats(string directory, string name)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(directory, name));
            var values = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            if (values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidDataException("Invalid CosyVoice asset: " + name);
            return values;
        }

        private static void RequireLength(float[] values, int length, string stage)
        {
            if (values.Length != length || values.Any(value => float.IsNaN(value) || float.IsInfinity(value)))
                throw new InvalidDataException("Unexpected CosyVoice " + stage + " shape.");
        }
    }
}
