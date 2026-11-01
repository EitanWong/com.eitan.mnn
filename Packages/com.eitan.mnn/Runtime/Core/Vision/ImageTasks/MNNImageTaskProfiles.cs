using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNImageTaskProfiles
    {
        internal sealed class Source
        {
            internal readonly MNNImageTaskProfile Profile;
            internal readonly string Repository, Graph, Hash, Url;
            internal readonly long Bytes;
            internal Source(MNNImageTaskProfile profile, string repository, string graph, string hash, long bytes, string url)
            { Profile = profile; Repository = repository; Graph = graph; Hash = hash; Bytes = bytes; Url = url; }
        }
        internal static readonly Source[] All = {
            new Source(MNNImageTaskProfile.ModNet, "jingluo20190901/modnet_mnn", "modnet_fp32.mnn", "6ab24229febee181fcf1a6ec9943e816a57fa950307054949c767470636403ac", 25884516, "https://www.modelscope.cn/api/v1/models/jingluo20190901/modnet_mnn/repo?Revision=5ce4227ba989f3a1d8bc4c8c49695cb14d18a846&FilePath=modnet_fp32.mnn"),
            new Source(MNNImageTaskProfile.RealEsrganX4, "jingluo20190901/realesrgan_mnn", "realesr_general_x4v3.mnn", "93941e751bfcddf8efc35bea72e2b93ac57ca163ea785c65c2c0d5a06a6e77d9", 4891644, "https://www.modelscope.cn/api/v1/models/jingluo20190901/realesrgan_mnn/repo?Revision=c07cd42983cb1f327d63617962f1d367ec5ef2b9&FilePath=realesr_general_x4v3.mnn"),
            new Source(MNNImageTaskProfile.UVDoc, "dr3334/UVDoc-MNN", "uvdoc_grid_fp32_baseline.mnn", "daa02297e339fdb26b921c9f11cd5a39fb08fa0f92445bf39cd381ff9dc33c3d", 31581956, "https://www.modelscope.cn/api/v1/models/dr3334/UVDoc-MNN/repo?Revision=4231583c216094e4278a1b721826cd504979afca&FilePath=uvdoc_grid_fp32_baseline.mnn"),
            new Source(MNNImageTaskProfile.Scrfd500M, "budaoshou/InsightFace-Det500M-MNN", "det_500m.mnn", "2de88e1883d04a001d586be68bd52e4ed8291f253ec6e9b163fbf440c5c88964", 1285612, "https://www.modelscope.cn/api/v1/models/budaoshou/InsightFace-Det500M-MNN/repo?Revision=c0cf15cc0f64c043b52fde50f823a388a270d675&FilePath=det_500m.mnn"),
            new Source(MNNImageTaskProfile.MobileFaceNet, "budaoshou/InsightFace-MobileFaceNet-MNN", "w600k_mbf.mnn", "857bde5d5112009b31d65636890fd931d166cc766d271125ca27f48b2f2fc9d9", 6865800, "https://www.modelscope.cn/api/v1/models/budaoshou/InsightFace-MobileFaceNet-MNN/repo?Revision=2276b19e2c03793fedc90ff0e9e06853af96fa8c&FilePath=w600k_mbf.mnn"),
            new Source(MNNImageTaskProfile.Landmark106, "budaoshou/InsightFace-2D106Det-MNN", "2d106det.mnn", "7afa88285a78dc1a997dc63ae5023a9e8c1bd31889e3c5c113faaa3ab06ed865", 4975640, "https://www.modelscope.cn/api/v1/models/budaoshou/InsightFace-2D106Det-MNN/repo?Revision=a77992f85443049d093d38a6c2ffa09a7d7f39e6&FilePath=2d106det.mnn"),
            new Source(MNNImageTaskProfile.MobileNetV2, "wangzhaode/mnn-mobilenet", "mobilenet_v2.mnn", "eec5c31faa3a88ffdb38203d29aaf4691752e150bee1226bc873d29a19788f4e", 13966192, "https://github.com/wangzhaode/mnn-mobilenet/releases/download/v1.0/mobilenet_v2.mnn"),
            new Source(MNNImageTaskProfile.PglNet, "klay11/PGL-Net", "rw2ah_pglnet_t.mnn", "9010b515b3f2987ca7bb4aab74634592c3422f9e3d5880aa8543597cdad889b7", 3156020, "https://huggingface.co/klay11/PGL-Net/resolve/main/mnn/rw2ah_pglnet_t.mnn"),
            // Prepared with an unmodified official converter; only the public
            // random Net.mnn_uuid metadata is canonicalized for a repeatable SHA.
            // No publisher-hosted MNN graph exists: use Tools~/Models/prepare_image_profile.py.
            new Source(MNNImageTaskProfile.CandyStyle, "DefTruth/FastStyleTransfer-Candy", "style-candy-8.mnn", "70c1069827b8df36212313e915abf727a046baf8b4fb95efb8eac612ee66047a", 6734176, null),
            new Source(MNNImageTaskProfile.DeepLabV3, "TensorFlow/DeepLabV3-MobileNetV2", "deeplabv3.mnn", "b510c32e45d689ebc7b8a0bf5070a89dafb77411c350a564f1e5b13f0038b640", 2769072, null),
            new Source(MNNImageTaskProfile.MoveNetLightning, "Xenova/movenet-singlepose-lightning", "movenet-lightning.mnn", "79b22749faaf89303f3714ba6fdf0c66a647b20425076cbd7ea64d8d9ec39d69", 9349132, null),
            // Dynamic positional cubic is decomposed into equivalent standard
            // ONNX arithmetic before conversion; exact GELU is retained.
            new Source(MNNImageTaskProfile.DepthAnythingV2Small, "onnx-community/depth-anything-v2-small", "depth-anything-v2-small.mnn", "a91629bc3c318f129ba0461384a8a5f9a58e6c998f8e01a6121ec8bb30b5f9fa", 99030556, null)
        };
        internal static Source Get(MNNImageTaskProfile profile)
        {
            foreach (var source in All) if (source.Profile == profile) return source;
            throw new ArgumentOutOfRangeException(nameof(profile));
        }
        internal static bool IsRepository(string repository)
        {
            foreach (var source in All) if (source.Url != null && string.Equals(source.Repository, repository, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
        internal static void Prepare(string directory, string repository)
        {
            foreach (var source in All)
                if (string.Equals(source.Repository, repository, StringComparison.OrdinalIgnoreCase))
                {
                    var config = new MNNImageTaskConfig {profile = source.Profile};
                    config.Resolve(directory, true);
                    File.WriteAllText(Path.Combine(directory, MNNImageTaskConfig.FileName), JsonUtility.ToJson(config, true));
                    return;
                }
            throw new NotSupportedException("No implemented image protocol for this repository.");
        }
    }
}
