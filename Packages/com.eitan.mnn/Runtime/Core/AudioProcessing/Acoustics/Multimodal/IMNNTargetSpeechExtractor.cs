using System.Threading;

namespace MNN.Unity
{
    /// <summary>Borrowed, worker-owned target extraction model. Supports checks frame shape/rate and required conditioning.
    /// Implementations may use vision, reference voice, or other validated target evidence.</summary>
    public interface IMNNTargetSpeechExtractor
    {
        bool Supports(MNNMultimodalSpeechInput input);
        float[] Extract(MNNMultimodalSpeechInput input, CancellationToken cancellationToken);
    }
}
