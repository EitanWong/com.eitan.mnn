using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNVbxConfig
    {
        public const string FileName = "mnn_vbx.json", CoefficientsFile = "vbx_coefficients.bin";
        public string repository, revision, checkpoint_sha256, graph, coefficient_sha256, execution, input, output;
        public int sample_rate, minimum_samples, maximum_samples, fft_size, frame_size, frame_shift, mel_bins,
            embedding_dimension, cmn_left, cmn_right, dither_seed, dither_level;
        [NonSerialized] internal double[] Window, Mel;

        public static MNNVbxConfig Read(string directory)
        {
            var config = JsonUtility.FromJson<MNNVbxConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
                ?? throw new InvalidDataException("Invalid VBx profile.");
            var bytes = File.ReadAllBytes(Path.Combine(directory, CoefficientsFile));
            if (!BitConverter.IsLittleEndian || bytes.Length != 134784)
                throw new InvalidDataException("Invalid VBx coefficients.");
            config.Window = new double[400]; config.Mel = new double[257 * 64];
            Buffer.BlockCopy(bytes, 0, config.Window, 0, 400 * 8);
            Buffer.BlockCopy(bytes, 400 * 8, config.Mel, 0, config.Mel.Length * 8);
            return config;
        }
    }
}
