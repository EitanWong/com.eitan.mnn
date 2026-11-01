using System;
using System.IO;
using System.Threading;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Immutable, synchronized frame-major SEG gesture features at 15 FPS.</summary>
    public sealed class MNNSegGestureFeatures
    {
        public const int FramesPerSecond = 15;
        public const int FeaturesPerFrame = 30;
        private readonly float[] _values;
        public int FrameCount => _values.Length / FeaturesPerFrame;

        public MNNSegGestureFeatures(float[] values, CancellationToken cancellationToken = default)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Length < FeaturesPerFrame || values.Length > 150 * FeaturesPerFrame || values.Length % FeaturesPerFrame != 0)
                throw new ArgumentException("Supply 1–150 frame-major gesture frames with 30 features each.", nameof(values));
            _values = (float[])values.Clone();
            for (int i = 0; i < _values.Length; ++i)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (float.IsNaN(_values[i]) || float.IsInfinity(_values[i]))
                    throw new ArgumentException("Supply finite gesture features.", nameof(values));
            }
        }

        public float[] ToArray() => (float[])_values.Clone();

        [Serializable]
        private sealed class Payload
        {
            public string format = "";
            public int fps = 0, feature_size = 0;
            public float[] values = null;
        }

        /// <summary>Read the frame-major JSON feature format described in the model README.</summary>
        public static MNNSegGestureFeatures ReadJson(string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var data = JsonUtility.FromJson<Payload>(File.ReadAllText(path));
            if (data == null || data.format != "SEG-YGD-gesture-v1" || data.fps != FramesPerSecond || data.feature_size != FeaturesPerFrame)
                throw new InvalidDataException("Expected SEG-YGD-gesture-v1 JSON with fps=15 and feature_size=30.");
            return new MNNSegGestureFeatures(data.values, cancellationToken);
        }
    }
}
