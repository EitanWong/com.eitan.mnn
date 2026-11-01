using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Sherpa Zipformer v1 streaming-transducer graph locations.</summary>
    [Serializable]
    public sealed class MNNZipformerConfig
    {
        public string encoder = "encoder-epoch-99-avg-1.mnn";
        public string decoder = "decoder-epoch-99-avg-1.mnn";
        public string joiner = "joiner-epoch-99-avg-1.mnn";
        public string tokens = "tokens.txt";
        public const string FileName = "mnn_zipformer.json";

        public static MNNZipformerConfig Read(string directory) =>
            JsonUtility.FromJson<MNNZipformerConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid Zipformer profile.");

        internal MNNZipformerConfig Clone() => (MNNZipformerConfig)MemberwiseClone();

        internal static string Resolve(string directory, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
                throw new InvalidDataException("Zipformer assets must use paths within their model directory.");
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
                throw new FileNotFoundException("Missing Zipformer asset: " + relative);
            return path;
        }
    }
}
