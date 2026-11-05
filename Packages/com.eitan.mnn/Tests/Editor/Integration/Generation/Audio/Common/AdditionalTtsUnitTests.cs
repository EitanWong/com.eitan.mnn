using System;
using System.IO;
using System.Linq;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class AdditionalTtsUnitTests
    {
        [Test]
        public void PiperPhonemeIdsMatchOfficialAlphabet()
        {
            CollectionAssert.AreEqual(new[]{1, 0, 69, 0, 74, 0, 88, 0, 120, 0, 122, 0, 154, 0, 158, 0, 2}, MNNPiperPhonemes.Encode("ɤɪɹˈːg̊"));
            CollectionAssert.AreEqual(new[]{1, 0, 14, 0, 3, 0, 15, 0, 10, 0, 2}, MNNPiperPhonemes.Encode("a b."));
        }

        [Test]
        public void PiperClauseBoundariesPreserveDecimalNumbersAndRestorePunctuation()
        {
            CollectionAssert.AreEqual(new[]{"The price is 3.14 dollars.", " Hello!"}, MNNPiper.SplitClauses("The price is 3.14 dollars. Hello!").Where(s => s.Length > 0).ToArray());
        }

        [Test]
        public void StudioOffersChineseExampleAndPreservesUserDraftOnVoiceModelChange()
        {
            var window = StudioWindowFixture.Create<MNNChatStudio>();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var type = typeof(MNNChatStudio);
            try
            {
                type.GetField("_taskIndex", flags).SetValue(window, (int)MNNStudioTask.SpeechSynthesis);
                type.GetField("_available", flags).SetValue(window, new System.Collections.Generic.List<MNNStudioModel>{MNNStudioModel.Read(GenerationTestData.Model("bert-vits2-MNN")), MNNStudioModel.Read(GenerationTestData.Model("piper-voices-MNN"))});
                type.GetField("_modelIndex", flags).SetValue(window, 0);
                string example = (string)type.GetProperty("TaskExample", flags).GetValue(window);
                StringAssert.Contains("语音合成", example);
                type.GetField("_query", flags).SetValue(window, example);
                type.GetMethod("SelectModel", flags).Invoke(window, new object[]{1});
                Assert.AreEqual("The capital of France is Paris.", type.GetField("_query", flags).GetValue(window));
                type.GetField("_query", flags).SetValue(window, "User draft");
                type.GetMethod("SelectModel", flags).Invoke(window, new object[]{0});
                Assert.AreEqual("User draft", type.GetField("_query", flags).GetValue(window));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [TestCase("nǐ", "ni3"), TestCase("lǜ", "lv4"), TestCase("shì", "shi4"), TestCase("ma", "ma5"), TestCase("a", "a5")]
        public void ChinesePinyinKeepsSyllableAndTone(string input, string expected) => Assert.AreEqual(expected, MNNBertVits2Text.NumberedPinyin(input));
        [Test]
        public void ChineseNormalizationVerbalizesNumbersAndPunctuation()
        {
            Assert.AreEqual("你好,今天二十三度。".Replace("。", "."), MNNBertVits2Text.Normalize("你好，今天23度。"));
            Assert.AreEqual("一万零一.三点一四", MNNBertVits2Text.Normalize("10001。3.14"));
            Assert.Throws<NotSupportedException>(() => MNNBertVits2Text.Normalize("Hello world"));
            Assert.Throws<ArgumentException>(() => MNNBertVits2Text.Normalize(new string ('中', 301)));
        }

        [Test]
        public void ChineseLexiconsAlignBertTokensAndBlankPhones()
        {
            var frontend = new MNNBertVits2Text(Path.Combine(GenerationTestData.Model("bert-vits2-MNN"), "common/text_processing_jsons"));
            var data = frontend.Encode("你好，语音合成。");
            Assert.AreEqual(data.Normalized.Length + 2, data.Tokens.Length);
            Assert.AreEqual(data.Tokens.Length, data.Word2Phone.Length);
            Assert.AreEqual(data.Phones.Length, data.Word2Phone.Sum());
            Assert.AreEqual(data.Phones.Length, data.Tones.Length);
            Assert.AreEqual(101, data.Tokens[0]);
            Assert.AreEqual(102, data.Tokens.Last());
            Assert.AreEqual(3, data.Word2Phone[0]);
            // 你 ni3 + 好 hao3 -> ni2 hao3; interspersed blanks include leading boundary.
            Assert.AreEqual(2, data.Tones[3]);
            Assert.AreEqual(3, data.Tones[7]);
            Assert.Throws<NotSupportedException>(() => frontend.Encode("😀"));
        }

        [Test]
        public void EnglishLexiconPreservesStressLanguageBlanksAndMixedSegments()
        {
            var frontend = new MNNBertVits2Text(Path.Combine(GenerationTestData.Model("bert-vits2-MNN"), "common/text_processing_jsons"));
            var data = frontend.EncodeSegments("Hello world!").Single();
            Assert.True(data.English);
            Assert.AreEqual(data.Phones.Length, data.Word2Phone.Sum());
            Assert.AreEqual(data.Phones.Length, data.Tones.Length);
            Assert.AreEqual(data.Phones.Length, data.Languages.Length);
            Assert.AreEqual(9, data.Tones[5]); // AH0: English offset 8 + stress 0 + 1.
            Assert.AreEqual(10, data.Tones[9]); // OW1.
            for (int i = 0; i < data.Phones.Length; ++i)
                Assert.AreEqual(i % 2 == 0 ? 0 : 2, data.Languages[i]);

            var mixed = frontend.EncodeSegments("你好，Hello world，欢迎。");
            CollectionAssert.AreEqual(new[]{false, true, false}, mixed.Select(segment => segment.English));
            Assert.AreEqual("Hello world,", mixed[1].Normalized);
            var numbers = frontend.EncodeSegments("MNN 23.5, 1,000.").Single();
            Assert.AreEqual("MNN twenty three point five, one thousand.", numbers.Normalized);
            Assert.AreEqual(numbers.Phones.Length, numbers.Word2Phone.Sum());
            Assert.Throws<NotSupportedException>(() => frontend.EncodeSegments("Hello 😀"));
            Assert.Throws<ArgumentException>(() => frontend.EncodeSegments(new string('a', 301)));
        }

        [Test]
        public void ExpressGraphPublicApisProduceKnownValuesAndReleaseResources()
        {
            string path = Path.Combine(UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AdditionalTtsUnitTests).Assembly).resolvedPath, "Tests/Fixtures/affine.mnn");
            var graph = new MNNGenerationGraph(path, 1, express: true);
            try
            {
                CollectionAssert.AreEqual(new[]{3f, 5f, 7f, 9f}, graph.Run("output", new MNNGenerationGraph.Input("input", new[]{1f, 2f, 3f, 4f}, 1, 4)));
                CollectionAssert.AreEqual(new[]{5f, 7f, 9f, 11f}, graph.Run("output", new MNNGenerationGraph.Input("input", new[]{2f, 3f, 4f, 5f}, 1, 4)));
                Assert.Throws<InvalidDataException>(() => graph.Run("missing", new MNNGenerationGraph.Input("input", new[]{1f, 2f, 3f, 4f}, 1, 4)));
            }
            finally
            {
                graph.Dispose();
                graph.Dispose();
            }

            Assert.Throws<ObjectDisposedException>(() => graph.Run("output"));
        }

        [Test]
        public void GraphSpecializationDoesNotDuplicateTheEntireSourceBeforeWritingItsResult()
        {
            long probeStart = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[4096]; GC.KeepAlive(probe);
            if (GC.GetAllocatedBytesForCurrentThread() - probeStart < probe.Length)
                Assert.Ignore("The runtime allocation counter is unavailable; run this case under host Mono.");
            string path = Environment.GetEnvironmentVariable("MNN_TEST_AFFINE_GRAPH") ?? Path.Combine(
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AdditionalTtsUnitTests).Assembly).resolvedPath,
                "Tests/Fixtures/affine.mnn");
            var source = new byte[16 * 1024 * 1024];
            byte[] fixture = File.ReadAllBytes(path);
            Buffer.BlockCopy(fixture, 0, source, 0, fixture.Length);
            var input = new MNNGenerationGraph.Input("input", new[] { 1f, 2f, 3f, 4f }, 1, 4);
            MNNGraphConstant.FreezeInputs(fixture, input); // Warm metadata/JIT before measuring.
            long before = GC.GetAllocatedBytesForCurrentThread();
            byte[] frozen = MNNGraphConstant.FreezeInputs(source, input);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            TestContext.WriteLine("Graph specialization allocated bytes: " + allocated);
            Assert.AreNotSame(source, frozen);
            Assert.Greater(frozen.Length, source.Length);
            CollectionAssert.AreEqual(fixture, source.Take(fixture.Length));
            Assert.Less(allocated, source.Length + 64L * 1024,
                "One specialization needs one result buffer; model-sized intermediate clones waste the weight memory budget.");
            GC.KeepAlive(frozen);
        }

        [Test]
        public void GraphConstantsPreserveSourceAndPublicFlatbufferSemantics()
        {
            string path = Path.Combine(UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AdditionalTtsUnitTests).Assembly).resolvedPath, "Tests/Fixtures/affine.mnn");
            var source = File.ReadAllBytes(path);
            var original = (byte[])source.Clone();
            var frozen = MNNGraphConstant.FreezeInputs(source, new MNNGenerationGraph.Input("input", new[]{1f, 2f, 3f, 4f}, 1, 4));
            CollectionAssert.AreEqual(original, source);
            using (var interpreter = MNNInterpreter.CreateFromBuffer(frozen))
            using (var session = interpreter.CreateSession())
            {
                session.Run();
                CollectionAssert.AreEqual(new[]{3f, 5f, 7f, 9f}, session.GetOutput("output").CopyToArray<float>());
            }

            Assert.Throws<InvalidDataException>(() => MNNGraphConstant.FreezeInputs(source, new MNNGenerationGraph.Input("missing", new[]{1}, 1)));
        }

        [Test]
        public void StudioDetectsTtsArchitectureInRenamedFolderAndChecksAssets()
        {
            string root = Path.GetFullPath("TestArtifacts~/AdditionalTtsMetadata");
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "config.json"), "{\"model_type\":\"piper\",\"model_path\":\"en_US-amy-low_fp16_public.mnn\",\"asset_folder\":\"espeak-ng-data\",\"sample_rate\":16000}");
                var model = MNNStudioModel.Read(root);
                Assert.AreEqual(MNNStudioTask.SpeechSynthesis, model.Task);
                Assert.True(model.IsPiper);
                Assert.False(model.HasGenerationPipeline);
                File.WriteAllBytes(Path.Combine(root, "en_US-amy-low_fp16_public.mnn"), new byte[16]);
                foreach (string file in new[]{"phontab", "phondata", "phonindex", "intonations", "en_dict", "lang/gmw/en"})
                {
                    string path = Path.Combine(root, "espeak-ng-data", file);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, new byte[1]);
                }

                model = MNNStudioModel.Read(root);
                Assert.True(model.HasGenerationPipeline);
                CollectionAssert.AreEqual(new[]{"en_US-amy-low"}, model.TtsVoices);
                File.Delete(Path.Combine(root, "espeak-ng-data/phondata"));
                model = MNNStudioModel.Read(root);
                Assert.False(model.HasGenerationPipeline);
                File.WriteAllText(Path.Combine(root, "config.json"), "{\"model_type\":\"bertvits\"}");
                model = MNNStudioModel.Read(root);
                Assert.True(model.IsBertVits2);
                Assert.AreEqual(MNNStudioTask.SpeechSynthesis, model.Task);
                Assert.False(model.HasGenerationPipeline);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }
    }
}
