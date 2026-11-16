using System;

namespace MNN.Unity
{
    /// <summary>Single-owner latest image for explicit multimodal requests. No history, disk storage or upload.
    /// Get it again for each turn; do not reuse a previously returned image for another person.</summary>
    public sealed class MNNVisionContext
    {
        private readonly double _maximumAge;
        private readonly int _maximumPixels;
        private MNNRgbImage _image;
        private double _capturedAt;
        private string _track;

        public MNNVisionContext(double maximumAgeSeconds = .75, int maximumPixels = 1920 * 1080)
        {
            MNNPresenceOptions.RequireTime(maximumAgeSeconds, nameof(maximumAgeSeconds));
            if (maximumAgeSeconds <= 0 || maximumPixels < 1) throw new ArgumentOutOfRangeException(nameof(maximumAgeSeconds));
            _maximumAge = maximumAgeSeconds;
            _maximumPixels = maximumPixels;
        }

        public void Update(MNNRgbImage image, double capturedAt, string trackId)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            MNNPresenceOptions.RequireTime(capturedAt, nameof(capturedAt));
            if ((long)image.Width * image.Height > _maximumPixels) throw new ArgumentException("Context image exceeds its pixel budget.", nameof(image));
            if (string.IsNullOrWhiteSpace(trackId)) throw new ArgumentException("Supply a target track.", nameof(trackId));
            if (_image != null && capturedAt < _capturedAt) return;
            _image = image; _capturedAt = capturedAt; _track = trackId;
        }

        public bool TryGet(string trackId, double now, out MNNRgbImage image)
        {
            MNNPresenceOptions.RequireTime(now, nameof(now));
            image = null;
            if (_image == null || now < _capturedAt) return false;
            if (now - _capturedAt > _maximumAge) { Clear(); return false; }
            if (_track != trackId) return false;
            image = _image;
            return true;
        }

        public void Clear() { _image = null; _track = null; }
    }
}
