using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNLsEendConfig
    {
        public const string FileName = "mnn_ls_eend.json";
        public const string CoefficientsFile = "ls_eend_coefficients.bin";
        public string repository, revision, checkpoint_sha256, encoder_graph, decoder_graph, coefficient_sha256;
        public int sample_rate, maximum_samples, fft_size, frame_size, frame_shift, mel_bins, context_size,
            subsampling, delay_frames, output_columns, trained_maximum_speakers, median_frames;
        public float threshold;
        public double[] analysis_window;
        public float[] mel_filters;

        public static MNNLsEendConfig Read(string directory)
        {
            var config = JsonUtility.FromJson<MNNLsEendConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
                ?? throw new InvalidDataException("Invalid LS-EEND profile.");
            var bytes = File.ReadAllBytes(Path.Combine(directory, CoefficientsFile));
            if (!BitConverter.IsLittleEndian || bytes.Length != 200 * 8 + 23 * 129 * 4)
                throw new InvalidDataException("Invalid LS-EEND coefficient file.");
            config.analysis_window = new double[200]; config.mel_filters = new float[23 * 129];
            Buffer.BlockCopy(bytes, 0, config.analysis_window, 0, 200 * 8);
            Buffer.BlockCopy(bytes, 200 * 8, config.mel_filters, 0, 23 * 129 * 4);
            return config;
        }
    }
}
