using System;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class DtlnAecCatalogTests
    {
        [Test]
        public void AllVariantsAreSearchableWithoutHubIndexAndMatchDedicatedStudioTask()
        {
            var repository = MNNModelRepository.GetDefaultRepository(); repository.IncludeDtlnAecProfiles();
            foreach (string query in new[] {"DTLN", "AEC", "回声消除"})
            {
                var models = repository.SearchModels(query).Where(model => MNNDtlnAecModel.IsRepository(model.repositoryId)).ToList();
                Assert.AreEqual(3, models.Count);
                foreach (var model in models)
                {
                    Assert.AreEqual(MNNModelCategory.EchoCancellation.ToString(), model.category);
                    Assert.True(model.runtimeAvailable); Assert.AreEqual(2, model.mnnFiles.Length);
                    Assert.True(MNNStudioTasks.Get((int)MNNStudioTask.EchoCancellation).MatchesCatalog(model));
                    var plan = MNNModelDownloadService.DtlnAecDownloadPlan(model.repositoryId);
                    Assert.AreEqual(3, plan.Length); Assert.True(plan.All(file => file.Revision.Length == 40 && file.Sha256.Length == 64));
                    Assert.AreEqual(model.fileSize, plan.Sum(file => file.Size));
                }
            }
            Assert.AreEqual("EchoCancellation", MNNModelRepository.Classify("EitanWang/DTLN-aec-128-MNN", tasks: new[] {"audio-to-audio"}));
        }
        [Test]
        public void SerializedTaskIndicesAndUnknownProfilesStaySafe()
        {
            for (int i = 0; i < MNNStudioTasks.All.Length; ++i) Assert.AreEqual(i, (int)MNNStudioTasks.All[i].Task);
            Assert.Throws<ArgumentException>(() => MNNModelDownloadService.DtlnAecDownloadPlan("other/DTLN-aec-128-MNN"));
        }
    }
}
