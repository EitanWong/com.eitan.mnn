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
    /// <summary>Managed Supertonic pipeline over official MNN Interpreter/Tensor APIs.
    /// Supports Supertonic 1/2/3 exports within the package platform ABI contract.</summary>
    public sealed partial class MNNSupertonic : IDisposable
    {
        private readonly object _gate = new object ();
        private readonly MNNGenerationGraph _duration, _text, _estimator, _vocoder;
        private readonly int[] _indexer;
        private readonly Dictionary<string, Voice> _voices = new Dictionary<string, Voice>();
        private readonly int _sampleRate, _chunk, _latentDim;
        private bool _disposed;
        private readonly MNNLocalSpeechFallback _languageFallback;
        private sealed class Voice
        {
            internal float[] Ttl, Dp;
            internal int[] TtlShape, DpShape;
        }

        public string[] Voices
        {
            get
            {
                lock (_gate)
                {
                    Check();
                    return _voices.Keys.OrderBy(value => value).ToArray();
                }
            }
        }

        public int SampleRate => _sampleRate;
        public string Version { get; }

        public string[] Languages => LanguagesForVersion(Version);
        /// <summary>Current stage backends; native unsupported operations may still run on CPU.</summary>
        public System.Collections.Generic.IReadOnlyDictionary<string, MNNBackendType> StageBackends
        {
            get
            {
                lock (_gate)
                {
                    if (_disposed)
                        throw new ObjectDisposedException(nameof(MNNSupertonic));
                    return new System.Collections.Generic.Dictionary<string, MNNBackendType>{{"Duration", _duration.Backend}, {"TextEncoder", _text.Backend}, {"Estimator", _estimator.Backend}, {"Vocoder", _vocoder.Backend}, };
                }
            }
        }

        private void Check()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MNNSupertonic));
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _languageFallback?.Dispose();
                _duration?.Dispose();
                _text?.Dispose();
                _estimator?.Dispose();
                _vocoder?.Dispose();
            }
        }
    }
}
