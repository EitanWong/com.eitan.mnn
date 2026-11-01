using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNDccrnModel
    {
        internal const string Graph = "dccrn.mnn", Analysis = "analysis.mnn", Encoder = "encoder.mnn";
        internal const string Recurrent = "recurrent.mnn", Decoder = "decoder.mnn", Weights = "dccrn.mnn.weight";
        internal static readonly string[] Files = { Graph, Analysis, Encoder, Recurrent, Decoder, Weights };
        private static readonly long[] Sizes = { 261192, 7472, 93072, 43576, 123968, 16940824 };
        private static readonly string[] Hashes = {
            "090b1dfe685892fe12136de4a40a843a405bf325aae5e38ffc0f97c12c262d83",
            "9c85dcbe0bdc99665f8ea798bf3db1098ed6748c1409ffadeba05ecd379a3039",
            "927090ff9b1458e454bdcae7ebff63b3034cdf4d4aaf70f1ac9e3ef0b5c91c1e",
            "1da2805d7611fc50cd4950dbe6f118a3f1e0857cc6cbbf647961f8f190fba5d9",
            "f0759c253ccceae3864ca05dc4f4857fd19e44e9d4489f26bd58bca7f547a284",
            "0b2c3c25f08febba6b65a9f9412ea7da2ef367d2ccbd349fac41cf4e35c39e6c"
        };

        internal static bool IsComplete(string directory)
        {
            try { ReadVerified(directory); return true; }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static MNNDccrnConfig ReadVerified(string directory)
        {
            var c = MNNDccrnConfig.Read(directory);
            if (c.repository != "shahules786/mayavoz-dccrn-valentini-28spk" ||
                c.revision != "874b526f313a9d85a61105a403dd17f84cbae93d" ||
                c.source_revision != "7133670252c9e24dd55ea045791b310d83b73d1c" ||
                c.checkpoint_loading != "strict-assign-first-eval" || c.normalization != "none" ||
                c.graph != Graph || c.analysis_graph != Analysis || c.encoder_graph != Encoder ||
                c.recurrent_graph != Recurrent || c.decoder_graph != Decoder || c.external_weights != Weights ||
                c.sample_rate != 16000 || c.minimum_samples != 1 || c.maximum_samples != 48000 ||
                c.fft_size != 512 || c.window_size != 400 || c.hop_size != 100)
                throw new InvalidDataException("Unrecognized checkpoint-owned Mayavoz DCCRN contract.");
            for (int i = 0; i < Files.Length; ++i)
            {
                using (var stream = File.OpenRead(Path.Combine(directory, Files[i])))
                using (var sha = SHA256.Create())
                    if (stream.Length != Sizes[i] || BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() != Hashes[i])
                        throw new InvalidDataException("DCCRN runtime file failed verification: " + Files[i]);
            }
            return c;
        }
    }
}
