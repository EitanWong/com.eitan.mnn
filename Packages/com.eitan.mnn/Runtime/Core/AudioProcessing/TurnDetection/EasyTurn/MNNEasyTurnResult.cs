using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace MNN.Unity
{
    public sealed class MNNEasyTurnResult
    {
        public string Text { get; }
        public string Transcript { get; }
        public string Label { get; }
        public IReadOnlyList<int> Tokens { get; }
        public bool IsComplete => Label == "complete";
        internal MNNEasyTurnResult(string text, int[] tokens)
        {
            var match = Regex.Match(text.TrimEnd(), @"<(complete|incomplete|backchannel|wait)>$");
            if (!match.Success) throw new InvalidDataException("Easy-Turn did not return a terminal classification label.");
            Text = text; Label = match.Groups[1].Value; Transcript = text.Substring(0, match.Index).Trim();
            Tokens = Array.AsReadOnly((int[])tokens.Clone());
        }
    }
}
