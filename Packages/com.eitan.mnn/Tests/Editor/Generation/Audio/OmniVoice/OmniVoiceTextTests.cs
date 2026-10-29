using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class OmniVoiceTextTests
    {
        [Test]
        public void EncodesPublishedChinesePromptLikeUpstreamTokenizer()
        {
            string root = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", "OmniVoice-MNN");
            if (!File.Exists(Path.Combine(root, "tokenizer.json")))
                Assert.Ignore("OmniVoice-MNN tokenizer asset is not installed.");

            var text = new MNNOmniVoiceText(root);
            CollectionAssert.AreEqual(new[] {151670, 23815, 151671, 151672, 4064, 151673}, text.Style("zh", null, false));
            CollectionAssert.AreEqual(new[] {151674, 108386, 3837, 100437, 104071, 63488, 7751, 51167, 1773, 151675},
                text.Content("你好，欢迎来到 OmniVoice。", null));
        }
    }
}
