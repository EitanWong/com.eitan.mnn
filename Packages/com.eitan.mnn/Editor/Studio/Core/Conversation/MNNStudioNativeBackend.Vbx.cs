using System;
using System.Globalization;
using System.Linq;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend
    {
        private MNNVbxEmbedding _vbx;
        private MNNStudioResult RunVbx(MNNStudioRequest request)
        {
            request.Cancellation.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Choose a mono WAV matching the model's sample rate to extract its speaker vector.");
            var vector = _vbx.EncodeWaveFile(request.Audio, request.Cancellation);
            return new MNNStudioResult
            {
                Text = "VBx raw speaker embedding · 256 dimensions\n" + string.Join(", ", vector.Select(v => v.ToString("G9", CultureInfo.InvariantCulture))),
                BackendDescription = _vbx.ModelName + " · " + _vbx.Backend + " · High / threads " + _vbx.ThreadCount
            };
        }
        private void DisposeVbx() { _vbx?.Dispose(); _vbx = null; }
    }
}
