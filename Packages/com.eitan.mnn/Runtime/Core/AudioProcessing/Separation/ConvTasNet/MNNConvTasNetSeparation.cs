using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Original two-speaker Libri2Mix ConvTasNet, with independent 8 kHz recording inference.</summary>
    public sealed class MNNConvTasNetSeparation : IDisposable
    {
        public const int SampleRate = 8000;
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private bool _disposed;
        public MNNBackendType Backend => _graph.Backend;

        private MNNConvTasNetSeparation(string directory,int threads,MNNBackendType backend)
        {
            if (!MNNConvTasNetModel.IsComplete(directory)) throw new InvalidDataException("ConvTasNet profile or graph verification failed.");
            if (backend == MNNBackendType.Auto)
                backend = MNNAcceleration.IsBackendAvailable(MNNBackendType.Metal) ? MNNBackendType.Metal : MNNBackendType.CPU;
            _graph = new MNNGenerationGraph(Path.Combine(directory,MNNConvTasNetModel.Graph),threads,
                backendType:backend,precisionMode:MNNPrecisionMode.High,gpuTuningMode:MNNGpuTuningMode.None,
                recreateGpuSessionOnShapeChange:true);
        }

        public static MNNConvTasNetSeparation Load(string directory,int threadCount=4,MNNBackendType backendType=MNNBackendType.Auto)
        {
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            MNNAcceleration.Validate(backendType);
            if (backendType == MNNBackendType.NN) throw new NotSupportedException("ConvTasNet NNAPI graph/device placement has not been verified.");
            MNNPlatformSupport.RequireSupported();
            return new MNNConvTasNetSeparation(Path.GetFullPath(directory),threadCount,backendType);
        }

        /// <summary>Separate 16–80000 samples at 8 kHz, or equivalent recordings at 8–96 kHz.
        /// Output amplitude/order follows the original forward_wav; speaker slots have no persistent identity.</summary>
        public MNNSeparatedAudio Separate(float[] samples,int sampleRate=SampleRate,CancellationToken cancellationToken=default)
        {
            Validate(samples,sampleRate,cancellationToken);
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNConvTasNetSeparation));
                var pcm = MNNSpeechDsp.Resample(samples,sampleRate,SampleRate,cancellationToken);
                if (pcm.Length < 16) throw new ArgumentException("Supply at least sixteen resampled samples.",nameof(samples));
                var output = _graph.RunOutputs(new[]{"separated"},new MNNGenerationGraph.Input("mixture",pcm,1,pcm.Length))[0];
                cancellationToken.ThrowIfCancellationRequested();
                if (output.Shape.Length != 3 || output.Shape[0] != 1 || output.Shape[1] != pcm.Length || output.Shape[2] != 2)
                    throw new InvalidDataException("Unexpected ConvTasNet separated waveform shape.");
                var values = output.Values; var first = new float[pcm.Length]; var second = new float[pcm.Length];
                for (int i=0;i<pcm.Length;++i) { first[i]=values[i*2]; second[i]=values[i*2+1]; }
                if (sampleRate != SampleRate)
                {
                    first = MNNSpeechDsp.Resample(first,SampleRate,sampleRate,cancellationToken);
                    second = MNNSpeechDsp.Resample(second,SampleRate,sampleRate,cancellationToken);
                    if (first.Length != samples.Length) Array.Resize(ref first,samples.Length);
                    if (second.Length != samples.Length) Array.Resize(ref second,samples.Length);
                }
                return new MNNSeparatedAudio(first,second,sampleRate);
            }
        }

        public MNNSeparatedAudio SeparateWaveFile(string path,CancellationToken cancellationToken=default)
        {
            var samples = MNNWaveAudio.Read(path,out int sampleRate);
            return Separate(samples,sampleRate,cancellationToken);
        }

        private static void Validate(float[] samples,int sampleRate,CancellationToken token)
        {
            if (samples == null || samples.Length == 0) throw new ArgumentException("Supply nonempty mono PCM.",nameof(samples));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (samples.Length > (long)sampleRate*10) throw new ArgumentException("Supply at most ten seconds of mono PCM.",nameof(samples));
            for (int i=0;i<samples.Length;++i)
            {
                if ((i&16383)==0) token.ThrowIfCancellationRequested();
                if (float.IsNaN(samples[i]) || float.IsInfinity(samples[i]) || Math.Abs(samples[i])>1)
                    throw new ArgumentException("Supply finite PCM in [-1,1].",nameof(samples));
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true; _graph.Dispose();
            }
        }
    }
}
