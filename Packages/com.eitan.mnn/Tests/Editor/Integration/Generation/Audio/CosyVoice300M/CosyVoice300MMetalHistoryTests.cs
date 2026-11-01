using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class CosyVoice300MMetalHistoryTests
    {
        [Test]
        public void FrozenFlowInputsMeasureMetalShapeHistoryAndTuning()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_ROOT");
            string fixtures = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_FLOW_FIXTURES");
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(fixtures)) Assert.Ignore("Set existing local model and frozen Flow fixtures; no downloads.");
            Task.Run(() =>
            {
                string csv = Path.Combine(AdditionalTtsValidation.Artifacts, "metal-history.csv");
                File.WriteAllText(csv, "tuning,fresh,repeat,phrase,frames,backend,relative_rmse,max_abs,finite\n");
                string[] names = {"x", "mask", "mu", "t", "spks", "cond"};
                var values = new Dictionary<string, float[]>[4];
                var shapes = new Dictionary<string, int[]>[4];
                for (int phrase = 0; phrase < 4; ++phrase)
                {
                    values[phrase] = new Dictionary<string, float[]>(); shapes[phrase] = new Dictionary<string, int[]>();
                    foreach (string name in names)
                    {
                        string path = Path.Combine(fixtures, $"Auto-{phrase}-_flow-{name}");
                        byte[] data = File.ReadAllBytes(path + ".bin");
                        var floats = new float[data.Length / 4]; Buffer.BlockCopy(data, 0, floats, 0, data.Length);
                        values[phrase][name] = floats;
                        shapes[phrase][name] = File.ReadAllText(path + ".shape").Split(',').Select(int.Parse).ToArray();
                    }
                }
                string graph = Path.Combine(root, "fp16/flow_estimator.mnn");
                using (var cpu = MNNInterpreter.CreateFromFile(graph))
                using (var session = cpu.CreateSession(new MNNSessionConfig {BackendType = MNNBackendType.CPU, PrecisionMode = MNNPrecisionMode.High, ThreadCount = 4}))
                {
                    var reference = new float[4][];
                    for (int phrase = 0; phrase < 4; ++phrase) reference[phrase] = Run(session, shapes[phrase], values[phrase]);
                    foreach (MNNGpuTuningMode tuning in new[] {MNNGpuTuningMode.None, MNNGpuTuningMode.Wide})
                    foreach (bool fresh in new[] {false, true})
                    {
                        MNNInterpreter model = null; MNNSession gpu = null;
                        try
                        {
                            for (int repeat = 0; repeat < 3; ++repeat)
                            foreach (int phrase in new[] {0, 1, 2, 3, 0, 2})
                            {
                                if (model == null || fresh)
                                {
                                    gpu?.Dispose(); model?.Dispose();
                                    model = MNNInterpreter.CreateFromBuffer(File.ReadAllBytes(graph)); model.SetExternalFile(graph + ".weight");
                                    gpu = model.CreateSession(new MNNSessionConfig {BackendType = MNNBackendType.Metal,
                                        PrecisionMode = MNNPrecisionMode.High, ThreadCount = 4, GpuTuningMode = tuning});
                                }
                                var actual = Run(gpu, shapes[phrase], values[phrase]);
                                double error = 0, energy = 0, max = 0;
                                bool finite = actual.All(v => !float.IsInfinity(v) && !float.IsNaN(v));
                                for (int i = 0; i < actual.Length; ++i)
                                {
                                    double difference = actual[i] - reference[phrase][i];
                                    error += difference * difference; energy += (double)reference[phrase][i] * reference[phrase][i];
                                    max = Math.Max(max, Math.Abs(difference));
                                }
                                string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
                                File.AppendAllText(csv, string.Join(",", tuning, fresh, repeat, phrase, shapes[phrase]["x"][2],
                                    gpu.ActualBackend, F(Math.Sqrt(error / Math.Max(energy, 1e-30))), F(max), finite) + "\n");
                            }
                        }
                        finally { gpu?.Dispose(); model?.Dispose(); }
                    }
                }
            }).GetAwaiter().GetResult();
        }

        [Test]
        public void FrozenFlowEncoderMeasuresShapeHistory()
            => FrozenOtherStagesMeasureShapeHistory("flow_encoder", "_flowEncoder", "encoded", true, new[] {"tokens", "lengths"});

        [TestCase("flow_regulator", "_regulator", "mu", false, new[] {"features"})]
        [TestCase("hift_f0", "_f0", "f0", false, new[] {"mel"})]
        [TestCase("hift_core", "_hift", "spectrum", false, new[] {"mel", "source_stft"})]
        public void FrozenOtherStagesMeasureShapeHistory(string graphName, string capturedName, string output, bool integer, string[] names)
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_ROOT");
            string fixtures = Environment.GetEnvironmentVariable("MNN_TEST_COSYVOICE300M_FLOW_FIXTURES");
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(fixtures)) Assert.Ignore("Set existing local model and frozen fixtures.");
            Task.Run(() =>
            {
                string csv = Path.Combine(AdditionalTtsValidation.Artifacts, graphName + "-history.csv");
                File.WriteAllText(csv, "fresh,repeat,phrase,backend,relative_rmse,max_abs,finite\n");
                var inputs = new MNNGenerationGraph.Input[4][];
                for (int phrase = 0; phrase < 4; ++phrase)
                {
                    inputs[phrase] = names.Select(name =>
                    {
                        string path = Path.Combine(fixtures, $"Auto-{phrase}-{capturedName}-{name}");
                        if (integer && name == "lengths" && !File.Exists(path + ".bin"))
                        {
                            int count = int.Parse(File.ReadAllText(Path.Combine(fixtures, $"Auto-{phrase}-{capturedName}-tokens.shape")).Split(',')[1]);
                            return new MNNGenerationGraph.Input(name, new[] {count}, 1);
                        }
                        byte[] bytes = File.ReadAllBytes(path + ".bin");
                        Array values = integer ? (Array)new int[bytes.Length / 4] : new float[bytes.Length / 4];
                        Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
                        return new MNNGenerationGraph.Input(name, values, File.ReadAllText(path + ".shape").Split(',').Select(int.Parse).ToArray());
                    }).ToArray();
                }
                string pathToGraph = Path.Combine(root, "fp16/" + graphName + ".mnn");
                using (var cpu = new MNNGenerationGraph(pathToGraph, 4, backendType: MNNBackendType.CPU, precisionMode: MNNPrecisionMode.High))
                {
                    var targets = inputs.Select(v => cpu.Run(output, v)).ToArray();
                    foreach (bool fresh in new[] {false, true})
                    {
                        MNNGenerationGraph gpu = null;
                        try
                        {
                            for (int repeat = 0; repeat < 3; ++repeat)
                            foreach (int phrase in new[] {0, 1, 2, 3, 0, 2})
                            {
                                if (gpu == null || fresh)
                                {
                                    gpu?.Dispose();
                                    gpu = new MNNGenerationGraph(pathToGraph, 4, preparedGraph: File.ReadAllBytes(pathToGraph),
                                        backendType: MNNBackendType.Metal, precisionMode: MNNPrecisionMode.High);
                                }
                                var actual = gpu.Run(output, inputs[phrase]);
                                double error = 0, energy = 0, max = 0;
                                bool finite = actual.All(v => !float.IsNaN(v) && !float.IsInfinity(v));
                                for (int i = 0; i < actual.Length; ++i)
                                {
                                    double d = actual[i] - targets[phrase][i]; error += d * d;
                                    energy += (double)targets[phrase][i] * targets[phrase][i]; max = Math.Max(max, Math.Abs(d));
                                }
                                string F(double v) => v.ToString("R", CultureInfo.InvariantCulture);
                                File.AppendAllText(csv, string.Join(",", fresh, repeat, phrase, gpu.Backend,
                                    F(Math.Sqrt(error / Math.Max(energy, 1e-30))), F(max), finite) + "\n");
                            }
                        }
                        finally { gpu?.Dispose(); }
                    }
                }
            }).GetAwaiter().GetResult();
        }

        private static float[] Run(MNNSession session, Dictionary<string, int[]> shapes, Dictionary<string, float[]> values)
        {
            session.ResizeInputs(shapes);
            foreach (var pair in values) session.GetInput(pair.Key).CopyFromArray(pair.Value);
            session.Run();
            return session.GetOutput("estimator_out").CopyToArray<float>();
        }
    }
}
