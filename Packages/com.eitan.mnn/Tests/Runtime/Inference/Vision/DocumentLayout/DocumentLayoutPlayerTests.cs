using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Category("DocumentLayoutPlayer")]
    public class DocumentLayoutPlayerTests
    {
        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void RealPageRegionsReadingOrderMemoryAndSpeed(MNNBackendType requested)
        {
            string root = Environment.GetEnvironmentVariable("MNN_PLAYER_DOCUMENT_LAYOUT_ROOT");
            if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(Application.persistentDataPath, "document-layout");
            if (!File.Exists(Path.Combine(root, "PP-DocLayoutV3.mnn")))
                Assert.Ignore("Stage pinned PP-DocLayoutV3.mnn, layout-meta.json and layout.jpg in persistentDataPath/document-layout or provide --document-layout-root.");
            var image = VisionPlayerData.Read(Path.Combine(root, "layout.jpg"));
            var before = PlayerMeasurements.Read();
            var timer = Stopwatch.StartNew();
            using (var model = MNNDocumentLayout.Load(root, backendType: requested))
            {
                double load = timer.Elapsed.TotalMilliseconds;
                var loaded = PlayerMeasurements.Read();
                timer.Restart(); var result = model.Analyze(image); double first = timer.Elapsed.TotalMilliseconds;
                timer.Restart(); var repeat = model.Analyze(image); double warm = timer.Elapsed.TotalMilliseconds;
                var after = PlayerMeasurements.Read();
                var regions = result.Regions;
                Assert.AreEqual(13, regions.Length);
                Assert.AreEqual(2, regions.Count(region => region.Detection.Label == "table"));
                Assert.AreEqual(2, regions.Count(region => region.Detection.Label == "figure_title"));
                Assert.AreEqual(4, regions.Count(region => region.Detection.Label == "paragraph_title"));
                Assert.AreEqual(5, regions.Count(region => region.Detection.Label == "text"));
                CollectionAssert.AreEqual(new[] {"figure_title", "table", "paragraph_title", "text", "paragraph_title", "text",
                    "figure_title", "table", "text", "paragraph_title", "text", "paragraph_title", "text"}, regions.Select(region => region.Detection.Label));
                CollectionAssert.AreEqual(regions.Select(region => region.ReadingOrder).OrderBy(order => order), regions.Select(region => region.ReadingOrder));
                var tables = regions.Where(region => region.Detection.Label == "table").Select(region => region.Detection).ToArray();
                Assert.That(tables[0].X, Is.EqualTo(75).Within(12));
                Assert.That(tables[0].Y, Is.EqualTo(108).Within(12));
                Assert.That(tables[0].Width, Is.EqualTo(248).Within(16));
                Assert.That(tables[0].Height, Is.EqualTo(192).Within(16));
                Assert.That(tables[1].X, Is.EqualTo(438).Within(12));
                Assert.That(tables[1].Y, Is.EqualTo(108).Within(12));
                Assert.That(tables[1].Width, Is.EqualTo(226).Within(16));
                Assert.That(tables[1].Height, Is.EqualTo(207).Within(16));
                for (int i = 0; i < regions.Length; ++i)
                {
                    Assert.AreEqual(regions[i].ReadingOrder, repeat.Regions[i].ReadingOrder);
                    Assert.AreEqual(regions[i].Detection.Label, repeat.Regions[i].Detection.Label);
                    Assert.That(repeat.Regions[i].Detection.X, Is.EqualTo(regions[i].Detection.X).Within(.5));
                }
                using (var cancelled = new CancellationTokenSource())
                {
                    cancelled.Cancel();
                    Assert.Throws<OperationCanceledException>(() => model.Analyze(image, cancellationToken: cancelled.Token));
                }
                string measurement = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"device\":\"{0}\",\"requested\":\"{1}\",\"actual\":\"{2}\",\"load_ms\":{3},\"first_ms\":{4},\"warm_ms\":{5},\"rss_before\":{6},\"rss_loaded\":{7},\"rss_after\":{8},\"peak_rss\":{9},\"physical_footprint\":{10},\"regions\":{11}}}",
                    SystemInfo.deviceModel, requested, model.Backend, load, first, warm, before.residentBytes, loaded.residentBytes,
                    after.residentBytes, after.peakResidentBytes, after.physicalFootprintBytes, regions.Length);
                File.WriteAllText(Path.Combine(root, requested + "-measurements.json"), measurement);
                TestContext.WriteLine(measurement);
                model.Dispose(); model.Dispose();
                Assert.Throws<ObjectDisposedException>(() => model.Analyze(image));
            }
        }
    }
}
