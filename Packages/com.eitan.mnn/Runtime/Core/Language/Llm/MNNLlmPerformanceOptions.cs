using System;
using System.Globalization;
using System.Text;

namespace MNN.Unity
{
    /// <summary>LLM tuning for the bundled compiler ABI. Unassigned properties use
    /// device/model defaults when loaded; explicitly assigned properties take precedence.
    /// Resolve returns a snapshot without changing this object.</summary>
    public sealed class MNNLlmPerformanceOptions
    {
        private bool? _weightReordering;
        private int? _threadCount;
        private MNNPrecisionMode? _precisionMode;
        private MNNPowerMode? _powerMode;
        /// <summary>Unassigned: select at load. The unresolved getter returns the fallback value.</summary>
        public int ThreadCount { get => _threadCount ?? 4; set => _threadCount = value; }
        /// <summary>One official OpenCL kernel tuning level. Independent of CPU
        /// ThreadCount, including CPU fallback. MNN LLM supplies buffer mode internally.
        /// Heavy tuning can increase first-load cost; benchmark on the target device.</summary>
        public MNNGpuTuningMode OpenClTuningMode { get; set; } = MNNGpuTuningMode.Wide;
        public MNNBackendType BackendType { get; set; } = MNNBackendType.Auto;
        public MNNPrecisionMode PrecisionMode { get => _precisionMode ?? MNNPrecisionMode.Low; set => _precisionMode = value; }
        public MNNPowerMode PowerMode { get => _powerMode ?? MNNPowerMode.High; set => _powerMode = value; }

        /// <summary>Fill unassigned properties from measured device/model profiles or
        /// the platform fallback. Backend Auto still permits native availability/CPU fallback.
        /// Does not benchmark, load a model, modify model files, or change this object.</summary>
        public MNNLlmPerformanceOptions Resolve(string modelDirectory)
        {
            if (string.IsNullOrWhiteSpace(modelDirectory))
                throw new ArgumentNullException(nameof(modelDirectory));
            return Resolve(MNNLlmDeviceDefaults.Select(modelDirectory, BackendType));
        }

        internal MNNLlmPerformanceOptions ResolveForBackend(string directory, MNNBackendType backend)
        {
            var request = (MNNLlmPerformanceOptions)MemberwiseClone();
            request.BackendType = backend;
            return request.Resolve(directory);
        }

        internal MNNLlmPerformanceOptions Resolve(MNNLlmPerformanceOptions defaults)
        {
            var result = (MNNLlmPerformanceOptions)MemberwiseClone();
            result.ThreadCount = _threadCount ?? defaults.ThreadCount;
            result.PrecisionMode = _precisionMode ?? defaults.PrecisionMode;
            result.PowerMode = _powerMode ?? defaults.PowerMode;
            if (BackendType == MNNBackendType.Auto) result.BackendType = defaults.BackendType;
            result.Validate();
            return result;
        }

        /// <summary>Enable official dynamic_option bit 3 (WEIGHT_ONLINE_REORDER).
        /// Its benefit and memory cost depend on the selected CPU kernel and model.
        /// Unassigned inherits the model setting; an explicit false clears only this bit.</summary>
        public bool EnableWeightReordering
        {
            get => _weightReordering ?? false;
            set => _weightReordering = value;
        }

        /// <summary>Null preserves the model's dynamic quantization mode.</summary>
        public MNNLlmDynamicQuantization? DynamicQuantization { get; set; }
        /// <summary>Null preserves attention_mode (or legacy quant_qkv). Changes only
        /// the FlashAttention bit, retaining the model's KV quantization mode.
        /// Support depends on the native backend and model.</summary>
        public bool? FlashAttention { get; set; }
        /// <summary>Null inherits chunk; zero disables chunking. Positive values bound
        /// prefill chunks. Models with chunk_limits require their exported shape contract.</summary>
        public int? PrefillChunkSize { get; set; }

        internal string ToJson(string modelDirectory = null)
            => ToJsonFromSource(modelDirectory == null ? MNNModelData.Object(MNNModelData.Parse("{}")) : MNNModelOptions.ReadLlmConfiguration(modelDirectory));

        internal string ToJsonFromConfiguration(string configuration)
            => ToJsonFromSource(MNNModelData.Object(MNNModelData.Parse(configuration)));

        private string ToJsonFromSource(System.Collections.Generic.Dictionary<string, object> source)
        {
            Validate();
            var json = new StringBuilder("{\"power\":\"").Append(PowerMode.ToString().ToLowerInvariant()).Append('"');
            AppendOverrides(source, json);
            return json.Append('}').ToString();
        }

        private void Validate()
        {
            if (ThreadCount < 1 || ThreadCount > 64)
                throw new ArgumentOutOfRangeException(nameof(ThreadCount));
            if (!Enum.IsDefined(typeof(MNNGpuTuningMode), OpenClTuningMode))
                throw new ArgumentOutOfRangeException(nameof(OpenClTuningMode), "Select exactly one OpenCL tuning level.");
            MNNAcceleration.Validate(BackendType);
            if (PrecisionMode != MNNPrecisionMode.Normal && PrecisionMode != MNNPrecisionMode.High && PrecisionMode != MNNPrecisionMode.Low)
                throw new ArgumentOutOfRangeException(nameof(PrecisionMode));
            if (!Enum.IsDefined(typeof(MNNPowerMode), PowerMode))
                throw new ArgumentOutOfRangeException(nameof(PowerMode));
            if (DynamicQuantization.HasValue && !Enum.IsDefined(typeof(MNNLlmDynamicQuantization), DynamicQuantization.Value))
                throw new ArgumentOutOfRangeException(nameof(DynamicQuantization));
            if (PrefillChunkSize < 0) throw new ArgumentOutOfRangeException(nameof(PrefillChunkSize));
        }

        private void AppendOverrides(System.Collections.Generic.Dictionary<string, object> source, StringBuilder json)
        {
            if (_weightReordering.HasValue || DynamicQuantization.HasValue)
            {
                int mode = source.TryGetValue("dynamic_option", out var value) ? MNNModelData.Integer(value) : 0;
                if (DynamicQuantization.HasValue) mode = (mode & ~3) | (int)DynamicQuantization.Value;
                if (_weightReordering.HasValue) mode = _weightReordering.Value ? mode | 8 : mode & ~8;
                json.Append(",\"dynamic_option\":").Append(mode.ToString(CultureInfo.InvariantCulture));
            }
            if (FlashAttention.HasValue)
            {
                int mode = source.TryGetValue("attention_mode", out var value) ? MNNModelData.Integer(value) :
                    source.TryGetValue("quant_qkv", out value) ? MNNModelData.Integer(value) : 8;
                mode = FlashAttention.Value ? mode | 8 : mode & ~8;
                json.Append(",\"attention_mode\":").Append(mode.ToString(CultureInfo.InvariantCulture));
            }
            if (PrefillChunkSize.HasValue)
            {
                if (source.ContainsKey("chunk_limits"))
                    throw new ArgumentException("chunk_limits takes precedence over chunk; retain the model's exported shape configuration.", nameof(PrefillChunkSize));
                json.Append(",\"chunk\":").Append(PrefillChunkSize.Value.ToString(CultureInfo.InvariantCulture));
            }
        }
    }

    /// <summary>Official MNN dynamic_option low bits; weight reordering is independent.</summary>
    public enum MNNLlmDynamicQuantization
    {
        PerChannel = 0,
        PerTensor = 1,
        PerBlock = 2
    }
}
