using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class StyleTransferTests
    {
        private const int Plane = 224 * 224;
        private static MNNGenerationGraph.Output Output(float[] values) => new MNNGenerationGraph.Output(values, new[] {1, 3, 224, 224});

        [Test]
        public void DecodesPlanarRgbInTopLeftOrderAndClipsBeforeRounding()
        {
            var values = new float[3 * Plane];
            values[0] = 10.2f; values[Plane] = 20.8f; values[2 * Plane] = 30.5f;
            values[223] = 40; values[Plane + 223] = 50; values[2 * Plane + 223] = 60;
            values[Plane - 1] = float.MaxValue;
            values[2 * Plane - 1] = -float.MaxValue;
            values[3 * Plane - 1] = 254.8f;
            var image = MNNImageTask.DecodeStyle(Output(values));
            Assert.AreEqual(224, image.Width);
            Assert.AreEqual(224, image.Height);
            CollectionAssert.AreEqual(new byte[] {10, 21, 30}, new[] {image.Rgb[0], image.Rgb[1], image.Rgb[2]});
            CollectionAssert.AreEqual(new byte[] {40, 50, 60}, new[] {image.Rgb[669], image.Rgb[670], image.Rgb[671]});
            CollectionAssert.AreEqual(new byte[] {255, 0, 255}, new[] {image.Rgb[3 * Plane - 3], image.Rgb[3 * Plane - 2], image.Rgb[3 * Plane - 1]});
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsNonFinitePixels(float invalid)
        {
            var values = new float[3 * Plane];
            values[3 * Plane - 1] = invalid;
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodeStyle(Output(values)));
        }

        [Test]
        public void RejectsDifferentLayoutsAndIntegerOutputAndHonorsCancellation()
        {
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodeStyle(new MNNGenerationGraph.Output(new float[3 * Plane], new[] {1, 224, 224, 3})));
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodeStyle(new MNNGenerationGraph.Output(new float[3], new[] {1, 3, 1, 1})));
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodeStyle(new MNNGenerationGraph.Output(new int[3 * Plane], new[] {1, 3, 224, 224})));
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Assert.Throws<OperationCanceledException>(() => MNNImageTask.DecodeStyle(Output(new float[3 * Plane]), cancelled.Token));
            }
        }

        [Test]
        public void PreparedLocalProfileIsDiscoveredButAlteredWeightsCannotLoad()
        {
            string directory = TemporaryDirectory();
            try
            {
                File.WriteAllText(Path.Combine(directory, MNNImageTaskConfig.FileName), "{\"profile\":8}");
                var profile = MNNImageTaskConfig.Read(directory);
                Assert.AreEqual(MNNImageTaskProfile.CandyStyle, profile.profile);
                var source = MNNImageTaskProfiles.Get(profile.profile);
                Assert.False(MNNImageTaskProfiles.IsRepository(source.Repository), "A converted local profile must not advertise a nonexistent hosted MNN graph.");
                Assert.Throws<FileNotFoundException>(() => profile.Resolve(directory, true));
                File.WriteAllBytes(Path.Combine(directory, source.Graph), new byte[64]);
                Assert.Throws<InvalidDataException>(() => profile.Resolve(directory, true));
                var model = MNNStudioModel.Read(directory);
                Assert.AreEqual(MNNStudioTask.StyleTransfer, model.Task);
                Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
                Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.Chat).CanRun(model));
                File.WriteAllText(Path.Combine(directory, MNNImageTaskConfig.FileName), "{\"profile\":999}");
                Assert.Throws<InvalidDataException>(() => MNNImageTaskConfig.Read(directory));
            }
            finally { Directory.Delete(directory, true); }
        }

        [UnityTest]
        public IEnumerator DedicatedPageRunsImageWithoutPromptOrConversationHistory()
        {
            string directory = TemporaryDirectory();
            File.WriteAllText(Path.Combine(directory, MNNImageTaskConfig.FileName), "{\"profile\":8}");
            File.WriteAllBytes(Path.Combine(directory, "style-candy-8.mnn"), new byte[64]);
            var texture = new Texture2D(64, 48, TextureFormat.RGB24, false);
            string photo = Path.Combine(directory, "photo.png");
            File.WriteAllBytes(photo, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            var backend = new StyleBackend();
            var session = new MNNStudioSession(backend);
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            object Invoke(string name) => typeof(MNNChatStudio).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
            try
            {
                Field("_available").SetValue(window, new List<MNNStudioModel> {MNNStudioModel.Read(directory)});
                Field("_modelIndex").SetValue(window, 0);
                Field("_taskIndex").SetValue(window, (int)MNNStudioTask.StyleTransfer);
                Field("_query").SetValue(window, "");
                Field("_autoTitle").SetValue(window, false);
                Field("_session").SetValue(window, session);
                Assert.False((bool)Invoke("CanRunTask"), "A source photo is required.");
                Field("_imagePath").SetValue(window, photo);
                Assert.True((bool)Invoke("CanRunTask"), "A fixed style model must not require a prompt.");
                session.Completed += () => Invoke("OnCompleted");
                Invoke("RunTask");
                float deadline = Time.realtimeSinceStartup + 10;
                while (session.Busy && Time.realtimeSinceStartup < deadline)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }
                Assert.False(session.Busy);
                Assert.NotNull(backend.Request);
                Assert.AreEqual(64, backend.Request.VisionImage.Width);
                Assert.AreEqual(48, backend.Request.VisionImage.Height);
                Assert.IsNull(backend.Request.History);
                Assert.IsEmpty((List<MNNStudioMessage>)Field("_messages").GetValue(window));
                Assert.NotNull(((MNNStudioResult)Field("_retrieval").GetValue(window)).ImageTask.Image);
                window.position = new Rect(40, 40, 1000, 900);
                window.Show(); window.Focus();
                for (int frame = 0; frame < 5; ++frame) { window.Repaint(); yield return null; }
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                session.Dispose();
                Directory.Delete(directory, true);
            }
        }

        private static string TemporaryDirectory()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "StyleTransferTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private sealed class StyleBackend : IMNNStudioBackend
        {
            internal MNNStudioRequest Request;
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                Request = request;
                var image = MNNImageTask.DecodeStyle(Output(new float[3 * Plane]));
                return new MNNStudioResult {ImageTask = new MNNImageTaskResult {Image = image}, GeneratedImage = image};
            }
            public void Dispose() { }
        }
    }
}
