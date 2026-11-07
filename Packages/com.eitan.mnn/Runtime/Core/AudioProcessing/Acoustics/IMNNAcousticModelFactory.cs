using System;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>One owned acoustic role per call. Implementations may wrap local or remote models.
    /// Frame methods run on a capture worker; blocking noncausal methods run on the utterance worker.
    /// Dispose must be idempotent and permit retry after a failed release.</summary>
    public interface IMNNAcousticModelFactory
    {
        IMNNAcousticEnhancer CreateEnhancer(string directory);
        IMNNAcousticKeywordModel CreateKeyword(string directory);
        IMNNAcousticSeparator CreateSeparator(string directory);
        IMNNAcousticSpeakerEncoder CreateSpeaker(string directory);
    }
    public interface IMNNAcousticEnhancer : IDisposable
    {
        bool Streaming { get; }
        int DelaySamples { get; }
        string Diagnostics { get; }
        void EnhanceFrame(float[] samples, int offset);
        float[] EnhanceUtterance(float[] samples, CancellationToken token);
        void Reset();
    }
    public interface IMNNAcousticKeywordModel : IDisposable
    {
        MNNKeywordLanguage Language { get; }
        void Reset(string keywords, float threshold);
        /// <summary>Consumes one 512-sample 16 kHz frame; returns the latest keyword end time, or -1.</summary>
        double AcceptWaveform(float[] samples);
    }
    public interface IMNNAcousticSeparator : IDisposable
    {
        /// <summary>Returns two mono 16 kHz candidates with the same length as samples.</summary>
        float[][] Separate(float[] samples, CancellationToken token);
    }
    public interface IMNNAcousticSpeakerEncoder : IDisposable
    {
        float[] Encode(float[] samples, CancellationToken token);
    }
}
