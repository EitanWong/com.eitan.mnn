using System;
using System.IO;
using System.Security.Cryptography;

namespace MNN.Unity
{
    internal static class MNNDeepFilterNet3Model
    {
        internal const string EncoderFp32 = "enc_fp32.mnn", ErbDecoderFp32 = "erb_dec_fp32.mnn", DfDecoderFp32 = "df_dec_fp32.mnn";
        internal const string Encoder = "enc_fp16.mnn", ErbDecoder = "erb_dec_fp16.mnn", DfDecoder = "df_dec_fp16.mnn";
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNDeepFilterNet3Config.Read(directory);
                return config.repository == "Serkan007/DeepFilterNet3-ONNX" && config.sample_rate == 48000 &&
                    config.fft_size == 960 && config.hop_size == 480 && config.erb_bins == 32 && config.df_bins == 96 &&
                    config.encoder == Encoder && config.erb_decoder == ErbDecoder && config.df_decoder == DfDecoder &&
                    config.encoder_fp32 == EncoderFp32 && config.erb_decoder_fp32 == ErbDecoderFp32 && config.df_decoder_fp32 == DfDecoderFp32 &&
                    config.execution_mode == "offline_native_gru" && config.df_order == 5 && config.df_lookahead == 2 && config.conv_lookahead == 2 && config.norm_alpha == .99f && config.max_audio_seconds == 30 &&
                    Verify(directory, "enc_fp16.mnn", 997520, "75b4ac6a5d9111d5ff0794dde3729d5f25963baf28d15b5acf19f7895c43a23b") &&
                    Verify(directory, "erb_dec_fp16.mnn", 1665084, "90d81466dbfef75ce2845c9b86dd88085a07fba8cd317201ac4a09b569c987d5") &&
                    Verify(directory, "df_dec_fp16.mnn", 1686708, "4d821e082f0152fa336824ad084fba3c2fc52af9d6790208025be96632082d2e") &&
                    Verify(directory, "enc_fp32.mnn", 1963316, "19937be3d7849b36081fbbfca8f5322586d809da4ec13889ec315f9bdd08ef7a") &&
                    Verify(directory, "erb_dec_fp32.mnn", 3302956, "e3471a4360143b5889bbfc46bcd0bc145bec6899c607dc5aa3969da0bbc19439") &&
                    Verify(directory, "df_dec_fp32.mnn", 3349284, "3cf61dd97f0516b4a2f986059288e627895032eabd98f69a21d29c519c683918");
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
        private static bool Verify(string directory, string name, long bytes, string hash)
        {
            using (var stream = File.OpenRead(Path.Combine(directory, name)))
            using (var sha = SHA256.Create())
                return stream.Length == bytes && string.Equals(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""), hash, StringComparison.OrdinalIgnoreCase);
        }
    }
}
