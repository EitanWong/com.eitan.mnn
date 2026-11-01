using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    public enum MNNImageTaskProfile { ModNet, RealEsrganX4, UVDoc, Scrfd500M, MobileFaceNet, Landmark106, MobileNetV2, PglNet, CandyStyle, DeepLabV3, MoveNetLightning, DepthAnythingV2Small }

    /// <summary>Exact publisher exports with implemented preprocessing and decoding.
    /// A profile does not certify every export sharing the architecture name.</summary>
    [Serializable]
    public sealed class MNNImageTaskConfig
    {
        public const string FileName = "mnn_image_task.json";
        public MNNImageTaskProfile profile;
        internal MNNImageTaskConfig Clone() => new MNNImageTaskConfig {profile = profile};

        public static MNNImageTaskConfig Read(string directory)
        {
            var config = JsonUtility.FromJson<MNNImageTaskConfig>(File.ReadAllText(Path.Combine(directory, FileName)));
            if (config == null || !Enum.IsDefined(typeof(MNNImageTaskProfile), config.profile))
                throw new InvalidDataException("Unknown image task profile.");
            return config;
        }

        internal string Resolve(string directory, bool verifyHash)
        {
            var source = MNNImageTaskProfiles.Get(profile);
            string path = Path.Combine(Path.GetFullPath(directory), source.Graph);
            if (!File.Exists(path)) throw new FileNotFoundException("Missing image task graph: " + path, path);
            if (verifyHash)
            {
                if (profile == MNNImageTaskProfile.MobileNetV2)
                {
                    string labels = Path.Combine(Path.GetFullPath(directory), "labels.txt");
                    if (!File.Exists(labels)) throw new FileNotFoundException("Missing ImageNet labels.", labels);
                    using (var labelStream = File.OpenRead(labels))
                    using (var labelHash = SHA256.Create())
                        if (BitConverter.ToString(labelHash.ComputeHash(labelStream)).Replace("-", "").ToLowerInvariant() != "07994f2a44b48305675f84979bfc22f4f58d3e99da2db514142917882250a926")
                            throw new InvalidDataException("ImageNet labels do not match the pinned class order.");
                }
                using (var stream = File.OpenRead(path))
                using (var hash = SHA256.Create())
                    if (!string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), source.Hash, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("Image profile requires the pinned publisher graph: " + source.Hash);
            }
            return path;
        }
    }
}
