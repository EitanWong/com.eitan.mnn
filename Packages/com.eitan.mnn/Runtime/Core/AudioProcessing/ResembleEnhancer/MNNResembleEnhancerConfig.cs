using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    internal sealed class MNNResembleEnhancerConfig
    {
        internal const string FileName = "mnn_resemble_enhancer.json";
        public string denoiser_graph, encoder_cpu_graph, encoder_metal_graph, velocity_graph;
        public string decoder_cpu_graph, decoder_metal_graph, vocoder_graph, external_weights;
        public int cpu_denoiser_shape_cache_capacity;
        public float running_mean, running_std;
        public float[] analysis_window, mel_window, mel_filterbank, dt, time_embeddings;

        internal static MNNResembleEnhancerConfig Read(string directory)
        {
            var config = JsonUtility.FromJson<MNNResembleEnhancerConfig>(
                File.ReadAllText(Path.Combine(directory, FileName)));
            // Model.Verify pins the entire runtime profile before this parsing boundary.
            if (config == null || !(config.running_std > 0) || float.IsInfinity(config.running_std))
                throw new InvalidDataException("Invalid Resemble Enhance runtime profile.");
            Require(config.analysis_window, 1680);
            Require(config.mel_window, 2048);
            Require(config.mel_filterbank, 1025 * 128);
            Require(config.dt, 16);
            Require(config.time_embeddings, 32 * 128);
            return config;
        }

        private static void Require(float[] values, int length)
        {
            if (values == null || values.Length != length || values.Any(v => float.IsNaN(v) || float.IsInfinity(v)))
                throw new InvalidDataException("Invalid Resemble Enhance runtime coefficients.");
        }
    }
}
