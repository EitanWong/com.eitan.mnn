using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    internal sealed class MNNResembleEnhancerGraphs : IDisposable
    {
        private readonly MNNBackendType _backend;
        internal MNNGenerationGraph Denoiser { get; private set; }
        internal MNNGenerationGraph Encoder { get; private set; }
        internal MNNGenerationGraph Velocity { get; private set; }
        internal MNNGenerationGraph Decoder { get; private set; }
        internal MNNGenerationGraph Vocoder { get; private set; }

        internal MNNResembleEnhancerGraphs(string directory, MNNResembleEnhancerConfig config, MNNBackendType backend)
        {
            _backend = backend;
            try
            {
                string weights = Path.Combine(directory, config.external_weights);
                Denoiser = new MNNGenerationGraph(Path.Combine(directory, config.denoiser_graph), 4,
                    backendType: backend, precisionMode: MNNPrecisionMode.High,
                    cpuSessionCacheCapacity: backend == MNNBackendType.CPU ? config.cpu_denoiser_shape_cache_capacity : 1, externalWeightsPath: weights, gpuTuningMode: MNNGpuTuningMode.None,
                    recreateGpuSessionOnShapeChange: true, cpuWinogradMemoryLevel: backend == MNNBackendType.CPU ? 0 : (int?)null);
                bool metal = backend == MNNBackendType.Metal;
                Encoder = Graph(directory, metal ? config.encoder_metal_graph : config.encoder_cpu_graph, backend, weights);
                Velocity = Graph(directory, config.velocity_graph, backend, weights);
                Decoder = Graph(directory, metal ? config.decoder_metal_graph : config.decoder_cpu_graph, backend, weights);
                Vocoder = Graph(directory, config.vocoder_graph, backend, weights);
                RequireQualifiedBackends();
            }
            catch { Dispose(); throw; }
        }

        private static MNNGenerationGraph Graph(string directory, string name, MNNBackendType backend, string weights) =>
            new MNNGenerationGraph(Path.Combine(directory, name), 4, backendType: backend,
                precisionMode: MNNPrecisionMode.High, externalWeightsPath: weights, gpuTuningMode: MNNGpuTuningMode.None,
                recreateGpuSessionOnShapeChange: true);

        internal static float[] Run(MNNGenerationGraph graph, string output, int[] shape, params MNNGenerationGraph.Input[] inputs)
        {
            var result = graph.RunOutputs(new[] { output }, inputs)[0];
            if (!result.Shape.SequenceEqual(shape))
                throw new InvalidDataException("Unexpected Resemble Enhance " + output + " shape.");
            return result.Values;
        }

        internal void RequireQualifiedBackends()
        {
            if (Denoiser.Backend != _backend || Encoder.Backend != _backend ||
                Velocity.Backend != _backend || Decoder.Backend != _backend || Vocoder.Backend != _backend)
                throw new NotSupportedException("A stage left the qualified Resemble Enhance backend combination; reload with CPU.");
        }

        public void Dispose()
        {
            Vocoder?.Dispose();
            Decoder?.Dispose();
            Velocity?.Dispose();
            Encoder?.Dispose();
            Denoiser?.Dispose();
        }
    }
}
