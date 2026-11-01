using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNWeSpeakerModel
    {
        internal const string Repository = "manyeyes/speaker_recognition_task_models_onnx_collection/wespeaker_en_voxceleb_resnet34_LM";
        internal const string Graph = "wespeaker_resnet34_lm_fp32.mnn";
        internal const long Bytes = 26534768;
        internal const string Hash = "ffc420aee441850ca78267dd7e1731b3e1914a38bc2dbe99c902736e25404023";

        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNWeSpeakerModelConfig.Read(directory);
                return IsRepository(config.repository) && config.graph == Graph && File.Exists(Path.Combine(directory, Graph)) &&
                    new FileInfo(Path.Combine(directory, Graph)).Length == Bytes;
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
        internal static void Verify(string directory)
        {
            if (!IsComplete(directory) || HashFile(Path.Combine(directory, Graph)) != Hash)
                throw new InvalidDataException("Incomplete pinned WeSpeaker graph.");
        }
        private static string HashFile(string path)
        {
            using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
