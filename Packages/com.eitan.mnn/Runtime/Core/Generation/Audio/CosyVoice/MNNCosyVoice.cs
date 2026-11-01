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
    /// <summary>CosyVoice3 complete fp16 release, built-in reference voice, 24 kHz mono.
    /// Uses the bundled macOS Mono C++ ABI. Voice enrollment is not included.</summary>
    public sealed partial class MNNCosyVoice : IDisposable
    {
        private readonly object _gate = new object ();
        private LlmHandle _llm;
        private MNNModelOptions _llmOptions;
        private MNNGenerationGraph _conditioner, _flow, _f0, _hift;
        private int[] _prompt;
        private float[] _cond, _speaker, _noise, _weight;
        private float _bias;
        private bool _disposed;
        public double LastTokenSeconds { get; private set; }

        public double LastFlowSeconds { get; private set; }

        public double LastVocoderSeconds { get; private set; }

        public int SampleRate => 24000;
        /// <summary>Configured LLM backend. Conditioner, Flow, F0 and HiFT use CPU.
        /// Individual unsupported LLM operators may also run on CPU.</summary>
        public MNNBackendType Backend => _llmOptions.Backend;
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _hift?.Dispose();
                _f0?.Dispose();
                _flow?.Dispose();
                _conditioner?.Dispose();
                _llm?.Dispose();
                MNNModelOptions.RemoveOwnedCache(_llmOptions?.OwnedCache);
            }
        }
    }
}
