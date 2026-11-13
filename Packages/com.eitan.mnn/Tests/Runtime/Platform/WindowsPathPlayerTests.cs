#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("NativeInference")]
    public sealed class WindowsPathPlayerTests
    {
        [TestCase("affine.mnn")]
        [TestCase("model with spaces.mnn")]
        public void FileModelLoadsAndInfersWithoutManagedCodePages(string filename)
        {
            string directory = Path.Combine(Application.persistentDataPath, "mnn-windows-path-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, filename);
                File.WriteAllBytes(path, AffineFixture.Bytes);
                using (var model = MNNInterpreter.CreateFromFile(path))
                using (var session = model.CreateSession(new MNNSessionConfig { BackendType = MNNBackendType.CPU, ThreadCount = 1 }))
                {
                    session.GetInput().CopyFromArray(new[] { -1f, 0f, .5f, 5f });
                    session.Run();
                    CollectionAssert.AreEqual(new[] { -1f, 1f, 2f, 11f }, session.GetOutput().CopyToArray<float>());
                }
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
#endif
