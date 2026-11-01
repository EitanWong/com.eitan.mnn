using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNSpeakerModelConfig
    {
        public const string FileName = "mnn_speaker.json";
        public string repository = MNNSpeakerModel.Repository;
        public string graph = MNNSpeakerModel.Graph;
        public string graph_fp16 = MNNSpeakerModel.GraphFp16;
        public string source_revision, frontend;
        public float[] analysis_window, mel_centers, mel_bands, mel_filters;
    }

}
