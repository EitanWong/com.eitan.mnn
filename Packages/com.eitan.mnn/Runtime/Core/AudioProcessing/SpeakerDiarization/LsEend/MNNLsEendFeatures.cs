using System.Threading;

namespace MNN.Unity
{
    internal sealed class MNNLsEendFeatures
    {
        private readonly MNNEendFeatures _features;
        internal MNNLsEendFeatures(MNNLsEendConfig config) =>
            _features = new MNNEendFeatures(config.analysis_window, config.mel_filters, true);
        internal float[] Extract(float[] pcm, CancellationToken token) => _features.Extract(pcm, token);
    }
}
