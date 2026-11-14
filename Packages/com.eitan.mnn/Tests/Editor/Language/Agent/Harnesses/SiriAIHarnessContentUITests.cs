using System;
using System.IO;
using System.Linq;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessContentUITests
    {
        [Test]
        public void DraftCompletionCannotClearNewEditsOrCrossHostScopes()
        {
            var draft = new HarnessTaskDraft();
            draft.SetScope(1);
            draft.Objective = "Inspect input";
            draft.Add(new HarnessContentPart(HarnessContentKind.Text, "original"));
            var sent = draft.Snapshot();
            sent.Attachments[0] = new HarnessContentPart(HarnessContentKind.Text, "caller changed");
            Assert.AreEqual("original", sent.Attachments[0].Payload);
            draft.Objective = "Next task";
            Assert.False(draft.ClearIfCurrent(sent.Scope, sent.Revision));
            Assert.AreEqual("Next task", draft.Objective);
            long revision = draft.Revision;
            draft.SetScope(2);
            Assert.False(draft.TryAdd(1, revision, new HarnessContentPart(HarnessContentKind.Text, "late file")));
            Assert.AreEqual(0, draft.Attachments.Length);
            Assert.AreEqual("", draft.Objective);
            draft.Objective = "Accepted";
            var accepted = draft.Snapshot();
            Assert.True(draft.ClearIfCurrent(accepted.Scope, accepted.Revision));
            Assert.AreEqual("", draft.Objective);
        }

        [Test]
        public void ExplicitFileImportAndExportAreBoundedAndNeverEscapeOrOverwrite()
        {
            string folder = Fixtures.HarnessTestCase.NewDirectory();
            Directory.CreateDirectory(folder);
            try
            {
                string file = Path.Combine(folder, "输入.txt");
                File.WriteAllBytes(file, new byte[] { 104, 105 });
                var part = HarnessContentFiles.Import(file);
                Assert.AreEqual("aGk=", part.Payload);
                Assert.AreEqual("输入.txt", part.FileName);
                var hostile = new HarnessContentPart(HarnessContentKind.Base64, part.Payload, "text/plain", "../../bad\\report.txt");
                string exports = Path.Combine(folder, "exports");
                string first = HarnessContentFiles.Export(exports, hostile);
                string second = HarnessContentFiles.Export(exports, hostile);
                Assert.AreEqual(Path.GetFullPath(exports), Path.GetDirectoryName(first));
                Assert.AreNotEqual(first, second);
                CollectionAssert.AreEqual(new byte[] { 104, 105 }, File.ReadAllBytes(first));
                Assert.AreEqual("hi", File.ReadAllText(second));
                File.WriteAllBytes(file, new byte[HarnessContentFiles.MaximumFileBytes + 1]);
                Assert.Throws<ArgumentException>(() => HarnessContentFiles.Import(file));
                Assert.Throws<ArgumentException>(() => HarnessContentFiles.Export(exports,
                    new HarnessContentPart(HarnessContentKind.Url, "https://example.org/report")));
                var draft = new HarnessTaskDraft();
                draft.Add(new HarnessContentPart(HarnessContentKind.Text, new string('x', 200000)));
                Assert.Throws<ArgumentException>(() => draft.Add(new HarnessContentPart(HarnessContentKind.Text, new string('x', 100000))));
                Assert.AreEqual(1, draft.Attachments.Length, "Failed additions leave the draft unchanged.");
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void FeedbackIncludesArtifactTextAndMetadataWithoutBinaryPayloads()
        {
            var parts = new[] { new HarnessContentPart(HarnessContentKind.Text, "Visible result"),
                new HarnessContentPart(HarnessContentKind.Base64, "c2VjcmV0LWJpbmFyeQ==", "image/png", "image.png") };
            var artifacts = new[] { new HarnessArtifact("report", "Report", null, parts) };
            string summary = HarnessArtifactPresentation.Summary(artifacts, 500);
            StringAssert.Contains("Visible result", summary);
            StringAssert.Contains("image.png", summary);
            StringAssert.DoesNotContain("c2VjcmV0LWJpbmFyeQ==", summary);
            Assert.LessOrEqual(HarnessArtifactPresentation.Summary(artifacts, 24).Length, 24);
            Assert.AreEqual(13, HarnessArtifactPresentation.DecodedBytes(parts[1]));
        }
    }
}
