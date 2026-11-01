using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Full-image DB detection, rectified crops and matched language CTC recognition.</summary>
    public sealed class MNNPaddleOcr : IDisposable
    {
        private readonly object _gate = new object();
        private readonly MNNPaddleTextDetector _detector;
        private readonly MNNPaddleTextRecognizer _recognizer;
        private readonly float _threshold;
        private bool _disposed;
        public MNNBackendType DetectionBackend => _detector.Backend;
        public MNNBackendType RecognitionBackend => _recognizer.Backend;

        private MNNPaddleOcr(string directory, string profile, MNNPaddleOcrConfig config, int threads, MNNBackendType backend)
        {
            config = config.Clone();
            config.Validate(directory);
            string recognizerDirectory = MNNPaddleOcrConfig.RecognizerDirectory(directory, profile);
            _threshold = config.recognitionThreshold;
            _recognizer = MNNPaddleTextRecognizer.Load(recognizerDirectory, threadCount: threads,
                backendType: backend == MNNBackendType.Auto ? config.preferredRecognitionBackend : backend);
            try { _detector = MNNPaddleTextDetector.Load(directory, config, threads, backend); }
            catch { _recognizer.Dispose(); throw; }
        }

        public static MNNPaddleOcr Load(string directory, string recognizerProfile = ".", MNNPaddleOcrConfig config = null,
            int threadCount = 4, MNNBackendType backendType = MNNBackendType.Auto)
        {
            MNNPlatformSupport.RequireSupported();
            if (string.IsNullOrWhiteSpace(directory))
                throw new ArgumentNullException(nameof(directory));
            if (threadCount < 1 || threadCount > 64)
                throw new ArgumentOutOfRangeException(nameof(threadCount));
            return new MNNPaddleOcr(Path.GetFullPath(directory), recognizerProfile, config ?? MNNPaddleOcrConfig.ReadForProfile(directory, recognizerProfile), threadCount, backendType);
        }

        public MNNOcrResult Recognize(MNNRgbImage image, CancellationToken cancellationToken = default)
        {
            if (image == null)
                throw new ArgumentNullException(nameof(image));
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNPaddleOcr));
                var boxes = _detector.Detect(image, cancellationToken);
                var regions = new List<MNNTextRegion>();
                foreach (var box in boxes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var crop = MNNPaddleDbPostprocessor.Crop(image, box, cancellationToken);
                    var text = _recognizer.Recognize(crop, cancellationToken);
                    if (text.Text.Length > 0 && text.Confidence >= _threshold)
                        regions.Add(new MNNTextRegion(box, text));
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new MNNOcrResult(regions);
            }
        }

        public Task<MNNOcrResult> RecognizeAsync(MNNRgbImage image, CancellationToken cancellationToken = default) =>
            Task.Run(() => Recognize(image, cancellationToken), cancellationToken);

        /// <summary>Recognize an already cropped upright line without DB detection or another resampling crop.</summary>
        public MNNTextRecognition RecognizeLine(MNNRgbImage line, CancellationToken cancellationToken = default)
        {
            lock (_gate)
            {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(MNNPaddleOcr));
                return _recognizer.Recognize(line, cancellationToken);
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                if (_disposed)
                    return;
                _disposed = true;
                _detector.Dispose();
                _recognizer.Dispose();
            }
        }
    }
}
