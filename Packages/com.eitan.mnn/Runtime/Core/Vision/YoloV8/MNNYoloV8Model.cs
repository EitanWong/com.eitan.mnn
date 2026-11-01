using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace MNN.Unity
{
    internal static class MNNYoloV8Model
    {
        internal const string Repository = "rap20171201/YOLOv8-N-MNN";
        internal const long GraphBytes = 6342828;
        internal const string GraphSha256 = "6e0c36d3c687834c9501295af9d6a16c638e60f81c702c0b2626ea93241b619b";
        internal static bool IsRepository(string repository) => string.Equals(repository, Repository, StringComparison.OrdinalIgnoreCase);

        internal static MNNYoloV8Config Config() => new MNNYoloV8Config
        {
            boxEncoding = MNNYoloV8BoxEncoding.AnchorDistances,
            labels = ("person|bicycle|car|motorcycle|airplane|bus|train|truck|boat|traffic light|fire hydrant|stop sign|parking meter|bench|bird|cat|dog|horse|sheep|cow|elephant|bear|zebra|giraffe|backpack|umbrella|handbag|tie|suitcase|frisbee|skis|snowboard|sports ball|kite|baseball bat|baseball glove|skateboard|surfboard|tennis racket|bottle|wine glass|cup|fork|knife|spoon|bowl|banana|apple|sandwich|orange|broccoli|carrot|hot dog|pizza|donut|cake|chair|couch|potted plant|bed|dining table|toilet|tv|laptop|mouse|remote|keyboard|cell phone|microwave|oven|toaster|sink|refrigerator|book|clock|vase|scissors|teddy bear|hair drier|toothbrush").Split('|')
        };

        internal static void Prepare(string directory)
        {
            string graph = Config().ResolveGraph(directory);
            using (var stream = File.OpenRead(graph))
            using (var hash = SHA256.Create())
                if (stream.Length != GraphBytes || !string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""), GraphSha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The YOLOv8 profile requires the verified publisher graph: " + GraphSha256);
            File.WriteAllText(Path.Combine(directory, MNNYoloV8Config.FileName), JsonUtility.ToJson(Config(), true));
        }
    }
}
