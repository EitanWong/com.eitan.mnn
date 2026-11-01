using System;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend
    {
        private MNNCueLightTurnDetector _cueLight;
        private MNNStudioResult RunCueLight(MNNStudioRequest request)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply a 16 kHz mono WAV for CueLight turn detection.");
            var result = _cueLight.AnalyzeWaveFile(request.Audio, cancellationToken: request.Cancellation);
            return new MNNStudioResult
            {
                Text = "CueLight speech and turn timeline (20 ms frames):\n" + result.ToTsv(),
                BackendDescription = _cueLight.ModelName + " · " + _cueLight.Backend + " · High / CPU" + MNNCueLightTurnDetector.DefaultThreadCount + " · 16 kHz"
            };
        }
        private void DisposeCueLight() { _cueLight?.Dispose(); _cueLight = null; }
    }
}
