using System;
using System.Collections.Generic;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>A detected text quadrilateral in source pixels: top-left, top-right, bottom-right, bottom-left.</summary>
    public sealed class MNNTextBox
    {
        internal Vector2[] Points { get; }
        public IReadOnlyList<Vector2> Corners { get; }
        public float Confidence { get; }
        internal MNNTextBox(Vector2[] corners, float confidence)
        {
            Points = (Vector2[])corners.Clone();
            Corners = Array.AsReadOnly(Points);
            Confidence = confidence;
        }
    }
}
