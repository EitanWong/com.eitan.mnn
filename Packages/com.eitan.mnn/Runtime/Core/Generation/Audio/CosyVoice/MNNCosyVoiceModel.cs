using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    // File contract for the upstream complete fp16 release. This describes assets,
    // independently of whether the current platform can execute the pipeline.
    internal static class MNNCosyVoiceModel
    {
        internal const string Repository = "VicenTrent/Cosy-Voice-MNN";
        internal const string Revision = "948b6a2600df25d9d01960a6fa34c126c02b66da";
        internal const string Archive = "cosyvoice3-mnn-mobile-fp16-complete.zip";
        internal const string ArchiveSha256 = "B1C74DFC90972D82D8166813620A882FE37A0DC02964E19C4F33DAAFEFEB1C84";
        internal const long ArchiveBytes = 1399083563;
        internal static readonly string[] RequiredFiles = {"config-cpu-cosyvoice-ras.json", "embeddings_bf16.bin", "flow-conditioner.fp32.mnn", "flow.cfg-student-2step.batch1.fp16.mnn", "flow.cfg-student-2step.batch1.fp16.mnn.weight", "hift-core.fp32.mnn", "hift-f0.fp32.mnn", "llm_config.json", "llm.mnn", "llm.mnn.weight", "manifest.json", "prompt-cond.bin", "prompt-speech-tokens.csv", "rand-noise.bin", "source-linear-bias.bin", "source-linear-weight.bin", "spks.bin", "tokenizer.mtok"};
        internal static readonly long[] RequiredSizes = {504, 284426240, 4403404, 1340608, 663253312, 70218952, 13271724, 456, 419640, 352844666, 2869, 55680, 400, 4800000, 4, 36, 320, 4042638};
        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);
        internal static string ResolveDirectory(string directory) => File.Exists(Path.Combine(directory, "manifest.json")) ? directory : Path.Combine(directory, "model");
        internal static bool IsComplete(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return false;
            try
            {
                directory = ResolveDirectory(directory);
                return RequiredFiles.Select((file, i) => new FileInfo(Path.Combine(directory, file))).Select((file, i) => file.Exists && file.Length == RequiredSizes[i]).All(valid => valid);
            }
            catch (Exception error)when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                return false;
            }
        }
    }
}
