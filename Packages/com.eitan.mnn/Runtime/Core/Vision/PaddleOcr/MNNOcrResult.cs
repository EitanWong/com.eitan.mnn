using System;
using System.Collections.Generic;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>Accepted text lines in geometric reading order, with source-image boxes and confidence.</summary>
    public sealed class MNNOcrResult
    {
        public IReadOnlyList<MNNTextRegion> Regions { get; }
        public string Text { get; }
        public float Confidence { get; }
        internal MNNOcrResult(IEnumerable<MNNTextRegion> regions)
        {
            var lines = regions.ToArray();
            Regions = Array.AsReadOnly(lines);
            Text = string.Join("\n", lines.Select(line => line.Text));
            Confidence = lines.Length == 0 ? 0 : lines.Average(line => line.Confidence);
        }
    }
}
