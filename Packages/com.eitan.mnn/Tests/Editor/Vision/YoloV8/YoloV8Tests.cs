using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class YoloV8Tests
    {
        [Test]
        public void ParallelLetterboxIsBitIdenticalToSerialWithUnevenPadding()
        {
            var random = new System.Random(7);
            var pixels = new byte[317*581*3];
            random.NextBytes(pixels);
            var image = new MNNRgbImage(pixels,317,581);
            var serial = MNNYoloV8.Letterbox(image,640,640,out float scale,out int left,out int top);
            var parallel = MNNYoloV8.Letterbox(image,640,640,out float otherScale,out int otherLeft,out int otherTop,threadCount:4);
            CollectionAssert.AreEqual(serial,parallel);
            Assert.AreEqual(scale,otherScale);
            Assert.AreEqual(left,otherLeft);
            Assert.AreEqual(top,otherTop);
            var workspace = new float[serial.Length];
            Array.Fill(workspace,float.NaN);
            Assert.AreSame(workspace,MNNYoloV8.Letterbox(image,640,640,out _,out _,out _,threadCount:4,destinationBuffer:workspace));
            CollectionAssert.AreEqual(serial,workspace);
        }

        [Test]
        public void CenterSizeLayoutsSuppressSameClassAndUndoLetterbox()
        {
            var config = new MNNYoloV8Config {labels = new[] {"person", "bus"}};
            // Two overlapping people and an equally positioned bus: only the second person is suppressed.
            float[] rows = {100, 80, 40, 20, .9f, .1f, 101, 80, 40, 20, .8f, .1f, 100, 80, 40, 20, .1f, .85f};
            float[] columns = Enumerable.Range(0, 6).SelectMany(c => Enumerable.Range(0, 3).Select(a => rows[a * 6 + c])).ToArray();
            foreach (bool transposed in new[] {false, true})
            {
                var result = MNNYoloV8.Decode(transposed ? rows : columns, transposed ? new[] {1, 3, 6} : new[] {1, 6, 3},
                    config, 200, 200, 2, 20, 40, .25f, .45f, 300);
                CollectionAssert.AreEqual(new[] {"person", "bus"}, result.Select(box => box.Label));
                Assert.AreEqual(30, result[0].X);
                Assert.AreEqual(15, result[0].Y);
                Assert.AreEqual(20, result[0].Width);
                Assert.AreEqual(10, result[0].Height);
            }
            rows[5] = float.NaN;
            Assert.Throws<InvalidDataException>(() => MNNYoloV8.Decode(rows, new[] {1, 3, 6}, config, 200, 200, 2, 20, 40, .25f, .45f, 300));
            rows[5] = .1f;
            rows[0] = float.PositiveInfinity;
            Assert.Throws<InvalidDataException>(() => MNNYoloV8.Decode(rows, new[] {1, 3, 6}, config, 200, 200, 2, 20, 40, .25f, .45f, 300));
        }

        [Test]
        public void DistanceHeadsUseTheCorrectStrideLevelAndClipToSource()
        {
            var config = new MNNYoloV8Config {width = 32, height = 32, labels = new[] {"object"}, boxEncoding = MNNYoloV8BoxEncoding.AnchorDistances};
            var values = new float[21 * 5];
            // Last anchor belongs to the stride-32 feature map, with center (16,16).
            values[20] = 1;
            values[21 + 20] = .25f;
            values[42 + 20] = .5f;
            values[63 + 20] = .25f;
            values[84 + 20] = .9f;
            var result = MNNYoloV8.Decode(values, new[] {1, 5, 21}, config, 32, 32, 1, 0, 0, .25f, .45f, 300);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual(0, result[0].X);
            Assert.AreEqual(8, result[0].Y);
            Assert.AreEqual(32, result[0].Width);
            Assert.AreEqual(16, result[0].Height);
            Assert.Throws<InvalidDataException>(() => MNNYoloV8.Decode(new float[100], new[] {1, 5, 20}, config, 32, 32, 1, 0, 0, .25f, .45f, 300));
        }

        [Test]
        public void RgbOwnershipAndLetterboxPreserveChannelsAndTopLeftRows()
        {
            byte[] pixels = {255, 0, 0, 0, 0, 255};
            var image = new MNNRgbImage(pixels, 1, 2);
            pixels[0] = 0;
            image.ToArray()[0] = 0;
            var input = MNNYoloV8.Letterbox(image, 2, 2, out float scale, out int left, out int top);
            Assert.AreEqual(1, scale);
            Assert.AreEqual(0, left);
            Assert.AreEqual(0, top);
            Assert.AreEqual(1, input[0]);
            Assert.AreEqual(0, input[2]);
            Assert.AreEqual(1, input[10]);
            Assert.AreEqual(114f / 255, input[1]);
            Assert.Throws<ArgumentException>(() => new MNNRgbImage(pixels, int.MaxValue, int.MaxValue));
        }

        [Test]
        public void ExplicitProfileRoutesRenamedModelsAndRejectsMalformedContracts()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~/YoloV8Tests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, "yolov8n.mnn"), "placeholder; never executed");
                var config = new MNNYoloV8Config {labels = new[] {"object"}};
                File.WriteAllText(Path.Combine(directory, MNNYoloV8Config.FileName), JsonUtility.ToJson(config));
                var model = MNNStudioModel.Read(directory);
                Assert.AreEqual(MNNStudioTask.ObjectDetection, model.Task);
                Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
                config.graph = "../escape.mnn";
                Assert.Throws<FileNotFoundException>(() => config.ResolveGraph(directory));
                config.graph = "yolov8n.mnn";
                config.boxEncoding = MNNYoloV8BoxEncoding.AnchorDistances;
                config.strides = new[] {16, 8};
                Assert.Throws<InvalidDataException>(() => config.ResolveGraph(directory));
                File.WriteAllText(Path.Combine(directory, MNNYoloV8Config.FileName), JsonUtility.ToJson(config));
                Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model), "The existing discovery snapshot is immutable.");
                Assert.False(MNNStudioTasks.Get((int)model.Task).CanRun(MNNStudioModel.Read(directory)));
                Assert.Throws<InvalidDataException>(() => MNNYoloV8Model.Prepare(directory));
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
