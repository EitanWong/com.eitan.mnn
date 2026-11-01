using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Shared image task execution on the existing official MNN C++ binding.</summary>
    public sealed partial class MNNImageTask : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNGenerationGraph _graph;
        private bool _disposed;
        private readonly string[] _labels;
        public MNNImageTaskProfile Profile { get; }
        public MNNBackendType Backend => _graph.Backend;
        private MNNImageTask(string directory, MNNImageTaskConfig config, int threads, MNNBackendType backend)
        {
            Profile = config.profile;
            if (Profile == MNNImageTaskProfile.DepthAnythingV2Small)
            {
                // Metal produced finite but incorrect depth against the original
                // ONNX graph. Other accelerators have not passed this profile.
                if (backend != MNNBackendType.Auto && backend != MNNBackendType.CPU)
                    throw new NotSupportedException("Depth Anything V2 Small supports CPU/High only; Auto selects CPU.");
                backend = MNNBackendType.CPU;
            }
            if (Profile == MNNImageTaskProfile.MobileNetV2)
            {
                _labels = File.ReadAllLines(Path.Combine(directory, "labels.txt"));
                if (_labels.Length != 1000) throw new InvalidDataException("MobileNetV2 requires its 1000 ImageNet labels in class order.");
            }
            // Candy's instance normalization amplifies FP16 error. Explicit High
            // precision passed independent ONNX pixel checks on Metal and CPU.
            _graph = new MNNGenerationGraph(config.Resolve(directory, true), threads, backendType: backend,
                precisionMode: Profile == MNNImageTaskProfile.CandyStyle || Profile == MNNImageTaskProfile.DeepLabV3 || Profile == MNNImageTaskProfile.MoveNetLightning || Profile == MNNImageTaskProfile.DepthAnythingV2Small ? MNNPrecisionMode.High : MNNPrecisionMode.Normal);
        }
        public static MNNImageTask Load(string directory, int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64) throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNImageTask(directory, MNNImageTaskConfig.Read(directory), threadCount, backendType);
        }

        /// <summary>For Landmark106, pass a face crop including the 1.5x bbox margin.
        /// MobileFaceNet requires a five-point aligned 112x112 crop, never a full photo.</summary>
        public MNNImageTaskResult Process(MNNRgbImage image, CancellationToken cancellationToken = default)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNImageTask));
                cancellationToken.ThrowIfCancellationRequested();
                MNNImageTaskResult result;
                switch (Profile)
                {
                    case MNNImageTaskProfile.ModNet: result = Matte(image, cancellationToken); break;
                    case MNNImageTaskProfile.RealEsrganX4: result = Upscale(image, cancellationToken); break;
                    case MNNImageTaskProfile.UVDoc: result = Rectify(image, cancellationToken); break;
                    case MNNImageTaskProfile.Scrfd500M: result = DetectFaces(image, cancellationToken); break;
                    case MNNImageTaskProfile.MobileFaceNet: result = EmbedFace(image, cancellationToken); break;
                    case MNNImageTaskProfile.Landmark106: result = Landmarks(image, cancellationToken); break;
                    case MNNImageTaskProfile.MobileNetV2: result = Classify(image, cancellationToken); break;
                    case MNNImageTaskProfile.PglNet: result = Dehaze(image, cancellationToken); break;
                    case MNNImageTaskProfile.CandyStyle: result = Stylize(image, cancellationToken); break;
                    case MNNImageTaskProfile.DeepLabV3: result = Segment(image, cancellationToken); break;
                    case MNNImageTaskProfile.MoveNetLightning: result = EstimatePose(image, cancellationToken); break;
                    case MNNImageTaskProfile.DepthAnythingV2Small: result = EstimateDepth(image, cancellationToken); break;
                    default: throw new InvalidDataException("Image profile has no implemented decoder.");
                }
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
        }
        public Task<MNNImageTaskResult> ProcessAsync(MNNRgbImage image, CancellationToken cancellationToken = default) =>
            Task.Run(() => Process(image, cancellationToken), cancellationToken);

        private MNNGenerationGraph.Output Run(MNNRgbImage image, string input, string output, int width, int height,
            float mean, float std, CancellationToken token, bool bgr = false, bool corners = false)
        {
            float[] values = MNNImageSampling.Tensor(image, width, height, mean, std, bgr, corners, token);
            token.ThrowIfCancellationRequested();
            return _graph.RunOutputs(new[] {output}, new MNNGenerationGraph.Input(input, values, 1, 3, height, width))[0];
        }
        internal static void RequireShape(MNNGenerationGraph.Output output, params int[] shape)
        {
            if (output.Shape.Length != shape.Length) throw new InvalidDataException("Unexpected image output rank.");
            for (int i = 0; i < shape.Length; ++i)
                if (output.Shape[i] != shape[i]) throw new InvalidDataException("Unexpected image output shape: " + string.Join(",", output.Shape));
        }
        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed) return;
                _disposed = true;
                _graph.Dispose();
            }
        }
    }
}
