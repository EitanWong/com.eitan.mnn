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
    public class SegmentationTests
    {
        private static MNNGenerationGraph.Output Output(float[] values) => new MNNGenerationGraph.Output(values, new[] {1, 257, 257, 21});

        [Test]
        public void ProjectsNhwcClassesWithNearestCentersAndPreservesImmutableResults()
        {
            var values = new float[257 * 257 * 21];
            values[6] = 100;
            values[(257 * 257 - 1) * 21 + 15] = 100;
            var result = MNNImageTask.DecodeSegmentation(Output(values), 514, 514);
            var mask = result.Segmentation;
            Assert.AreEqual(514, mask.Width); Assert.AreEqual(514, mask.Height);
            Assert.AreEqual(6, mask.ClassAt(0, 0));
            Assert.AreEqual(6, mask.ClassAt(1, 1));
            Assert.AreEqual(0, mask.ClassAt(2, 0));
            Assert.AreEqual(15, mask.ClassAt(513, 513));
            Assert.AreEqual("bus", mask.Labels[6]);
            Assert.AreEqual("person", mask.Labels[15]);
            Assert.AreEqual(1, mask.ConfidenceAt(0, 0));
            Assert.That(mask.ConfidenceAt(2, 0), Is.EqualTo(1f / 21).Within(1e-7));
            Assert.AreEqual(4, mask.ClassPixelCounts[6]);
            Assert.AreEqual(4, mask.ClassPixelCounts[15]);
            CollectionAssert.AreEqual(new byte[] {0, 128, 128}, new[] {result.Image.Rgb[0], result.Image.Rgb[1], result.Image.Rgb[2]});
            mask.ClassIds[0] = 20;
            mask.Confidence[0] = 0;
            mask.Labels[6] = "changed";
            mask.ClassPixelCounts[6] = 999;
            Assert.AreEqual(6, mask.ClassAt(0, 0));
            Assert.AreEqual(1, mask.ConfidenceAt(0, 0));
            Assert.AreEqual("bus", mask.Labels[6]);
            Assert.AreEqual(4, mask.ClassPixelCounts[6]);
            Assert.Throws<ArgumentOutOfRangeException>(() => mask.ClassAt(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => mask.ConfidenceAt(0, 514));
        }

        [Test]
        public void SoftmaxHandlesExtremeFiniteLogitsAndTiesWithoutOverflow()
        {
            var values = new float[257 * 257 * 21];
            values[1] = float.MaxValue;
            values[2] = float.MaxValue;
            values[3] = -float.MaxValue;
            var mask = MNNImageTask.DecodeSegmentation(Output(values), 257, 257).Segmentation;
            Assert.AreEqual(1, mask.ClassAt(0, 0), "A tie selects the first class, as the independent argmax does.");
            Assert.AreEqual(.5f, mask.ConfidenceAt(0, 0));
            Assert.AreEqual(0, mask.ClassAt(1, 0));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsInvalidNativeLogits(float invalid)
        {
            var values = new float[257 * 257 * 21];
            values[values.Length - 1] = invalid;
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodeSegmentation(Output(values), 3, 2));
        }

        [Test]
        public void RejectsOtherProtocolsAndInvalidSourceSizesAndCancellation()
        {
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodeSegmentation(new MNNGenerationGraph.Output(new float[257 * 257 * 21], new[] {1, 21, 257, 257}), 10, 10));
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodeSegmentation(new MNNGenerationGraph.Output(new int[257 * 257 * 21], new[] {1, 257, 257, 21}), 10, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNImageTask.DecodeSegmentation(Output(new float[257 * 257 * 21]), 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNImageTask.DecodeSegmentation(Output(new float[257 * 257 * 21]), int.MaxValue, int.MaxValue));
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Assert.Throws<OperationCanceledException>(() => MNNImageTask.DecodeSegmentation(Output(new float[257 * 257 * 21]), 1, 1, cancelled.Token));
            }
        }

        [UnityTest]
        public IEnumerator DedicatedPageRunsSourceImageAndDisplaysMaskWithoutPrompt()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "SegmentationTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, MNNImageTaskConfig.FileName), "{\"profile\":9}");
            File.WriteAllBytes(Path.Combine(directory, "deeplabv3.mnn"), new byte[64]);
            var texture = new Texture2D(64, 48, TextureFormat.RGB24, false);
            string photo = Path.Combine(directory, "photo.png");
            File.WriteAllBytes(photo, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            var backend = new SegmentationBackend();
            var session = new MNNStudioSession(backend);
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            object Invoke(string name) => typeof(MNNChatStudio).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
            try
            {
                var model = MNNStudioModel.Read(directory);
                Assert.AreEqual(MNNStudioTask.Segmentation, model.Task);
                Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
                Assert.Throws<InvalidDataException>(() => MNNImageTaskConfig.Read(directory).Resolve(directory, true));
                Assert.False(MNNImageTaskProfiles.IsRepository(MNNImageTaskProfiles.Get(MNNImageTaskProfile.DeepLabV3).Repository));
                Field("_available").SetValue(window, new List<MNNStudioModel> {model});
                Field("_modelIndex").SetValue(window, 0);
                Field("_taskIndex").SetValue(window, (int)MNNStudioTask.Segmentation);
                Field("_query").SetValue(window, "");
                Field("_autoTitle").SetValue(window, false);
                Field("_session").SetValue(window, session);
                Assert.False((bool)Invoke("CanRunTask"));
                Field("_imagePath").SetValue(window, photo);
                Assert.True((bool)Invoke("CanRunTask"));
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
                Assert.IsNull(backend.Request.History);
                Assert.IsEmpty((List<MNNStudioMessage>)Field("_messages").GetValue(window));
                Assert.NotNull(((MNNStudioResult)Field("_retrieval").GetValue(window)).ImageTask.Segmentation);
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

        private sealed class SegmentationBackend : IMNNStudioBackend
        {
            internal MNNStudioRequest Request;
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                Request = request;
                var result = MNNImageTask.DecodeSegmentation(Output(new float[257 * 257 * 21]), request.VisionImage.Width, request.VisionImage.Height);
                return new MNNStudioResult {ImageTask = result, GeneratedImage = result.Image};
            }
            public void Dispose() { }
        }
    }
}
