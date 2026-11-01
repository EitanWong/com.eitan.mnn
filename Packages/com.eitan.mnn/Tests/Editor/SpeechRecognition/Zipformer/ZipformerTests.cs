using System;
using System.IO;
using System.Linq;
using System.Threading;
using MNN.Unity.Editor;
using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public class ZipformerTests
    {
        [Test]
        public void ReflectedFeaturesAcceptSilenceShortAudioAndCancellation()
        {
            float[] features = MNNZipformerFeatures.Extract(new float[16000], out int frames, CancellationToken.None);
            Assert.AreEqual(100, frames);
            Assert.True(features.All(v => Math.Abs(v - (float)Math.Log(1.1920928955078125e-7)) < .00001));
            Assert.AreEqual(0, MNNZipformerFeatures.Extract(new[] {.1f}, out frames, CancellationToken.None).Length);
            Assert.Throws<OperationCanceledException>(() => MNNZipformerFeatures.Extract(new float[16000], out _, new CancellationToken(true)));
        }

        [TestCase(MNNZipformerModel.English), TestCase(MNNZipformerModel.Bilingual)]
        public void ProviderAliasesShareVerifiedAssetsAndRejectUnpreparedModels(string name)
        {
            Assert.True(MNNZipformerModel.IsRepository("MNN/" + name));
            Assert.True(MNNZipformerModel.IsRepository("taobao-mnn/" + name));
            Assert.False(MNNZipformerModel.IsRepository("community/" + name));
            var files = MNNZipformerModel.Files(name == MNNZipformerModel.Bilingual);
            Assert.AreEqual(4, files.Length);
            Assert.True(files.All(f => f.Sha256.Length == 64 && f.Bytes > 0));
            string directory = Path.GetFullPath("TestArtifacts~/ZipformerUnit/" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                File.WriteAllText(Path.Combine(directory, MNNZipformerConfig.FileName), JsonUtility.ToJson(MNNZipformerModel.Config(false)));
                var model = MNNStudioModel.Read(directory);
                Assert.AreEqual(MNNStudioTask.SpeechRecognition, model.Task);
                Assert.False(model.HasSpeechRecognitionPipeline);
                Assert.False(MNNStudioTasks.Get((int)MNNStudioTask.SpeechRecognition).CanRun(model));
                Assert.Throws<FileNotFoundException>(() => MNNZipformerModel.Prepare(directory, "MNN/" + name));
                Assert.Throws<FileNotFoundException>(() => MNNZipformerConfig.Resolve(directory, "../outside.mnn"));
            }
            finally { Directory.Delete(directory, true); }
        }
    }
}
