using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public class DocumentLayoutTests
    {
        private static MNNGenerationGraph.Output Rows(params float[] values) => new MNNGenerationGraph.Output(values, new[] {values.Length / 7, 7});

        [Test]
        public void MapsStretchedCoordinatesClipsEdgesFiltersAndSortsReadingOrder()
        {
            var output = Rows(21,.9f,400,80,820,400,30, 17,.8f,-10,40,200,80,4, 22,.2f,0,0,20,20,2);
            var result = MNNDocumentLayout.Decode(output, 1600, 400, .5f);
            var regions = result.Regions;
            CollectionAssert.AreEqual(new[] {4,30}, regions.Select(region => region.ReadingOrder));
            Assert.AreEqual("paragraph_title", regions[0].Detection.Label);
            Assert.AreEqual(0, regions[0].Detection.X);
            Assert.AreEqual(20, regions[0].Detection.Y);
            Assert.AreEqual(400, regions[0].Detection.Width);
            Assert.AreEqual("table", regions[1].Detection.Label);
            Assert.AreEqual(800, regions[1].Detection.X);
            Assert.AreEqual(800, regions[1].Detection.Width);
            Assert.AreEqual(160, regions[1].Detection.Height);
            regions[0] = null;
            Assert.NotNull(result.Regions[0], "Callers cannot replace stored regions.");
        }

        [TestCase(0, 25f)]
        [TestCase(0, 1.5f)]
        [TestCase(1, float.NaN)]
        [TestCase(1, 1.1f)]
        [TestCase(4, float.PositiveInfinity)]
        [TestCase(4, 0f)]
        [TestCase(6, -1f)]
        [TestCase(6, 300f)]
        [TestCase(6, 1.5f)]
        public void RejectsMalformedNativeRows(int column, float value)
        {
            var row = new[] {21f,.9f,0,0,100,100,2};
            row[column] = value;
            Assert.Throws<InvalidDataException>(() => MNNDocumentLayout.Decode(Rows(row), 800,800,.5f));
        }

        [Test]
        public void RejectsWrongOutputProtocolAndInvalidThreshold()
        {
            Assert.Throws<NotSupportedException>(() => MNNDocumentLayout.Load("unused", backendType: MNNBackendType.Metal));
            Assert.Throws<InvalidDataException>(() => MNNDocumentLayout.Decode(new MNNGenerationGraph.Output(new float[6], new[] {1,6}), 800,800,.5f));
            Assert.Throws<InvalidDataException>(() => MNNDocumentLayout.Decode(new MNNGenerationGraph.Output(new int[7], new[] {1,7}), 800,800,.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNDocumentLayout.Decode(Rows(), 800,800,float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNDocumentLayout.Decode(Rows(), 800,800,0));
            Assert.IsEmpty(MNNDocumentLayout.Decode(Rows(),800,800,.5f).Regions);
        }

        [Test]
        public void StudioRequiresBothPinnedFilesAndRuntimeRejectsAlteredAssets()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "DocumentLayoutUnit", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, MNNDocumentLayoutModel.Metadata), "{}");
                Assert.False(MNNDocumentLayoutModel.IsComplete(directory));
                for (int i = 0; i < MNNDocumentLayoutModel.Files.Length; ++i)
                    using (var stream = File.Create(Path.Combine(directory, MNNDocumentLayoutModel.Files[i]))) stream.SetLength(MNNDocumentLayoutModel.Sizes[i]);
                var model = MNNStudioModel.Read(directory);
                Assert.AreEqual(MNNStudioTask.DocumentLayout, model.Task);
                Assert.True(MNNStudioTasks.Get((int)model.Task).CanRun(model));
                Assert.Throws<InvalidDataException>(() => MNNDocumentLayoutModel.Verify(directory));
                Assert.False(MNNDocumentLayoutModel.IsRepository("dr3334/PP-DocLayoutV3-mnn"), "Different exports cannot inherit this decoder contract.");
            }
            finally { Directory.Delete(directory, true); }
        }

        [UnityTest]
        public IEnumerator DedicatedPageRendersAndForwardsImageAndConfidenceWithoutChatHistory()
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "DocumentLayoutStudio", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            for (int i = 0; i < MNNDocumentLayoutModel.Files.Length; ++i)
                using (var stream = File.Create(Path.Combine(directory, MNNDocumentLayoutModel.Files[i]))) stream.SetLength(MNNDocumentLayoutModel.Sizes[i]);
            var texture = new Texture2D(64,64,TextureFormat.RGB24,false);
            string page = Path.Combine(directory, "page.png");
            File.WriteAllBytes(page, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            var backend = new LayoutBackend();
            var session = new MNNStudioSession(backend);
            var window = ScriptableObject.CreateInstance<MNNChatStudio>();
            FieldInfo Field(string name) => typeof(MNNChatStudio).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            object Invoke(string name) => typeof(MNNChatStudio).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(window, null);
            try
            {
                Field("_available").SetValue(window, new List<MNNStudioModel> {MNNStudioModel.Read(directory)});
                Field("_modelIndex").SetValue(window, 0);
                Field("_taskIndex").SetValue(window, (int)MNNStudioTask.DocumentLayout);
                Field("_imagePath").SetValue(window, page);
                Field("_layoutScoreThreshold").SetValue(window, .7f);
                Field("_query").SetValue(window, "");
                Field("_autoTitle").SetValue(window, false);
                Field("_session").SetValue(window, session);
                session.Completed += () => Invoke("OnCompleted");
                Assert.True((bool)Invoke("CanRunTask"), "Page analysis must not require a text prompt.");
                Invoke("RunTask");
                for (int frame = 0; frame < 60 && session.Busy; ++frame)
                {
                    MNNStudioJobs.Update();
                    yield return null;
                }
                Assert.False(session.Busy);
                Assert.NotNull(backend.Request);
                Assert.AreEqual(.7f, backend.Request.LayoutScoreThreshold);
                Assert.AreEqual(64, backend.Request.VisionImage.Width);
                Assert.IsNull(backend.Request.History);
                Assert.IsEmpty((List<MNNStudioMessage>)Field("_messages").GetValue(window));
                Assert.NotNull(((MNNStudioResult)Field("_retrieval").GetValue(window)).DocumentLayout);
                window.position = new Rect(40,40,1000,900);
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

        private sealed class LayoutBackend : IMNNStudioBackend
        {
            internal MNNStudioRequest Request;
            public MNNStudioResult Run(MNNStudioRequest request)
            {
                Request = request;
                var result = MNNDocumentLayout.Decode(Rows(21,.9f,40,40,760,760,5),64,64,.7f);
                return new MNNStudioResult {Text = "table", DocumentLayout = result,
                    GeneratedImage = MNNStudioImage.DrawDetections(request.VisionImage, result.Regions.Select(region => region.Detection).ToArray())};
            }
            public void Dispose() { }
        }
    }
}
