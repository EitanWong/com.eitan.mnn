using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>Owned detection geometry in the upright RGB image's top-left pixel coordinates.
    /// Carries display evidence only; boxes never grant presence or identity admission.</summary>
    public sealed class MNNPresenceDetectionFrame
    {
        public int Width { get; }
        public int Height { get; }
        public IReadOnlyList<MNNDetection> Detections { get; }
        public int SelectedIndex { get; }

        public MNNPresenceDetectionFrame(int width, int height, IReadOnlyList<MNNDetection> detections, int selectedIndex = -1)
        {
            if (width < 1 || height < 1 || (long)width * height > 64000000)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (detections == null) throw new ArgumentNullException(nameof(detections));
            if (selectedIndex < -1 || selectedIndex >= detections.Count)
                throw new ArgumentOutOfRangeException(nameof(selectedIndex));
            var owned = new MNNDetection[detections.Count];
            for (int i = 0; i < owned.Length; ++i)
            {
                var detection = detections[i];
                if (detection == null || !Finite(detection.X) || !Finite(detection.Y) ||
                    !Finite(detection.Width) || !Finite(detection.Height) || detection.Width <= 0 || detection.Height <= 0 ||
                    !Finite(detection.Score) || detection.Score < 0 || detection.Score > 1)
                    throw new ArgumentException("Expected finite detection geometry and confidence.", nameof(detections));
                owned[i] = detection;
            }
            Width = width;
            Height = height;
            Detections = Array.AsReadOnly(owned);
            SelectedIndex = selectedIndex;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
