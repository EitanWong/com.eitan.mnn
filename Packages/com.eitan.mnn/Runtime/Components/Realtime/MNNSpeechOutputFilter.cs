using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Unity streaming-clip post-filter, downstream of the official managed audio device.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MNN/Realtime/Speech Output Filter")]
    public sealed class MNNSpeechOutputFilter : MonoBehaviour
    {
        private MNNSpeechOutputProcessor _processor;
        public MNNPlaybackWaveform Waveform { get; private set; } = new MNNPlaybackWaveform();
        public bool MayBeAudible => _processor?.MayBeAudible == true;
        public void Configure(MNNRealtimeOutputPipeline output, double presentationTailMilliseconds = 0)
        {
            _processor = new MNNSpeechOutputProcessor(output, AudioSettings.outputSampleRate, presentationTailMilliseconds);
            Waveform = _processor.Waveform;
        }
        public void SetPresentationTail(double milliseconds) => _processor?.SetPresentationTail(milliseconds);
        public Task FadeOutAsync() => _processor?.FadeOutAsync() ?? Task.CompletedTask;
        private void OnAudioFilterRead(float[] samples, int channels) => _processor?.Process(samples, channels);
    }
}
