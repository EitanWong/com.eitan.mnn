using MNN.Unity.Interop;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class LlmConversationCachePolicyTests
    {
        [TestCase("{}", true)]
        [TestCase("{\"attention_mode\":0}", true)]
        [TestCase("{\"attention_mode\":8}", true)]
        [TestCase("{\"attention_mode\":1}", false)]
        [TestCase("{\"attention_mode\":2}", false)]
        [TestCase("{\"attention_mode\":9}", false)]
        [TestCase("{\"attention_mode\":10}", false)]
        [TestCase("{\"attention_mode\":11}", false)]
        [TestCase("{\"attention_mode\":12}", false)]
        [TestCase("{\"quant_qkv\":2}", false)]
        [TestCase("{\"attention_mode\":8,\"quant_qkv\":2}", true)]
        [TestCase("{\"attention_mode\":10,\"quant_qkv\":0}", false)]
        [TestCase("{\"has_talker\":true}", false)]
        [TestCase("{\"speculative_type\":\"lookahead\"}", false)]
        [TestCase("{\"has_talker\":false,\"speculative_type\":\"\"}", true)]
        public void CachePolicyHonorsEffectiveAttentionConfiguration(string configuration, bool expected)
            => Assert.AreEqual(expected, MNNInterop.SupportsChatCacheConfiguration(configuration));
    }
}
