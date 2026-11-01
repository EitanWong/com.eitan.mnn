using System;
using System.IO;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class SupertonicQualityTests
    {
        [Test, Explicit("Diagnoses Supertonic 2 F2 alignment at different sampling settings using local weights and ASR.")]
        public void DiagnoseVersion2FemaleAlignment()
        {
            string root = Environment.GetEnvironmentVariable("MNN_TEST_SUPERTONIC2_ROOT");
            using (var model = MNNSupertonic.Load(root))
            using (var asr = MNNLlm.Load(Environment.GetEnvironmentVariable("MNN_TEST_ASR_ROOT"),
                cacheDirectory: Path.Combine(AdditionalTtsValidation.Artifacts, "quality-asr"), backendType: MNNBackendType.CPU))
                foreach (int seed in new[]{42, 7, 123})
                    foreach (int steps in new[]{5, 15})
                    foreach (float speed in new[]{1f, 1.05f})
                    {
                        var audio = model.Synthesize("The capital of France is Paris.", "F2", steps, speed, seed);
                        string path = AdditionalTtsValidation.ValidateAndSave(audio, "supertonic2-F2-quality-" + seed + "-" + steps + "-" + speed.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ".wav", 44100);
                        string text = asr.GenerateMultimodal("Transcribe the spoken sentence in English.", audioPath: path, maxNewTokens: 64).Text;
                        File.WriteAllText(Path.ChangeExtension(path, "asr.txt"), text);
                        TestContext.WriteLine(seed + " / " + steps + " / " + speed + ": " + text);
                    }
        }
    }
}
