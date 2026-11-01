using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNDtlnModel
    {
        internal static string Graph(int stage, bool compact) => "dtln_" + stage + (compact ? "_fp16.mnn" : "_fp32.mnn");
        internal static bool IsComplete(string directory)
        {
            try
            {
                var c = MNNDtlnConfig.Read(directory);
                return c.repository == "breizhn/DTLN" && c.sample_rate == 16000 && c.frame_length == 512 && c.hop_length == 128 &&
                    c.units == 128 && c.output_delay_samples == 384 && c.offline_alignment == "flush_three_hops_crop_384" &&
                    c.stage1_fp32 == Graph(1, false) && c.stage2_fp32 == Graph(2, false) &&
                    c.stage1_fp16 == Graph(1, true) && c.stage2_fp16 == Graph(2, true) &&
                    Verify(directory, Graph(1, false), 1467040, "258180bd903582d1c303cf4b47b3d1f1d2190a342c4066284e859d972e84dd22") &&
                    Verify(directory, Graph(2, false), 2532352, "40b6cd93f0709af8515cad9c3971cf331f395c0fd8167565645fee8dbcfc2a1e") &&
                    Verify(directory, Graph(1, true), 745136, "d4c0d3acd9fe6d2cbf86b27711c0abd3eacf49af499d4dab1c5bea3660639530") &&
                    Verify(directory, Graph(2, true), 1283920, "25d1f0db20c1ea5e86fb95088a73c4c88b4ccfc0735ab8a2bf0f55d53a8048c3");
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
        private static bool Verify(string directory, string file, long bytes, string hash)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, file)))
            using (var sha = SHA256.Create())
                return stream.Length == bytes && string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), hash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
