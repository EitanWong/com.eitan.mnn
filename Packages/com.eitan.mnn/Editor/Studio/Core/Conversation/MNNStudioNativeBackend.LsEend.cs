using System;
using System.IO;
using System.Linq;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend
    {
        private MNNLsEendDiarization _lsEend;
        private MNNStudioResult RunLsEend(MNNStudioRequest request)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply an 8 kHz mono WAV for LS-EEND diarization.");
            var activity = _lsEend.AnalyzeWaveFile(request.Audio, request.Cancellation);
            string recording = Path.GetFileNameWithoutExtension(request.Audio);
            recording = new string(recording.Select(c => char.IsWhiteSpace(c) || char.IsControl(c) ? '_' : c).ToArray());
            string rttm = activity.ToRttm(string.IsNullOrEmpty(recording) ? "recording" : recording);
            return new MNNStudioResult
            {
                Text = "LS-EEND speaker activity (100 ms frames, nine original posterior columns):\n" +
                    (rttm.Length == 0 ? "No active segments." : rttm),
                BackendDescription = "LS-EEND Simu 8K · encoder " + _lsEend.EncoderBackend +
                    " / decoder " + _lsEend.DecoderBackend + " · High / CPU" + MNNLsEendDiarization.DefaultThreadCount + " · 8 kHz"
            };
        }
        private void DisposeLsEend()
        {
            _lsEend?.Dispose(); _lsEend = null;
        }
    }
}
