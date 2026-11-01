using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNAvMossFormer2TseModel
    {
        internal const string Repository = "alibabasglab/AV_MossFormer2_TSE_16K";
        internal const string Graph = "av_mossformer2_tse_16k_3s.mnn";
        internal const string ConfigFileName = "mnn_av_mossformer2_tse.json";
        internal const int SampleRate = 16000, FramesPerSecond = 25, ChunkSamples = 48000, ChunkFrames = 75, FaceSize = 112;
        internal const int DecoderFrameLength = 16, DecoderFrameStep = 8, DecoderFrameCount = 5999;

        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = JsonUtility.FromJson<MNNAvMossFormer2TseConfig>(File.ReadAllText(Path.Combine(directory, ConfigFileName)));
                return config != null && string.Equals(config.repository, Repository, StringComparison.OrdinalIgnoreCase) &&
                    config.sample_rate == SampleRate && config.video_fps == FramesPerSecond && config.chunk_samples == ChunkSamples &&
                    config.chunk_frames == ChunkFrames && config.face_size == FaceSize &&
                    string.Equals(config.input_audio, "mixture", StringComparison.Ordinal) &&
                    string.Equals(config.input_visual, "visual", StringComparison.Ordinal) &&
                    string.Equals(config.output, "target_frames", StringComparison.Ordinal) &&
                    string.Equals(config.output_mode, "decoder_frames", StringComparison.Ordinal) &&
                    config.decoder_frame_count == DecoderFrameCount && config.decoder_frame_length == DecoderFrameLength &&
                    config.decoder_frame_step == DecoderFrameStep &&
                    VerifyGraph(directory, config.graph, config.graph_bytes, config.graph_sha256) &&
                    VerifyGraph(directory, config.graph_fp32, config.graph_fp32_bytes, config.graph_fp32_sha256);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static void Verify(string directory)
        {
            if (!IsComplete(directory))
                throw new InvalidDataException("AV MossFormer2 target-speaker graph failed verification.");
        }

        private static bool VerifyGraph(string directory, string graph, long bytes, string hash)
        {
            if (string.IsNullOrWhiteSpace(graph)) return false;
            string path = Path.Combine(directory, graph);
            try
            {
                using (var stream = File.OpenRead(path))
                using (var sha = SHA256.Create())
                {
                    return stream.Length == bytes && string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), hash, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
