using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Owned convenience adapter for the existing SCRFD/MobileFaceNet task APIs.
    /// Other model families implement IMNNPresenceSource; the camera/controller never inspect model names.</summary>
    public sealed class MNNFacePresenceSource : IMNNPresenceSource, IDisposable
    {
        private readonly MNNImageTask _detector, _recognizer;
        private readonly MNNFacePresencePipeline _pipeline;

        private MNNFacePresenceSource(MNNImageTask detector, MNNImageTask recognizer, MNNFaceGallery gallery)
        {
            _detector = detector; _recognizer = recognizer;
            _pipeline = new MNNFacePresencePipeline(detector, recognizer, gallery);
        }

        public static MNNFacePresenceSource Load(string detectorDirectory, string recognizerDirectory = null,
            MNNFaceGallery gallery = null, int threadCount = 4, MNNBackendType backend = MNNBackendType.Auto)
        {
            var detector = MNNImageTask.Load(detectorDirectory, threadCount, backend);
            MNNImageTask recognizer = null;
            try
            {
                if (recognizerDirectory != null) recognizer = MNNImageTask.Load(recognizerDirectory, threadCount, backend);
                return new MNNFacePresenceSource(detector, recognizer, gallery);
            }
            catch { recognizer?.Dispose(); detector.Dispose(); throw; }
        }

        public MNNPresenceObservation Process(MNNRgbImage image, double capturedAt, CancellationToken cancellationToken = default) =>
            _pipeline.Process(image, capturedAt, cancellationToken);
        public void Reset() => _pipeline.Reset();
        public void Dispose() { _recognizer?.Dispose(); _detector.Dispose(); }
    }
}
