using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>A keyword match, with acoustic confidence and times relative to the stream's start.</summary>
    public sealed class MNNKeywordDetection
    {
        public string Keyword { get; }
        public float Confidence { get; }
        public float StartSeconds { get; }
        public float EndSeconds { get; }
        public IReadOnlyList<int> TokenIds { get; }

        internal MNNKeywordDetection(string keyword, float confidence, float start, float end, int[] ids)
        {
            Keyword = keyword;
            Confidence = confidence;
            StartSeconds = start;
            EndSeconds = end;
            TokenIds = Array.AsReadOnly((int[])ids.Clone());
        }
    }
}
