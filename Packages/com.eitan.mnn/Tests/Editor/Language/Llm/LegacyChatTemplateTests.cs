using MNN.Unity.Interop;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class LegacyChatTemplateTests
    {
        [TestCase("{}")]
        [TestCase("{\"prompt_template\":\"%s\"}")]
        [TestCase("{\"prompt_template\":\"<|im_start|>user\\n%s<|im_end|>\\n<|im_start|>assistant\\nextra\"}")]
        public void UnknownLegacyFormatsAreNotGuessed(string configuration)
        {
            Assert.IsNull(MNNInterop.LegacyChatTemplateOptions(configuration));
        }

        [Test]
        public void ExplicitJinjaRetainsItsOwnRoleAndToolRules()
        {
            const string configuration = "{\"prompt_template\":\"<|im_start|>user\\n%s<|im_end|>\\n<|im_start|>assistant\\n\",\"jinja\":{\"chat_template\":\"custom role and tool rules\"}}";
            Assert.IsNull(MNNInterop.LegacyChatTemplateOptions(configuration));
        }
    }
}
