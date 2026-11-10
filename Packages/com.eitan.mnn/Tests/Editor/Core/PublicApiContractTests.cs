using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class PublicApiContractTests
    {
        [Test]
        public void ApplicationsAndEditorHaveNoRuntimeFriendship()
        {
            var friends = typeof(MNNInterpreter).Assembly.GetCustomAttributes<InternalsVisibleToAttribute>();
            CollectionAssert.AreEquivalent(new[] { "MNN.Unity.Tests", "MNN.Unity.Runtime.Tests",
                "MNN.Unity.Integration.Tests", "MNN.Unity.Burst" }, friends.Select(value => value.AssemblyName));
        }

        [Test]
        public void NativeImplementationIsNotExported()
        {
            foreach (var type in typeof(MNNInterpreter).Assembly.GetExportedTypes())
            {
                Assert.False(type.Namespace?.StartsWith("MNN.Unity.Interop", StringComparison.Ordinal) == true, type.FullName);
                Assert.False(typeof(System.Runtime.InteropServices.SafeHandle).IsAssignableFrom(type), type.FullName);
            }
        }

        [Test]
        public void MissingModelInspectionIsOfflineAndDoesNotCreateFiles()
        {
            string missing = Path.GetFullPath(Path.Combine("TestArtifacts~", "UnifiedAPI", Guid.NewGuid().ToString("N")));
            foreach (MNNModelKind kind in Enum.GetValues(typeof(MNNModelKind)))
                Assert.False(MNNModelSupport.IsComplete(kind, missing), kind.ToString());
            Assert.AreEqual(MNNSpeechModelKind.Unknown, MNNSpeechSynthesizer.DetectModelKind(missing));
            Assert.IsEmpty(MNNSpeechSynthesizer.InstalledVoices(missing));
            Assert.False(Directory.Exists(missing));
        }

        [Test]
        public void InvalidModelKindIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNModelSupport.IsComplete((MNNModelKind)(-1), "."));
        }

        [TestCase("piper", MNNSpeechModelKind.Piper)]
        [TestCase("bertvits", MNNSpeechModelKind.BertVits2)]
        public void SpeechMetadataWithoutAssetsIsDetectedButNotInstalled(string modelType, MNNSpeechModelKind kind)
        {
            string directory = Path.GetFullPath(Path.Combine("TestArtifacts~", "UnifiedAPI", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, "config.json"), "{\"model_type\":\"" + modelType + "\",\"model_path\":\"missing.mnn\",\"sample_rate\":16000}");
                Assert.AreEqual(kind, MNNSpeechSynthesizer.DetectModelKind(directory));
                Assert.False(MNNSpeechSynthesizer.IsComplete(directory, kind));
                Assert.False(MNNSpeechSynthesizer.HasProfile(directory));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Test]
        public void ResamplingOwnsOutputAndRejectsInvalidPcm()
        {
            var source = new[] { 0.25f, -0.5f, 0.75f };
            var result = MNNAudioResampling.Resample(source, 16000, 16000, tailSamples: 2);
            CollectionAssert.AreEqual(new[] { -0.5f, 0.75f }, result);
            result[0] = 1;
            Assert.AreEqual(-0.5f, source[1]);
            Assert.Throws<ArgumentException>(() => MNNAudioResampling.Resample(new[] { float.NaN }, 16000, 16000));
            Assert.Throws<ArgumentOutOfRangeException>(() => MNNAudioResampling.Resample(source, 0, 16000));
        }

        [Test]
        public void CustomProviderResultsRejectInvalidDataAtTheBoundary()
        {
            Assert.Throws<ArgumentNullException>(() => new MNNGeneratedImage(null, 1, 1, "", 0));
            Assert.Throws<ArgumentException>(() => new MNNGeneratedImage(new byte[2], 1, 1, "", 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MNNGenerationUpdate("text", -1));
        }

        [Test]
        public void ModelMetadataCannotMutateTheSharedVoiceTable()
        {
            string expected = MNNIndicVits.InstalledSpeakers[0];
            string[] speakers = MNNIndicVits.InstalledSpeakers;
            speakers[0] = "changed";
            Assert.AreEqual(expected, MNNIndicVits.InstalledSpeakers[0]);
        }

        [TestCase(MNNModelSource.HuggingFace, "developerabu/pocket-tts-mnn")]
        [TestCase(MNNModelSource.HuggingFace, "developerabu/whisper-tiny-en-mnn")]
        public void RevisionResolutionIsPinnedAndIdempotent(MNNModelSource source, string repository)
        {
            string revision = MNNModelRevisions.Resolve(source, repository);
            Assert.AreEqual(40, revision.Length);
            Assert.AreEqual(revision, MNNModelRevisions.Resolve(source, repository, revision));
            Assert.Throws<ArgumentException>(() => MNNModelRevisions.Resolve(source, repository, "other-revision"));
        }
    }
}
