using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class MNNLlmValidationTests
    {
        [Test]
        public void Load_RejectsNullDirectory() => Assert.Throws<ArgumentNullException>(() => MNNLlm.Load(null));
        [Test]
        public void Load_RejectsMissingConfig() => Assert.Throws<FileNotFoundException>(() => MNNLlm.Load(Path.GetFullPath("TestArtifacts~/MNNValidation/NonexistentModel")));
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(65)]
        public void Load_RejectsInvalidThreadCount(int count) => Assert.Throws<ArgumentOutOfRangeException>(() => MNNLlm.Load(".", count));
    }
}
