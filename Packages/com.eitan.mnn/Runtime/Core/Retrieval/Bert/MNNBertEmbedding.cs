using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>WordPiece BERT/MiniLM embedding graphs through existing official Interpreter/Tensor APIs.</summary>
    public sealed class MNNBertEmbedding : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNBertEmbeddingConfig _config;
        private readonly MNNWordPieceTokenizer _tokenizer;
        private readonly MNNInterpreter _interpreter;
        private MNNSession _session;
        private readonly int _threads;
        private bool _disposed;
        public int Dimension => _config.dimension;
        public MNNBackendType Backend => _session.ActualBackend;

        private MNNBertEmbedding(string directory, MNNBertEmbeddingConfig config, int threads, MNNPrecisionMode precision, MNNBackendType backend)
        {
            _config = config.Clone();
            _threads = threads;
            if (_config.dimension < 1 || _config.dimension > 65536 || _config.maximumTokens < 2 || _config.maximumTokens > 65536 ||
                string.IsNullOrWhiteSpace(_config.inputIds) || string.IsNullOrWhiteSpace(_config.output) || !Enum.IsDefined(typeof(MNNBertPooling), _config.pooling))
                throw new InvalidDataException("Invalid BERT embedding profile dimensions, pooling or tensor names.");
            _tokenizer = new MNNWordPieceTokenizer(directory, _config);
            _interpreter = MNNInterpreter.CreateFromFile(MNNBertEmbeddingConfig.ResolveFile(directory, _config.graph));
            try
            {
                _session = _interpreter.CreateSession(new MNNSessionConfig {BackendType = backend, ThreadCount = threads, PrecisionMode = precision});
            }
            catch
            {
                _interpreter.Dispose();
                throw;
            }
        }

        public static MNNBertEmbedding Load(string directory, MNNBertEmbeddingConfig config = null, int threadCount = 4,
            MNNPrecisionMode precisionMode = MNNPrecisionMode.Normal, MNNBackendType backendType = MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException(nameof(directory));
            if (threadCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(threadCount));
            if (precisionMode != MNNPrecisionMode.Normal && precisionMode != MNNPrecisionMode.High && precisionMode != MNNPrecisionMode.Low)
                throw new ArgumentOutOfRangeException(nameof(precisionMode));
            MNNPlatformSupport.RequireSupported();
            return new MNNBertEmbedding(Path.GetFullPath(directory), config ?? MNNBertEmbeddingConfig.Read(directory), threadCount, precisionMode, backendType);
        }

        /// <summary>Encodes a document or raw text; truncation reserves CLS/SEP and returns an owned vector.</summary>
        public float[] Encode(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNBertEmbedding));
                cancellationToken.ThrowIfCancellationRequested();
                int[] ids = _tokenizer.Encode(text, _config.maximumTokens);
                int length = ids.Length;
                var data = new Dictionary<string, int[]>(StringComparer.Ordinal) {[_config.inputIds] = ids};
                if (!string.IsNullOrEmpty(_config.attentionMask))
                    data.Add(_config.attentionMask, Enumerable.Repeat(1, length).ToArray());
                if (!string.IsNullOrEmpty(_config.tokenTypes))
                    data.Add(_config.tokenTypes, new int[length]);
                return RunWithFallback(data, length, cancellationToken);
            }
        }

        private float[] RunWithFallback(Dictionary<string, int[]> data, int length, CancellationToken cancellationToken)
        {
            bool outputReady = false;
            try
            {
                return Run(data, length, cancellationToken, out outputReady);
            }
            catch (Exception error) when (_session.ActualBackend != MNNBackendType.CPU &&
                (error is MNNException || (outputReady && error is InvalidDataException)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_session.ActualBackend == MNNBackendType.Metal && outputReady &&
                    error is InvalidDataException && _session.Config.PrecisionMode != MNNPrecisionMode.High)
                {
                    UnityEngine.Debug.Log("[MNN] BERT Metal output invalid; retrying with High (FP32) precision.");
                    outputReady = false;
                    try
                    {
                        ReplaceSession(MNNBackendType.Metal, MNNPrecisionMode.High);
                        return Run(data, length, cancellationToken, out outputReady);
                    }
                    catch (Exception retryError) when (retryError is MNNException || (outputReady && retryError is InvalidDataException))
                    {
                        UnityEngine.Debug.Log("[MNN] BERT Metal FP32 retry failed: " + retryError.Message);
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                UnityEngine.Debug.Log("[MNN] BERT accelerator failed; retrying on CPU. " + error.Message);
                ReplaceSession(MNNBackendType.CPU, MNNPrecisionMode.High);
                return Run(data, length, cancellationToken, out outputReady);
            }
        }

        private void ReplaceSession(MNNBackendType backend, MNNPrecisionMode precision)
        {
            // Keep the current session usable if creating its replacement fails.
            var replacement = _interpreter.CreateSession(new MNNSessionConfig
            {
                BackendType = backend, ThreadCount = _threads, PrecisionMode = precision
            });
            _session.Dispose();
            _session = replacement;
        }

        private float[] Run(Dictionary<string, int[]> data, int length, CancellationToken cancellationToken, out bool outputReady)
        {
            outputReady = false;
            cancellationToken.ThrowIfCancellationRequested();
            var shapes = data.Where(item => !_session.GetInput(item.Key).Shape.SequenceEqual(new[] {1, length}))
                .ToDictionary(item => item.Key, item => new[] {1, length});
            if (shapes.Count > 0)
                _session.ResizeInputs(shapes, true);
            foreach (var item in data)
            {
                var tensor = _session.GetInput(item.Key);
                if (tensor.DataType == MNNDataType.Int64)
                    tensor.CopyFromArray(item.Value.Select(value => (long)value).ToArray());
                else if (tensor.DataType == MNNDataType.Int32)
                    tensor.CopyFromArray(item.Value);
                else
                    throw new InvalidDataException("BERT token inputs must be int32 or int64: " + item.Key);
            }
            cancellationToken.ThrowIfCancellationRequested();
            _session.Run();
            cancellationToken.ThrowIfCancellationRequested();
            var output = _session.GetOutput(_config.output);
            int[] shape = output.Shape;
            if (!shape.SequenceEqual(new[] {1, length, Dimension}) && !shape.SequenceEqual(new[] {length, Dimension}))
                throw new InvalidDataException("BERT hidden states must have shape [1,tokens,dimension] or [tokens,dimension].");
            var hidden = output.CopyToArray<float>();
            outputReady = true;
            return Pool(hidden, length, Dimension, _config.pooling, _config.normalize);
        }

        public float[] EncodeQuery(string text, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ArgumentNullException(nameof(text));
            return Encode((_config.queryPrefix ?? "") + text, cancellationToken);
        }

        public Task<float[]> EncodeAsync(string text, CancellationToken cancellationToken = default) => Task.Run(() => Encode(text, cancellationToken), cancellationToken);

        internal static float[] Pool(float[] hidden, int tokens, int dimension, MNNBertPooling pooling, bool normalize)
        {
            if (tokens < 1 || dimension < 1 || hidden == null || hidden.Length != checked(tokens * dimension) || !Enum.IsDefined(typeof(MNNBertPooling), pooling))
                throw new InvalidDataException("Invalid BERT hidden state shape.");
            var sums = new double[dimension];
            for (int row = 0; row < tokens; ++row)
                for (int column = 0; column < dimension; ++column)
                {
                    float value = hidden[row * dimension + column];
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        throw new InvalidDataException("BERT embedding returned a non-finite hidden state.");
                    if (pooling == MNNBertPooling.Mean || row == 0)
                        sums[column] += value;
                }
            double norm = 0;
            for (int i = 0; i < sums.Length; ++i)
            {
                if (pooling == MNNBertPooling.Mean)
                    sums[i] /= tokens;
                norm += sums[i] * sums[i];
            }
            if (!(norm > 1e-24) || double.IsInfinity(norm))
                throw new InvalidDataException("BERT embedding has zero or invalid norm.");
            double scale = normalize ? 1 / Math.Sqrt(norm) : 1;
            return sums.Select(value => (float)(value * scale)).ToArray();
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _session.Dispose();
                _interpreter.Dispose();
            }
        }
    }
}
