using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNFsmnVadConfig
    {
        public const string FileName = "mnn_fsmn_vad.json";
        public string repository = "iic/speech_fsmn_vad_zh-cn-16k-common-pytorch";
        public int sample_rate = 16000;
        public int feature_bins = 80;
        public int lfr_m = 5;
        public int lfr_n = 1;
        public string input = "speech";
        public string[] outputs = {"logits", "out_cache0", "out_cache1", "out_cache2", "out_cache3"};
        public string[] cache_inputs = {"in_cache0", "in_cache1", "in_cache2", "in_cache3"};
        public int[] cache_shape = {1, 128, 19, 1};
        public string graph = "fsmn_vad_fp32.mnn";
        public double[] cmvn_shift;
        public double[] cmvn_scale;

        public static MNNFsmnVadConfig Read(string directory) =>
            JsonUtility.FromJson<MNNFsmnVadConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid FSMN VAD profile.");
    }
}
