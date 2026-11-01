using System;
using System.IO;
using System.Linq;
using MNN.Unity;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class SherpaZipformerAsrTests
    {
        [TestCase("sherpa-mnn-streaming-zipformer-en-2023-02-21")]
        [TestCase("sherpa-mnn-streaming-zipformer-bilingual-zh-en-2023-02-20")]
        public void ModelGraphLoadsExpectedStreamingInputsAndCacheVariables(string modelName)
        {
            string directory = Path.Combine(Application.streamingAssetsPath, "MNN", "Models", modelName);
            string encoder = Directory.GetFiles(directory, "encoder-*.mnn", SearchOption.TopDirectoryOnly).Single();
            using (var graph = new MNNExpressGraph(encoder, 1, MNNBackendType.CPU))
            {
                var names = graph.GetVariableNames().OrderBy(name => name, StringComparer.Ordinal).ToArray();
                TestContext.WriteLine(modelName + " encoder variables:\n" + string.Join("\n", names));
                CollectionAssert.Contains(names, "x");
                CollectionAssert.Contains(names, "encoder_out");
                for (int layer = 0; layer < 5; ++layer)
                {
                    foreach (string state in new[]{"avg", "key", "val", "val2", "conv1", "conv2", "len"})
                    {
                        CollectionAssert.Contains(names, "cached_" + state + "_" + layer);
                        CollectionAssert.Contains(names, "new_cached_" + state + "_" + layer);
                    }
                }
            }
        }
    }
}
