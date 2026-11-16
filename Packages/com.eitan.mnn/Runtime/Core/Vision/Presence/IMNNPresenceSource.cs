using System.Threading;

namespace MNN.Unity
{
    /// <summary>Worker-owned visual detection/recognition task, independent of a model family.
    /// Implement with any validated person/face detector and tracker. Never equate a track with an identity.</summary>
    public interface IMNNPresenceSource
    {
        MNNPresenceObservation Process(MNNRgbImage image, double capturedAt, CancellationToken cancellationToken = default);
        void Reset();
    }
}
