using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Explicit PP-OCR CTC line recognizer protocol; not a page detector.</summary>
    [Serializable]
    public sealed class MNNPaddleTextConfig
    {
        public const string FileName = "mnn_paddle_text.json";
        public string graph = "en_PP-OCRv5_mobile_rec_infer.mnn";
        public string dictionary = "ppocr_keys_en.txt";
        public string input = "x", output = "fetch_name_0";
        public int height = 48, minimumWidth = 320, maximumWidth = 2048;
        public bool appendSpace = true;
        public bool reverseArabic = false;
        public MNNBackendType preferredBackend = MNNBackendType.Auto;
        // A sibling configuration in the page root; graph/dictionary remain confined to this profile.
        public string pageConfig = MNNPaddleOcrConfig.FileName;

        public static MNNPaddleTextConfig Read(string directory) =>
            JsonUtility.FromJson<MNNPaddleTextConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid Paddle text profile.");

        internal MNNPaddleTextConfig Clone() => JsonUtility.FromJson<MNNPaddleTextConfig>(JsonUtility.ToJson(this));

        internal void Validate(string directory)
        {
            if ((height != 32 && height != 48) || minimumWidth < 8 || maximumWidth < minimumWidth || maximumWidth > 4096 ||
                minimumWidth % 8 != 0 || maximumWidth % 8 != 0 || string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output) ||
                !Enum.IsDefined(typeof(MNNBackendType), preferredBackend) || string.IsNullOrWhiteSpace(pageConfig) ||
                pageConfig != Path.GetFileName(pageConfig) || pageConfig.IndexOfAny(new[] {'/', '\\'}) >= 0)
                throw new InvalidDataException("Expected Paddle BGR NCHW input, height 32/48 and widths divisible by 8 in [8,4096].");
            ResolveFile(directory, graph);
            ResolveFile(directory, dictionary);
        }

        internal static string ResolveFile(string directory, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
                throw new InvalidDataException("Paddle profile assets must use relative paths.");
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
                throw new FileNotFoundException("Missing Paddle profile asset within the model directory: " + relative);
            return path;
        }
    }
}
