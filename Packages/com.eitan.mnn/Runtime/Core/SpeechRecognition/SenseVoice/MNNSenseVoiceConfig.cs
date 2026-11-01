using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Verified SenseVoiceSmall graph profile. Language is auto, zh, en, ja, ko or yue.</summary>
    [Serializable]
    public sealed class MNNSenseVoiceConfig
    {
        public const string FileName = "mnn_sensevoice.json";
        public string profile = "modelscope-small";
        public string model = "model.mnn";
        public string tokens = "tokens.txt";
        public string language = "auto";
        public bool useInverseTextNormalization = true;

        public static MNNSenseVoiceConfig Read(string directory) =>
            JsonUtility.FromJson<MNNSenseVoiceConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid SenseVoice profile.");
    }
}
