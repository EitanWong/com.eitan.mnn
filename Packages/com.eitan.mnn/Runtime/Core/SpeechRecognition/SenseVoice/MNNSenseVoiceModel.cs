using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNSenseVoiceModel
    {
        internal const string Repository = "wo105090/SenseVoiceSmall-MNN";
        internal const string HuggingFaceRepository = "xinliu/sensevoice_mnn";
        internal const string HuggingFaceRevision = "7ca517021b1a780246859989f2dd12c1f1db3520";
        internal static readonly string[] HuggingFacePaths = {"sensevoice.mnn", "tokens.txt"};
        internal static readonly long[] HuggingFaceSizes = {266565516, 315894};
        private static readonly string[] HuggingFaceHashes =
        {
            "f5b77214caf52bcc708960ee06b11a6efb26c6264d753ef8e12fa24fe8ae9e9a",
            "f449eb28dc567533d7fa59be34e2abca8784f771850c78a47fb731a31429a1dc"
        };
        internal static readonly string[] Paths = {"model.mnn", "model.mnn.weight", "tokens.txt"};
        internal static readonly long[] Sizes = {1640496, 135020930, 340949};
        private static readonly string[] Hashes =
        {
            "1f213de949bc161cf8d15a7da931f2a4a777e0c0707c8f0e87dc2f3a703a09e5",
            "048ad55bc9538ccd3edbf124fc36d5a7d7e76d4f20b21ec139e24d907cc31472",
            "4c2b204bcd9ed037cc14cba44a2046cfa50b39d7702704778c0c5ef0de1d8683"
        };
        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase) || IsHuggingFaceRepository(repository);
        internal static bool IsHuggingFaceRepository(string repository) => string.Equals(repository, HuggingFaceRepository, StringComparison.OrdinalIgnoreCase);
        internal static bool IsHuggingFace(MNNSenseVoiceConfig config)
        {
            if (config.profile != "modelscope-small" && config.profile != "huggingface-small")
                throw new ArgumentException("Unknown verified SenseVoice profile.", nameof(config));
            return config.profile == "huggingface-small";
        }
        internal static MNNSenseVoiceConfig Config(bool huggingFace) => huggingFace
            ? new MNNSenseVoiceConfig {profile = "huggingface-small", model = "sensevoice.mnn"} : new MNNSenseVoiceConfig();
        internal static string[] Assets(MNNSenseVoiceConfig config) => IsHuggingFace(config)
            ? new[] {config.model, config.tokens} : new[] {config.model, config.model + ".weight", config.tokens};
        internal static void Verify(string directory, MNNSenseVoiceConfig config)
        {
            var paths = Assets(config);
            var sizes = IsHuggingFace(config) ? HuggingFaceSizes : Sizes;
            var hashes = IsHuggingFace(config) ? HuggingFaceHashes : Hashes;
            for (int i = 0; i < paths.Length; ++i)
                using (var stream = File.OpenRead(MNNZipformerConfig.Resolve(directory, paths[i])))
                using (var hash = SHA256.Create())
                    if (stream.Length != sizes[i] || !string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), hashes[i], StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("SenseVoice profile file failed verification: " + paths[i]);
        }
        internal static void Prepare(string directory, bool huggingFace = false)
        {
            var config = Config(huggingFace);
            Verify(directory, config);
            File.WriteAllText(Path.Combine(directory, MNNSenseVoiceConfig.FileName), JsonUtility.ToJson(config, true));
        }
        internal static bool IsComplete(string directory)
        {
            try
            {
                var config = MNNSenseVoiceConfig.Read(directory);
                var sizes = IsHuggingFace(config) ? HuggingFaceSizes : Sizes;
                return Assets(config).Select((path, i) =>
                    new FileInfo(MNNZipformerConfig.Resolve(directory, path)).Length == sizes[i]).All(match => match);
            }
            catch (Exception error) when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                return false;
            }
        }
    }
}
