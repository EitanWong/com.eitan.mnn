using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Worker-owned SCRFD -> stable target -> five-point alignment -> optional MobileFaceNet/gallery.
    /// Caller owns the supplied models. A missing/ambiguous face produces an absent observation.
    /// Capture and UI clocks must agree. Pass the result to MNNPresenceController or MNNPresenceComponent.</summary>
    public sealed class MNNFacePresencePipeline : IMNNPresenceSource
    {
        private readonly MNNImageTask _detector, _recognizer;
        private readonly MNNFaceGallery _gallery;
        private readonly MNNFaceTargetTracker _tracker;

        public MNNFacePresencePipeline(MNNImageTask detector, MNNImageTask recognizer = null,
            MNNFaceGallery gallery = null, MNNFaceTargetTracker tracker = null)
        {
            if (detector == null) throw new ArgumentNullException(nameof(detector));
            if (detector.Profile != MNNImageTaskProfile.Scrfd500M) throw new ArgumentException("Expected SCRFD face detection.", nameof(detector));
            if ((recognizer == null) != (gallery == null) || recognizer != null && recognizer.Profile != MNNImageTaskProfile.MobileFaceNet)
                throw new ArgumentException("Recognition requires MobileFaceNet and an enrolled gallery together.", nameof(recognizer));
            _detector = detector; _recognizer = recognizer; _gallery = gallery;
            _tracker = tracker ?? new MNNFaceTargetTracker();
        }

        public MNNPresenceObservation Process(MNNRgbImage image, double capturedAt, CancellationToken cancellationToken = default)
        {
            MNNPresenceOptions.RequireTime(capturedAt, nameof(capturedAt));
            var result = _detector.Process(image, cancellationToken);
            if (result.Detections == null) throw new InvalidDataException("Face detector returned no detection list.");
            int selected = _tracker.Select(result.Detections, capturedAt);
            if (selected < 0) return MNNPresenceObservation.Absent(capturedAt);
            string identity = null;
            if (_recognizer != null)
            {
                if (result.FaceKeypoints == null || result.FaceKeypoints.Length != result.Detections.Length)
                    throw new InvalidDataException("Recognition requires SCRFD five-point landmarks.");
                var crop = MNNImageTask.AlignFace(image, result.FaceKeypoints[selected]);
                identity = _gallery.Recognize(_recognizer.Process(crop, cancellationToken).Vector);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new MNNPresenceObservation(capturedAt, MNNPresenceKind.Face, _tracker.TrackId,
                result.Detections[selected].Score, identity);
        }

        public void Reset() => _tracker.Reset();
    }
}
