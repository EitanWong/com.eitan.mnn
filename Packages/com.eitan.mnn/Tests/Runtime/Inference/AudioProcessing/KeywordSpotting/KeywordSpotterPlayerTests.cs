using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    [Category("KwsPlayer")]
    public class KeywordSpotterPlayerTests
    {
        [Test]
        public void DetectsPublishedKeywordsResetsAndReleasesOfficialMnnResourcesInPlayer()
        {
            string directory = Environment.GetEnvironmentVariable("MNN_PLAYER_KWS_ROOT");
            if (string.IsNullOrWhiteSpace(directory)) Assert.Ignore("Provide --kws-root with the prepared KWS model and demo WAV; no downloads.");
            string assets = MNNKwsModel.AssetDirectory(directory);
            string keywords = File.ReadAllText(Path.Combine(assets, "test_wavs/test_keywords.txt"));
            var spotter = MNNKeywordSpotter.Load(directory);
            try
            {
                var result = spotter.DetectWaveFile(Path.Combine(assets, "test_wavs/3.wav"), keywords);
                CollectionAssert.AreEqual(new[] {"文森特卡索", "法国"}, result.Select(match => match.Keyword));
                Assert.IsEmpty(spotter.Detect(new float[16000], 16000, keywords));
                Assert.Throws<OperationCanceledException>(() => spotter.DetectWaveFile(Path.Combine(assets, "test_wavs/3.wav"), keywords, cancellationToken: new CancellationToken(true)));
                var repeated = spotter.DetectWaveFile(Path.Combine(assets, "test_wavs/3.wav"), keywords);
                CollectionAssert.AreEqual(result.Select(match => match.Confidence), repeated.Select(match => match.Confidence));
                TestContext.WriteLine("KWS backend=" + spotter.Backend + ": " + string.Join(", ", repeated.Select(match => match.Keyword)));
            }
            finally { spotter.Dispose(); spotter.Dispose(); }
            Assert.Throws<ObjectDisposedException>(() => spotter.AcceptWaveform(new float[160]));
        }
    }
}
