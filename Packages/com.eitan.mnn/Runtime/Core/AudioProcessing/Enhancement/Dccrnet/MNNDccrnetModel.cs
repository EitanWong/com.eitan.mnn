using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNDccrnetModel
    {
        internal const string Graph = "dccrnet_native_fp32.mnn";
        internal const string Encoder = "dccrnet_encoder_fp32.mnn";
        internal const string Recurrent = "dccrnet_recurrent_fp32.mnn";
        internal const string Decoder = "dccrnet_decoder_fp32.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNDccrnetConfig.Read(directory);
                if (config.repository != "JorisCos/DCCRNet_Libri1Mix_enhsingle_16k" ||
                    config.revision != "00914c64c47932a360b5b4e0c07ab12d305604ba" || config.asteroid_version != "0.7.0" || config.filterbanks_version != "0.4.0" ||
                    config.graph != Graph || config.encoder_graph != Encoder || config.recurrent_graph != Recurrent || config.decoder_graph != Decoder ||
                    config.sample_rate != 16000 || config.speakers != 1 || config.input != "mixture" || config.output != "enhanced" ||
                    config.normalization != "none; original forward_wav" || config.minimum_samples != 1000 || config.maximum_samples != 48000) return false;
                return
                    Verify(directory, "dccrnet_native_fp32.mnn", 27382952, "ebf33239eeee071a6c322500650cf0cfc23243989a2599b343d5ed163064440c") &&
                    Verify(directory, "dccrnet_encoder_fp32.mnn", 7820008, "cb7fd82f15b63c10dc35d7e9d41f59bf45b7588311a508209f3287502a586bef") &&
                    Verify(directory, "dccrnet_recurrent_fp32.mnn", 4772220, "a68b38584b592f4ac23059ed95a9b5369221ab25d7571837a21aab693a102f25") &&
                    Verify(directory, "dccrnet_decoder_fp32.mnn", 14791200, "d409013a5aa6d560d22e8d4db3b22660e8c5161955aa38cfb17e9227c28f3391");
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
        private static bool Verify(string directory, string graph, long bytes, string checksum)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, graph)))
            using (var sha = SHA256.Create())
                return stream.Length == bytes && BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant() == checksum;
        }
    }
}
