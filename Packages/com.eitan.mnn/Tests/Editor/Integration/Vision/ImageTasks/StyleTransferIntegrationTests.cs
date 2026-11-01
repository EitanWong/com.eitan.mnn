using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    [Explicit("Requires the pinned prepared Candy graph and existing independent ONNX reference; never downloads.")]
    public class StyleTransferIntegrationTests
    {
        [Test]
        public void MetalHighPrecisionMatchesIndependentOnnxReference()
        {
            string models = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            string artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT");
            string reference = Path.GetFullPath(Path.Combine(models, "../../../../TestArtifacts~/TaskIntegration/StyleTransfer"));
            float[] input = Read(Path.Combine(reference, "reference-input.f32"));
            float[] expected = Read(Path.Combine(reference, "reference-output.f32"));
            using (var interpreter = MNNInterpreter.CreateFromFile(Path.Combine(models, "DefTruth--FastStyleTransfer-Candy/style-candy-8.mnn")))
            using (var session = interpreter.CreateSession(new MNNSessionConfig {BackendType = MNNBackendType.Metal, PrecisionMode = MNNPrecisionMode.High, ThreadCount = 4}))
            {
                Assert.AreEqual(MNNBackendType.Metal, session.ActualBackend);
                session.GetInput("input1").CopyFromArray(input);
                session.Run();
                float[] output = session.GetOutput("output1").CopyToArray<float>();
                double max = expected.Select((value, index) => Math.Abs(value - output[index])).Max();
                double mae = expected.Select((value, index) => Math.Abs(value - output[index])).Average();
                File.WriteAllText(Path.Combine(artifacts, "MetalHigh-reference.json"), string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"actual\":\"Metal\",\"precision\":\"High\",\"max_float_error\":{0},\"mean_float_error\":{1}}}", max, mae));
                Assert.Less(max, .25);
                Assert.Less(mae, .01);
            }
        }

        [TestCase(MNNBackendType.CPU)]
        [TestCase(MNNBackendType.Auto)]
        public void ActualMnnPixelsMatchIndependentOnnxReference(MNNBackendType backend)
        {
            string models = Environment.GetEnvironmentVariable("MNN_TEST_MODEL_ROOT");
            string artifacts = Environment.GetEnvironmentVariable("MNN_TEST_ARTIFACT_ROOT");
            Assert.NotNull(models); Assert.NotNull(artifacts);
            string reference = Path.GetFullPath(Path.Combine(models, "../../../../TestArtifacts~/TaskIntegration/StyleTransfer"));
            string directory = Path.Combine(models, "DefTruth--FastStyleTransfer-Candy");
            var image = MNNStudioImage.ReadRgb(Path.Combine(reference, "style.jpg"));
            var resized = MNNImageSampling.ResizeBilinear(image,224,224,CancellationToken.None);
            float[] input = MNNImageSampling.Tensor(resized,224,224,0,1);
            float[] expectedInput = Read(Path.Combine(reference,"reference-input.f32"));
            CollectionAssert.AreEqual(expectedInput,input,"Managed antialiased resize must match independent Pillow bytes.");
            float[] expected = Read(Path.Combine(reference,"reference-output.f32"));
            var source = MNNImageTaskProfiles.Get(MNNImageTaskProfile.CandyStyle);
            using (var graph = new MNNGenerationGraph(Path.Combine(directory,source.Graph),4,backendType:backend,precisionMode:MNNPrecisionMode.High))
            {
                var actual = graph.RunOutputs(new[] {"output1"},new MNNGenerationGraph.Input("input1",input,1,3,224,224))[0];
                MNNImageTask.RequireShape(actual,1,3,224,224);
                double max = expected.Select((value,index) => Math.Abs(value-actual.Values[index])).Max();
                double mae = expected.Select((value,index) => Math.Abs(value-actual.Values[index])).Average();
                File.WriteAllText(Path.Combine(artifacts,backend+"-reference.json"),string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"requested\":\"{0}\",\"actual\":\"{1}\",\"max_float_error\":{2},\"mean_float_error\":{3}}}",backend,graph.Backend,max,mae));
                TestContext.WriteLine("Reference max="+max+" MAE="+mae+" actual="+graph.Backend);
                Assert.Less(max, .25, "Raw RGB must agree within a quarter of one 8-bit intensity level; byte comparisons below additionally require >99% exact pixels.");
                Assert.Less(mae, .01);
            }
            long before = Process.GetCurrentProcess().WorkingSet64;
            var timer = Stopwatch.StartNew();
            using (var model = MNNImageTask.Load(directory,backendType:backend))
            {
                double load = timer.Elapsed.TotalMilliseconds;
                long loaded = Process.GetCurrentProcess().WorkingSet64;
                timer.Restart();var result=model.Process(image);double first=timer.Elapsed.TotalMilliseconds;
                timer.Restart();var repeat=model.Process(image);double warm=timer.Elapsed.TotalMilliseconds;
                long after=Process.GetCurrentProcess().WorkingSet64;
                Assert.AreEqual(224,result.Image.Width);Assert.AreEqual(224,result.Image.Height);
                CollectionAssert.AreEqual(result.Image.Rgb,repeat.Image.Rgb);
                const int plane=224*224;
                int maxPixel=0;double meanPixel=0;
                for(int i=0;i<plane;++i)
                    for(int c=0;c<3;++c)
                    {
                        int error=Math.Abs(result.Image.Rgb[i*3+c]-MNNImageSampling.Byte(expected[c*plane+i]));
                        maxPixel=Math.Max(maxPixel,error);meanPixel+=error;
                    }
                meanPixel/=3*plane;
                Assert.LessOrEqual(maxPixel,1);Assert.Less(meanPixel,.01);
                File.WriteAllText(Path.Combine(artifacts,backend+"-measurements.json"),string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "{{\"requested\":\"{0}\",\"actual\":\"{1}\",\"load_ms\":{2},\"first_ms\":{3},\"warm_ms\":{4},\"rss_before\":{5},\"rss_loaded\":{6},\"rss_after\":{7},\"max_pixel_error\":{8},\"mean_pixel_error\":{9}}}",
                    backend,model.Backend,load,first,warm,before,loaded,after,maxPixel,meanPixel));
                var texture=MNNStudioImage.CreateTexture(result.Image);
                try{File.WriteAllBytes(Path.Combine(artifacts,backend+"-style.png"),texture.EncodeToPNG());}
                finally{UnityEngine.Object.DestroyImmediate(texture);}
                using(var cancelled=new CancellationTokenSource())
                {
                    cancelled.Cancel();Assert.Throws<OperationCanceledException>(()=>model.Process(image,cancelled.Token));
                }
                model.Dispose();model.Dispose();Assert.Throws<ObjectDisposedException>(()=>model.Process(image));
            }
            using(var studio=MNNStudioNativeBackend.Load(MNNStudioModel.Read(directory),4,false))
            {
                var result=studio.Run(new MNNStudioRequest{VisionImage=image});
                Assert.NotNull(result.ImageTask.Image);Assert.NotNull(result.GeneratedImage);Assert.AreEqual(224,result.GeneratedImage.Width);
            }
        }

        private static float[] Read(string path)
        {
            byte[] bytes=File.ReadAllBytes(path);var values=new float[bytes.Length/4];Buffer.BlockCopy(bytes,0,values,0,bytes.Length);return values;
        }
    }
}
