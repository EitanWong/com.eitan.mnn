using MNN.Unity;
using NUnit.Framework;
namespace MNN.Unity.Tests
{
    public sealed class VisualModelProfileTests
    {
        [TestCase("budaoshou/InsightFace-Det500M-MNN", MNNImageTaskProfile.Scrfd500M)]
        [TestCase("budaoshou/InsightFace-MobileFaceNet-MNN", MNNImageTaskProfile.MobileFaceNet)]
        public void ExistingPublisherProfileResolvesWithoutTrustingModelName(string repository, MNNImageTaskProfile expected)
        {
            var model = new MNNRemoteModel { provider = MNNModelSource.ModelScope, repositoryId = repository };
            Assert.That(MNNImageTaskConfig.TryGetProfile(model, out var actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
            model.provider = MNNModelSource.HuggingFace;
            Assert.That(MNNImageTaskConfig.TryGetProfile(model, out _), Is.False);
            model.repositoryId = "unknown/face-model";
            model.category = MNNModelCatalogCategory.FaceDetection.ToString();
            Assert.That(MNNImageTaskConfig.TryGetProfile(model, out _), Is.False);
        }
    }
}
