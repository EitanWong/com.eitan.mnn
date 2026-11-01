using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    // The owner selects session lifetime. Release transient stages early when
    // prefill and step weight copies do not need to remain in memory together.
    internal sealed class MNNSpeechGraphs : IDisposable
    {
        private readonly string _root, _suffix;
        private readonly int _threads;
        private readonly CancellationToken _token;
        private readonly bool _cacheCodePredictor;
        private readonly Dictionary<string, MNNGenerationGraph> _graphs = new Dictionary<string, MNNGenerationGraph>();
        internal MNNSpeechGraphs(string root, int threads, bool index, CancellationToken token, bool cacheCodePredictor = false)
        { _root = root; _threads = threads; _suffix = index ? "_fp16.mnn" : ".mnn"; _token = token; _cacheCodePredictor = cacheCodePredictor; }
        internal MNNGenerationGraph.Output[] Run(string graph, string[] outputs, params MNNGenerationGraph.Input[] inputs)
        {
            _token.ThrowIfCancellationRequested();
            if (!_graphs.TryGetValue(graph, out var runner))
            {
                runner = new MNNGenerationGraph(Path.Combine(_root, graph + _suffix), _threads, backendType: MNNBackendType.CPU,
                    cpuSessionCacheCapacity: _cacheCodePredictor && graph == "cp_step" ? 15 : 1);
                _graphs.Add(graph, runner);
            }
            var result = runner.RunOutputs(outputs, inputs);
            _token.ThrowIfCancellationRequested();
            return result;
        }
        internal MNNGenerationGraph.Output Run(string graph, string output, params MNNGenerationGraph.Input[] inputs) => Run(graph, new[]{output}, inputs)[0];
        internal void Release(string graph)
        {
            if (_graphs.TryGetValue(graph, out var runner)) { runner.Dispose(); _graphs.Remove(graph); }
        }
        public void Dispose()
        {
            foreach (var graph in _graphs.Values) graph.Dispose();
            _graphs.Clear();
        }
    }
}
