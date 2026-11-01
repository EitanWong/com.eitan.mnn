using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Pinned Whisper encoder/decoder profile; timestamps are disabled.</summary>
    [Serializable]
    public sealed class MNNWhisperConfig
    {
        public const string FileName = "mnn_whisper.json";
        public string profile = "developerabu-tiny-en";
        public string encoder = "encode.mnn";
        public string decoder = "decode.mnn";
        public string tokens = "tiny.en-tokens.txt";
        public string language = "en"; // en; the small profile additionally supports zh and ta.

        public static MNNWhisperConfig Read(string directory) =>
            JsonUtility.FromJson<MNNWhisperConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid Whisper profile.");
    }
}
