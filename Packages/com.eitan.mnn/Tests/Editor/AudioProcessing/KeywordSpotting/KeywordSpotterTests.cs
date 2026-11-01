using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MNN.Unity.Editor;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public class KeywordSpotterTests
    {
        private static string[] Tokens()
        {
            var tokens = Enumerable.Range(0, 197).Select(id => "token" + id).ToArray();
            tokens[0] = "<blk>"; tokens[1] = "<sos/eos>"; tokens[2] = "<unk>";
            tokens[3] = "a"; tokens[4] = "b"; tokens[5] = "c";
            return tokens;
        }

        private static List<MNNKeywordDetection> Run(string keywords, int[] sequence, float threshold = .25f, float peak = 20)
        {
            float[] logits = null;
            var decoder = new MNNKeywordDecoder(keywords, Tokens(), threshold, 1, 1, 4,
                _ => new float[320], (_, __) => logits);
            var matches = new List<MNNKeywordDetection>();
            foreach (int id in sequence)
            {
                logits = new float[197]; logits[id] = peak;
                var result = decoder.Advance(new float[320]);
                if (result != null) matches.Add(result);
            }
            return matches;
        }

        [Test]
        public void SearchNeedsFullKeywordAndTrailingBlanksAndCanTriggerRepeatedly()
        {
            Assert.IsEmpty(Run("a b @wake", new[] {3, 0, 0}));
            Assert.IsEmpty(Run("a b @wake", new[] {3, 4, 0}));
            var matches = Run("a b @wake", new[] {3, 4, 0, 0, 3, 4, 0, 0});
            Assert.AreEqual(2, matches.Count);
            Assert.AreEqual("wake", matches[0].Keyword);
            CollectionAssert.AreEqual(new[] {3, 4}, matches[0].TokenIds);
            Assert.AreEqual(0, matches[0].StartSeconds);
            Assert.That(matches[0].EndSeconds, Is.EqualTo(.08f).Within(.000001));
            Assert.Greater(matches[1].StartSeconds, matches[0].EndSeconds);
        }

        [Test]
        public void FailureArcsRecoverOverlappingPrefixesAndThresholdRejectsWeakMatches()
        {
            var matches = Run("a b @first\nb c @second", new[] {3, 4, 5, 0, 0});
            Assert.AreEqual(1, matches.Count);
            Assert.AreEqual("second", matches[0].Keyword);
            Assert.IsEmpty(Run("a b @wake", new[] {3, 4, 0, 0}, .95f, 5));
            Assert.AreEqual(1, Run("a b #0.1 @wake", new[] {3, 4, 0, 0}, .95f, 5).Count);
            Assert.IsEmpty(Run("a b @wake", new[] {3, 5, 4, 0, 0}));
        }

        [TestCase(""), TestCase("a"), TestCase("a q @wake"), TestCase("a b #NaN"), TestCase("a b :Infinity"), TestCase("a b\na b")]
        public void RejectsEmptyUnknownInvalidAndDuplicateKeywords(string keywords)
        {
            Assert.Throws<ArgumentException>(() => Run(keywords, new[] {0}));
        }

        [TestCase(false), TestCase(true)]
        public void CatalogPinsOnlySupportedPublisherAndProvider(bool modelScope)
        {
            var model = new MNNModelInfo {provider = modelScope ? MNNModelProvider.ModelScope : MNNModelProvider.HuggingFace,
                repositoryId = MNNKwsModel.Repository, name = "kws-zipformer-wenetspeech-3.3M-MNN"};
            typeof(MNNModelRepository).GetMethod("ApplyArtifactEvidence", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] {model, MNNKwsModel.Paths.Select(path => MNNKwsModel.Prefix + path).ToArray()});
            Assert.AreEqual(!modelScope, model.runtimeAvailable);
            Assert.AreEqual("KeywordSpotting", model.category);
            if (!modelScope)
            {
                Assert.AreEqual(MNNKwsModel.Revision, model.revision);
                Assert.AreEqual(MNNKwsModel.Sizes.Sum(), model.fileSize);
            }
            Assert.AreEqual(MNNKwsModel.Revision, MNNKwsModel.ResolveRevision("main"));
            Assert.Throws<ArgumentException>(() => MNNKwsModel.ResolveRevision("other"));
            Assert.False(MNNKwsModel.IsRepository("other/kws-zipformer-wenetspeech-3.3M-MNN"));
        }

        [Test]
        public void DedicatedTaskFiltersCatalogAndLocalModelsWithoutAcceptingAudioLanguageModels()
        {
            var kws = MNNStudioTasks.Get((int)MNNStudioTask.KeywordSpotting);
            var audio = MNNStudioTasks.Get((int)MNNStudioTask.AudioProcessing);
            var asr = MNNStudioTasks.Get((int)MNNStudioTask.SpeechRecognition);
            var catalog = new MNNModelInfo {category = MNNModelCategory.KeywordSpotting.ToString()};
            var model = new MNNStudioModel("renamed", ".", MNNStudioTask.KeywordSpotting, 0, catalog.category);
            var chat = new MNNStudioModel("audio chat", ".", MNNStudioTask.Chat, MNNModelCapabilities.AudioInput);
            Assert.True(kws.MatchesCatalog(catalog));
            Assert.True(kws.Accepts(model));
            Assert.False(kws.Accepts(chat));
            Assert.False(audio.MatchesCatalog(catalog));
            Assert.False(audio.Accepts(model));
            Assert.False(asr.MatchesCatalog(catalog));
            Assert.False(asr.Accepts(model));
            Assert.AreEqual(MNNStudioTask.KeywordSpotting, MNNStudioTasks.FromCategory(catalog.category));
            CollectionAssert.Contains(kws.Repositories, MNNKwsModel.Repository);
            CollectionAssert.DoesNotContain(audio.Repositories, MNNKwsModel.Repository);
        }

        [Test]
        public void IncompleteOrMalformedProfilesCannotRunInStudio()
        {
            string directory = Path.GetFullPath("TestArtifacts~/Kws-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, MNNKeywordSpotterConfig.FileName), "{\"repository\":\"unknown\"}");
                var model = MNNStudioModel.Read(directory);
                Assert.True(model.IsKeywordSpotter);
                Assert.False(model.HasKeywordSpottingPipeline);
                Assert.AreEqual(MNNStudioTask.KeywordSpotting, model.Task);
                Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.KeywordSpotting).CanRun(model));
                Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.SpeechRecognition).CanRun(model));
                File.WriteAllText(Path.Combine(directory, MNNKeywordSpotterConfig.FileName), "{broken");
                Assert.False(MNNKwsModel.IsComplete(directory));
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
