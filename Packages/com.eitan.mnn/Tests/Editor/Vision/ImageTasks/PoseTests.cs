using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class PoseTests
    {
        private static MNNGenerationGraph.Output Output(float[] values) => new MNNGenerationGraph.Output(values, new[] {1, 1, 17, 3});
        private static float[] ValidValues()
        {
            var values = new float[51];
            for (int i = 0; i < 17; ++i) { values[i * 3] = .5f; values[i * 3 + 1] = .5f; values[i * 3 + 2] = .8f; }
            return values;
        }

        [TestCase(205, 550, 72, 192, 60, 0)]
        [TestCase(165, 500, 63, 192, 64, 0)]
        [TestCase(500, 165, 192, 63, 0, 64)]
        [TestCase(1, 1000, 1, 192, 95, 0)]
        [TestCase(193, 192, 192, 191, 0, 0)]
        public void AspectRatioRoundingAndPaddingAreExplicit(int w, int h, int rw, int rh, int left, int top)
        {
            MNNImageTask.PoseGeometry(w, h, out int actualWidth, out int actualHeight, out int actualLeft, out int actualTop);
            CollectionAssert.AreEqual(new[] {rw, rh, left, top}, new[] {actualWidth, actualHeight, actualLeft, actualTop});
        }

        [Test]
        public void IntegerNhwcInputPreservesRgbAndBlackLetterbox()
        {
            int[] values = MNNImageTask.PoseTensor(new MNNRgbImage(new byte[] {12, 128, 255, 45, 67, 89}, 1, 2));
            Assert.AreEqual(192 * 192 * 3, values.Length);
            CollectionAssert.AreEqual(new[] {0, 0, 0}, values.Take(3));
            CollectionAssert.AreEqual(new[] {12, 128, 255}, values.Skip(48 * 3).Take(3));
            CollectionAssert.AreEqual(new[] {45, 67, 89}, values.Skip((191 * 192 + 143) * 3).Take(3));
            CollectionAssert.AreEqual(new[] {0, 0, 0}, values.Skip((191 * 192 + 144) * 3).Take(3));
            Assert.Throws<ArgumentNullException>(() => MNNImageTask.PoseTensor(null));
        }

        [Test]
        public void DecoderRetainsCocoOrderAndUnclippedPaddingCoordinates()
        {
            var values = ValidValues();
            values[0] = 0; values[1] = 0; values[2] = 0;
            values[48] = 1; values[49] = 1;
            var pose = MNNImageTask.DecodePose(Output(values), 165, 500);
            Assert.AreEqual(17, pose.Keypoints.Count);
            var nose = pose.GetKeypoint(MNNPoseJoint.Nose);
            Assert.AreEqual(0, nose.Confidence); Assert.False(nose.IsInBounds);
            Assert.That(nose.X, Is.EqualTo(-64f * 165 / 63).Within(.001));
            var center = pose.GetKeypoint(MNNPoseJoint.LeftShoulder);
            Assert.That(center.X, Is.EqualTo(32f * 165 / 63).Within(.001));
            Assert.AreEqual(250, center.Y); Assert.True(center.IsInBounds);
            Assert.AreEqual(MNNPoseJoint.RightAnkle, pose.Keypoints[16].Joint);
            Assert.False(pose.Keypoints[16].IsInBounds);
            values[17] = 0;
            Assert.AreEqual(.8f, center.Confidence);
            Assert.Throws<NotSupportedException>(() => ((IList<MNNPoseKeypoint>)pose.Keypoints)[0] = center);
            Assert.Throws<ArgumentOutOfRangeException>(() => pose.GetKeypoint((MNNPoseJoint)17));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsInvalidCoordinatesAndScores(float invalid)
        {
            for (int component = 0; component < 3; ++component)
            {
                var values = ValidValues(); values[48 + component] = invalid;
                Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodePose(Output(values), 20, 10));
            }
        }

        [TestCase(-.001f)]
        [TestCase(1.001f)]
        public void RejectsOutOfRangeScoresButRetainsLearnedCoordinatesOutsideCanvas(float value)
        {
            var values = ValidValues(); values[50] = value;
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodePose(Output(values), 192, 192));
            values = ValidValues(); values[0] = value; values[1] = value;
            var point = MNNImageTask.DecodePose(Output(values), 192, 192).Keypoints[0];
            Assert.That(point.X, Is.EqualTo(value * 192).Within(.0001));
            Assert.False(point.IsInBounds);
            values[0] = float.MaxValue;
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodePose(Output(values), 192, 192));
        }

        [Test]
        public void RejectsOtherShapesAndTypesAndOversizedSourceAndHonorsCancellation()
        {
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodePose(new MNNGenerationGraph.Output(ValidValues(), new[] {1, 17, 3}), 10, 10));
            Assert.Throws<InvalidDataException>(() => MNNImageTask.DecodePose(new MNNGenerationGraph.Output(new int[51], new[] {1, 1, 17, 3}), 10, 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNImageTask.DecodePose(Output(ValidValues()), 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNImageTask.DecodePose(Output(ValidValues()), int.MaxValue, int.MaxValue));
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                Assert.Throws<OperationCanceledException>(() => MNNImageTask.DecodePose(Output(ValidValues()), 1, 1, cancelled.Token));
                Assert.Throws<OperationCanceledException>(() => MNNImageTask.PoseTensor(new MNNRgbImage(new byte[3], 1, 1), cancelled.Token));
            }
        }

        [Test]
        public void OverlayFiltersLowScoresAndPaddingWithoutMutatingSource()
        {
            var source = new MNNGeneratedImage(new byte[30 * 20 * 3], 30, 20, "source", 0);
            var result = new MNNImageTaskResult {Pose = MNNImageTask.DecodePose(Output(ValidValues()), 30, 20), Image = source};
            Assert.True(MNNChatStudio.RenderPoseOverlay(result, .2f).Rgb.Any(v => v != 0));
            Assert.True(MNNChatStudio.RenderPoseOverlay(result, .9f).Rgb.All(v => v == 0));
            Assert.True(source.Rgb.All(v => v == 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNChatStudio.RenderPoseOverlay(result, float.NaN));
            var values = ValidValues();
            for (int i = 0; i < 17; ++i) values[i * 3] = 0;
            result.Pose = MNNImageTask.DecodePose(Output(values), 30, 20);
            Assert.True(MNNChatStudio.RenderPoseOverlay(result, .2f).Rgb.All(v => v == 0), "Padding joints must not be clamped into the photo.");
        }

        [UnityTest]
        public IEnumerator DedicatedPageRunsImageWithoutPromptAndDisplaysSkeleton()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "PoseTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, MNNImageTaskConfig.FileName), "{\"profile\":10}");
            File.WriteAllBytes(Path.Combine(directory, "movenet-lightning.mnn"), new byte[64]);
            var texture = new Texture2D(64, 48, TextureFormat.RGB24, false);
            string photo = Path.Combine(directory, "photo.png");
            File.WriteAllBytes(photo, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            var backend = new PoseBackend();
            var session = new MNNStudioSession(backend);
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            object Invoke(string name) => typeof(MNNChatStudio).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
            try
            {
                var model = MNNStudioModel.Read(directory);
                Assert.AreEqual(MNNStudioTask.Pose, model.Task);
                Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
                Assert.Throws<InvalidDataException>(() => MNNImageTaskConfig.Read(directory).Resolve(directory, true));
                Assert.False(MNNImageTaskProfiles.IsRepository(MNNImageTaskProfiles.Get(MNNImageTaskProfile.MoveNetLightning).Repository));
                Field("_available").SetValue(window, new List<MNNStudioModel> {model});
                Field("_modelIndex").SetValue(window, 0);
                Field("_taskIndex").SetValue(window, (int)MNNStudioTask.Pose);
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
                Assert.NotNull(((MNNStudioResult)Field("_retrieval").GetValue(window)).ImageTask.Pose);
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

        private sealed class PoseBackend : IMNNStudioBackend
        {
            internal MNNStudioRequest Request;
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                Request = request;
                var result = new MNNImageTaskResult {Pose = MNNImageTask.DecodePose(Output(ValidValues()), request.VisionImage.Width, request.VisionImage.Height), Image = new MNNGeneratedImage(request.VisionImage.ToArray(), request.VisionImage.Width, request.VisionImage.Height, "pose", 0)};
                return new MNNStudioResult {ImageTask = result, GeneratedImage = result.Image};
            }
            public void Dispose() { }
        }
    }
}
