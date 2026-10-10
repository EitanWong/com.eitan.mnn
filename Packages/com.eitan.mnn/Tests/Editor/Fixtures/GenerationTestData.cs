using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    internal static class GenerationTestData
    {
        internal static string Model(string repository)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_GENERATION_ROOT") ?? Path.Combine(Application.streamingAssetsPath, "MNN", "Models");
            string directory = Path.Combine(root, repository);
            if (!Directory.Exists(directory))
            {
                if (Environment.GetEnvironmentVariable("MNN_REQUIRE_MODEL_TESTS") == "1")
                    Assert.Fail("Required generation model is missing: " + directory);
                Assert.Ignore("Install generation model or set MNN_TEST_GENERATION_ROOT: " + repository);
            }

            return directory;
        }
    }
}
