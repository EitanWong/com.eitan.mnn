using System.Globalization;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend
    {
        private MNNTurnSense _turnSense;

        private MNNStudioResult RunTurnSense(MNNStudioRequest request)
        {
            var prediction = _turnSense.Predict(request.Prompt, request.Cancellation);
            var result = new MNNTurnDetectionResult(prediction.EndOfUtteranceProbability, .5f, 0);
            return new MNNStudioResult
            {
                TurnDetection = result,
                Text = string.Format(CultureInfo.InvariantCulture, "{0}\nEnd-of-utterance probability: {1:P3}",
                    prediction.IsComplete ? "Utterance complete." : "Utterance incomplete.", prediction.EndOfUtteranceProbability),
                BackendDescription = "TurnSense SmolLM2-135M · " + _turnSense.Backend + " / High"
            };
        }

        private void DisposeTurnSense()
        {
            _turnSense?.Dispose();
            _turnSense = null;
        }
    }
}
