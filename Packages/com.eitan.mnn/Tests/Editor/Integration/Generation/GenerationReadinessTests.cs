using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    /// <summary>Explicit readiness gates. Fail, rather than skip/pass, when the requested
    /// generation pipeline is unavailable. These checks do not validate generated outputs.</summary>
    public class GenerationReadinessTests
    {
        [TestCase("stable-diffusion-v1-5-mnn", 3), TestCase("supertonic-tts-mnn", 5), Explicit("Requires real installed generation weights; checks Studio routing, not output quality.")]
        public void DedicatedRuntimeMustBeAvailable(string repository, int taskIndex)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_GENERATION_ROOT") ?? Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT") ?? Path.Combine(Application.streamingAssetsPath, "MNN", "Models");
            var model = MNNStudioModel.Read(Path.Combine(root, repository));
            Assert.True(MNNStudioTasks.Get(taskIndex).CanRun(model), "Complete supported generation model must route to its dedicated runtime.");
        }

        [TestCase("stable-diffusion-v1-5-mnn"), TestCase("supertonic-tts-mnn"), Explicit("Requires real generation weights already installed locally. Never downloads models.")]
        public void RepresentativeWeightsMustBeInstalled(string repository)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_GENERATION_ROOT") ?? Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT") ?? Path.Combine(Application.streamingAssetsPath, "MNN", "Models");
            string directory = Path.Combine(root, repository);
            Assert.True(Directory.Exists(directory), "Missing real model repository: " + directory);
            Assert.True(Directory.EnumerateFiles(directory, "*.mnn", SearchOption.AllDirectories).Any(), "No MNN graph files in " + directory + ". Presence alone is not an integrity or inference test.");
        }
    }
}
