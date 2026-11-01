using System;
using System.IO;
using System.Linq;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend
    {
        private MNNFsEendDiarization _fsEend;
        private MNNStudioResult RunFsEend(MNNStudioRequest request)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply an 8 kHz mono WAV for FS-EEND diarization.");
            var activity = _fsEend.AnalyzeWaveFile(request.Audio, request.Cancellation);
            string recording = Path.GetFileNameWithoutExtension(request.Audio);
            recording = new string(recording.Select(c => char.IsWhiteSpace(c) || char.IsControl(c) ? '_' : c).ToArray());
            string rttm = activity.ToRttm(string.IsNullOrEmpty(recording) ? "recording" : recording);
            return new MNNStudioResult
            {
                Text = "FS-EEND speaker activity (100 ms frames, five original posterior columns):\n" +
                    (rttm.Length == 0 ? "No active segments." : rttm),
                BackendDescription = _fsEend.ModelName + " · " + _fsEend.Backend + " · High / threads " + MNNFsEendDiarization.DefaultThreadCount + " · 8 kHz"
            };
        }
        private void DisposeFsEend()
        {
            _fsEend?.Dispose(); _fsEend = null;
        }
    }
}
