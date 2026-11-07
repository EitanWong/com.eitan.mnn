using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class SpeechOutputProcessorTests
    {
        [Test]
        public void PublishesTheRenderedFadeAndRejectsInterruptedResponses()
        {
            var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(16000, 1), 160);
            var filter = new MNNSpeechOutputProcessor(output, 16000, 50);
            var response = output.BeginResponse();
            var pcm = new float[160];
            Array.Fill(pcm, .25f);
            filter.Process(pcm, 1);
            var points = new float[64];
            Assert.True(filter.Waveform.TryRead(points, response, out float rms));
            Assert.That(rms, Is.EqualTo(.25f).Within(.00001f));
            var fading = filter.FadeOutAsync();
            for (int i = 0; i < 10; ++i) { Array.Fill(pcm, .25f); filter.Process(pcm, 1); }
            fading.GetAwaiter().GetResult();
            Assert.True(filter.Waveform.TryRead(points, response, out rms));
            Assert.Zero(rms);
            output.Interrupt();
            Assert.False(filter.Waveform.TryRead(points, output.BeginResponse(), out rms));
            Assert.Throws<ArgumentOutOfRangeException>(() => filter.SetPresentationTail(double.NaN));
        }
    }
}
