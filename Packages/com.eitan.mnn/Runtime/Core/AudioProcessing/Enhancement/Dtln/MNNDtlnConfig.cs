using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNDtlnConfig
    {
        public const string FileName = "mnn_dtln.json";
        public string repository, stage1_fp32, stage2_fp32, stage1_fp16, stage2_fp16, offline_alignment;
        public int sample_rate, frame_length, hop_length, units, output_delay_samples;
        public static MNNDtlnConfig Read(string directory) =>
            JsonUtility.FromJson<MNNDtlnConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid DTLN enhancement profile.");
    }
}
