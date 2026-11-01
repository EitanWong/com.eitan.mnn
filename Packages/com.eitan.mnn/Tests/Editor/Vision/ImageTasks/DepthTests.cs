using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;
using System.IO;
using System.Linq;
using System.Threading;
using System.Text;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class DepthTests
    {
        [TestCase(810, 1080, 518, 686)]
        [TestCase(1080, 810, 686, 518)]
        [TestCase(1080, 1080, 518, 518)]
        [TestCase(1920, 1080, 924, 518)]
        [TestCase(1, 1, 518, 518)]
        public void DptMinimalResizePreservesAspectAndRoundsToPatches(int w, int h, int rw, int rh)
        {
            MNNImageTask.DepthGeometry(w, h, out int actualWidth, out int actualHeight);
            CollectionAssert.AreEqual(new[] {rw, rh}, new[] {actualWidth, actualHeight});
        }
        [TestCase(0, 1)]
        [TestCase(1, 0)]
        [TestCase(1, 518)]
        [TestCase(10000, 10000)]
        [TestCase(10000, 100)]
        public void DegenerateOrExcessiveAttentionGeometryIsRejectedBeforeAllocation(int w, int h)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNImageTask.DepthGeometry(w, h, out _, out _));
        }
        [Test]
        public void SourceSamplingPreviewAndPfmRetainRawInverseDepthAndOrientation()
        {
            float[] values = Enumerable.Range(0, 518 * 518).Select(i => (float)i).ToArray();
            var result = MNNImageTask.DecodeDepth(new MNNGenerationGraph.Output(values, new[] {1, 518, 518}), 14, 14);
            var depth = result.Depth;
            Assert.AreEqual(0, depth.Minimum); Assert.AreEqual(values.Length - 1, depth.Maximum);
            Assert.AreEqual(18 * 518 + 18, depth.SampleAt(0, 0));
            Assert.AreEqual(499 * 518 + 499, depth.SampleAt(13, 13));
            Assert.Less(result.Image.Rgb[0], result.Image.Rgb.Last());
            var copy = depth.RelativeInverseDepth; copy[0] = -1; Assert.AreEqual(0, depth.RelativeInverseDepth[0]);
            Assert.Throws<ArgumentOutOfRangeException>(() => depth.SampleAt(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => depth.SampleAt(0, 14));
            byte[] header = Encoding.ASCII.GetBytes("Pf\n518 518\n-1.0\n"), pfm = MNNChatStudio.EncodeDepthPfm(depth);
            CollectionAssert.AreEqual(header, pfm.Take(header.Length));
            Assert.AreEqual(517 * 518, BitConverter.ToSingle(pfm, header.Length));
            Assert.AreEqual(0, BitConverter.ToSingle(pfm, header.Length + 517 * 518 * 4));
            Assert.AreEqual(values.Length * 4 + header.Length, pfm.Length);
        }
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void RejectsMalformedDepth(float value)
        {
            var values = new float[518 * 518]; values[values.Length - 1] = value;
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodeDepth(new MNNGenerationGraph.Output(values, new[] {1, 518, 518}), 14, 14));
        }
        [Test]
        public void RejectsWrongShapeOrTypeAndHonorsCancellation()
        {
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodeDepth(new MNNGenerationGraph.Output(new float[4], new[] {1, 2, 2}), 14, 14));
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodeDepth(new MNNGenerationGraph.Output(new int[518 * 518], new[] {1, 518, 518}), 14, 14));
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Assert.Throws<OperationCanceledException>(() => MNNImageTask.DepthTensor(new MNNRgbImage(new byte[3], 1, 1), cancelled.Token));
            }
        }
        [UnityTest]
        public IEnumerator DedicatedPageRunsImageWithoutPromptAndDisplaysDepth()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "DepthTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, MNNImageTaskConfig.FileName), "{\"profile\":11}");
            File.WriteAllBytes(Path.Combine(directory, "depth-anything-v2-small.mnn"), new byte[64]);
            var texture = new Texture2D(64, 48, TextureFormat.RGB24, false);
            string photo = Path.Combine(directory, "photo.png");
            File.WriteAllBytes(photo, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            var backend = new DepthBackend();
            var session = new MNNStudioSession(backend);
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            object Invoke(string name) => typeof(MNNChatStudio).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
            try
            {
                var model = MNNStudioModel.Read(directory);
                Assert.AreEqual(MNNStudioTask.DepthEstimation, model.Task);
                Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
                Assert.Throws<InvalidDataException>(() => MNNImageTaskConfig.Read(directory).Resolve(directory, true));
                Assert.False(MNNImageTaskProfiles.IsRepository(MNNImageTaskProfiles.Get(MNNImageTaskProfile.DepthAnythingV2Small).Repository));
                Field("_available").SetValue(window, new List<MNNStudioModel> {model});
                Field("_modelIndex").SetValue(window, 0);
                Field("_taskIndex").SetValue(window, (int)MNNStudioTask.DepthEstimation);
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
                Assert.NotNull(((MNNStudioResult)Field("_retrieval").GetValue(window)).ImageTask.Depth);
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

        private sealed class DepthBackend : IMNNStudioBackend
        {
            internal MNNStudioRequest Request;
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                Request = request;
                MNNImageTask.DepthGeometry(request.VisionImage.Width, request.VisionImage.Height, out int width, out int height);
                var output = new MNNGenerationGraph.Output(Enumerable.Repeat(1f, width * height).ToArray(), new[] {1, height, width});
                var result = MNNImageTask.DecodeDepth(output, request.VisionImage.Width, request.VisionImage.Height);
                return new MNNStudioResult {ImageTask = result, GeneratedImage = result.Image};
            }
            public void Dispose() { }
        }
    }
}
