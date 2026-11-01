using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNAvMossFormer2TseConfig
    {
        public const string FileName = "mnn_av_mossformer2_tse.json";
        public string repository = MNNAvMossFormer2TseModel.Repository;
        public int sample_rate = MNNAvMossFormer2TseModel.SampleRate;
        public int video_fps = MNNAvMossFormer2TseModel.FramesPerSecond;
        public int chunk_samples = MNNAvMossFormer2TseModel.ChunkSamples;
        public int chunk_frames = MNNAvMossFormer2TseModel.ChunkFrames;
        public int face_size = MNNAvMossFormer2TseModel.FaceSize;
        public string graph = MNNAvMossFormer2TseModel.Graph;
        public long graph_bytes;
        public string graph_sha256;
        public string graph_fp32 = "av_mossformer2_tse_16k_3s_fp32.mnn";
        public long graph_fp32_bytes;
        public string graph_fp32_sha256;
        public string input_audio = "mixture";
        public string input_visual = "visual";
        public string output = "target_frames";
        public string output_mode = "decoder_frames";
        public int decoder_frame_count = MNNAvMossFormer2TseModel.DecoderFrameCount;
        public int decoder_frame_length = MNNAvMossFormer2TseModel.DecoderFrameLength;
        public int decoder_frame_step = MNNAvMossFormer2TseModel.DecoderFrameStep;

        public static MNNAvMossFormer2TseConfig Read(string directory) =>
            JsonUtility.FromJson<MNNAvMossFormer2TseConfig>(File.ReadAllText(Path.Combine(directory, MNNAvMossFormer2TseModel.ConfigFileName)))
            ?? throw new InvalidDataException("Invalid AV MossFormer2 target-speaker profile.");
    }
}
