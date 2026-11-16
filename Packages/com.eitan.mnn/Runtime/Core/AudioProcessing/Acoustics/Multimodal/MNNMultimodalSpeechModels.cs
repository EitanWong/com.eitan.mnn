using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Connects existing model instances to target routing. Caller owns and disposes the models.</summary>
    public static class MNNMultimodalSpeechModels
    {
        public static MNNMultimodalSpeechProcessor CreateFromProviders(IMNNTargetSpeechExtractor audioVisual = null,
            IMNNTargetSpeechExtractor referenceTarget = null,
            Func<MNNMultimodalSpeechInput, CancellationToken, MNNSeparatedAudio> separate = null,
            Func<MNNSeparatedAudio, string, int> selectSpeaker = null, bool requirePresence = true) =>
            new MNNMultimodalSpeechProcessor(audioVisual == null ? null : (Func<MNNMultimodalSpeechInput, CancellationToken, float[]>)audioVisual.Extract,
                referenceTarget == null ? null : (Func<MNNMultimodalSpeechInput, CancellationToken, float[]>)referenceTarget.Extract, separate, selectSpeaker,
                requirePresence, visualSupports: audioVisual == null ? null : (Func<MNNMultimodalSpeechInput, bool>)audioVisual.Supports,
                referenceSupports: referenceTarget == null ? null : (Func<MNNMultimodalSpeechInput, bool>)referenceTarget.Supports);

        public static MNNMultimodalSpeechProcessor Create(MNNAvMossFormer2Tse audioVisual = null,
            MNNSpeechSeparator separator = null, Func<MNNSeparatedAudio, string, int> selectSpeaker = null,
            Func<MNNMultimodalSpeechInput, CancellationToken, float[]> referenceTarget = null,
            bool requirePresence = true)
        {
            Func<MNNMultimodalSpeechInput, CancellationToken, float[]> visual = null;
            Func<MNNMultimodalSpeechInput, CancellationToken, MNNSeparatedAudio> separate = null;
            if (audioVisual != null)
                visual = (input, token) => audioVisual.Extract(input.CopyAudio(), input.Visual.CopyFrames(),
                    input.Visual.FrameCount, input.SampleRate, token);
            if (separator != null)
                separate = (input, token) => separator.Separate(input.CopyAudio(), input.SampleRate, token);
            return new MNNMultimodalSpeechProcessor(visual, referenceTarget, separate, selectSpeaker, requirePresence,
                visualSupports: input => input.Visual != null && input.Visual.Width == 112 && input.Visual.Height == 112 && input.Visual.FramesPerSecond == 25);
        }
    }
}
