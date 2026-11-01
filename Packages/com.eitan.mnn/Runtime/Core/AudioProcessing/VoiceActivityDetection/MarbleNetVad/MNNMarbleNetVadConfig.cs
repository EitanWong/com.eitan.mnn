using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNMarbleNetVadConfig
    {
        public const string FileName = "mnn_marblenet_vad.json";
        public string repository = MNNMarbleNetVadModel.Repository;
        public string graph = MNNMarbleNetVadModel.Graph;
        public int sample_rate = 16000;
        public int feature_bins = 80;
        public int frame_shift_samples = 160;
        public int output_frame_samples = 320;
        public string input = "audio_signal";
        public string output = "outputs";
        public int speech_class = 1;

        public static MNNMarbleNetVadConfig Read(string directory) =>
            JsonUtility.FromJson<MNNMarbleNetVadConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid MarbleNet VAD profile.");
    }
}
