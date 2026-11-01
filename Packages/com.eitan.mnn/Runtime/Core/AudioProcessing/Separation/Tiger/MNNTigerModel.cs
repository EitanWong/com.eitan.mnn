using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNTigerModel
    {
        internal const string Graph = "tiger_fp32.mnn";
        private const string WindowHash = "ca961f55e3d74499159074e0401c180a88f41c6a4a7d9540fb733cb3d8a91a27";

        internal static bool IsComplete(string directory)
        {
            try { ReadVerified(directory); return true; }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static MNNTigerConfig ReadVerified(string directory)
        {
            var c = MNNTigerConfig.Read(directory);
            if (c.repository != "JusperLee/TIGER-speech" || c.revision != "f0340340b2d9bbf72074edf8c076dcab59a10ba2" ||
                c.source_revision != "9f18d4a10a7137e1ce8052cfb62215179f1287b6" || c.graph != Graph || c.causal ||
                c.sample_rate != 16000 || c.speakers != 2 || c.fft_size != 640 || c.hop_size != 160 ||
                c.minimum_samples != 321 || c.maximum_samples != 48000 || c.input != "spectrum" || c.output != "separated" ||
                c.coefficient_sha256 != WindowHash || c.analysis_window == null || c.analysis_window.Length != 640)
                throw new InvalidDataException("Unrecognized TIGER source or spectral contract.");
            var coefficients = new byte[640 * 4];
            Buffer.BlockCopy(c.analysis_window, 0, coefficients, 0, coefficients.Length);
            using (var sha = SHA256.Create())
                if (Hash(sha.ComputeHash(coefficients)) != WindowHash)
                    throw new InvalidDataException("TIGER analysis coefficients failed verification.");
            Verify(Path.Combine(directory, Graph), 5946144, "28299e823a45e74d9c603e9678a8da2dcf1d97dd66b95d046b4786208df18fb9");
            Verify(Path.Combine(directory, Graph + ".weight"), 3179296, "b774ab4e60aa2d67e3f1f2a590a613bc3f2be2cb189ea125852a78819c689b80");
            return c;
        }

        private static void Verify(string path, long size, string expected)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                if (stream.Length != size || Hash(sha.ComputeHash(stream)) != expected)
                    throw new InvalidDataException("TIGER runtime file failed verification: " + Path.GetFileName(path));
        }

        private static string Hash(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}
