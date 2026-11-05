using System;
using System.IO;
using System.Threading;

namespace MNN.Unity
{
    /// <summary>Profile-selected MNN acoustic models with worker-local recurrent state. No device dependency.</summary>
    public sealed class MNNAcousticMnnModelFactory : IMNNAcousticModelFactory
    {
        public IMNNAcousticEnhancer CreateEnhancer(string directory) => new Enhancer(directory);
        public IMNNAcousticKeywordModel CreateKeyword(string directory) => new Keyword(MNNKeywordSpotter.Load(directory, 1));
        public IMNNAcousticSeparator CreateSeparator(string directory) => new Separator(MNNSpeechSeparator.Load(directory, 2, MNNBackendType.CPU));
        public IMNNAcousticSpeakerEncoder CreateSpeaker(string directory) => new Speaker(MNNSpeakerEncoder.Load(directory, 1, MNNBackendType.CPU));

        private sealed class Enhancer : IMNNAcousticEnhancer
        {
            private MNNDtln _stream;
            private MNNNoiseSuppressor _utterance;
            private readonly float[] _hop = new float[128], _clean = new float[128];
            public bool Streaming => _stream != null;
            public int DelaySamples => Streaming ? MNNDtln.OutputDelaySamples : 0;
            public string Diagnostics => _stream != null ? _stream.Stage1Backend + "/" + _stream.Stage2Backend : "utterance worker";
            internal Enhancer(string directory)
            {
                if (File.Exists(Path.Combine(directory, MNNDtlnConfig.FileName))) _stream = MNNDtln.Load(directory, 1, MNNBackendType.CPU);
                else _utterance = MNNNoiseSuppressor.Load(directory, 2, MNNBackendType.CPU);
            }
            public void EnhanceFrame(float[] samples, int offset)
            {
                if (_stream == null) throw new InvalidOperationException("This enhancer supports utterances only.");
                for (int hop = 0; hop < 4; ++hop)
                {
                    Array.Copy(samples, offset + hop * 128, _hop, 0, 128);
                    _stream.ProcessHop(_hop, _clean);
                    Array.Copy(_clean, 0, samples, offset + hop * 128, 128);
                }
            }
            public float[] EnhanceUtterance(float[] samples, CancellationToken token) =>
                _utterance == null ? samples : MNNAcousticSpeakerSelection.Normalize(_utterance.Enhance(samples, 16000, token));
            public void Reset() => _stream?.Reset();
            public void Dispose()
            {
                System.Collections.Generic.List<Exception> errors = null;
                MNNAcousticModelResources.Release(ref _stream, ref errors);
                MNNAcousticModelResources.Release(ref _utterance, ref errors);
                MNNAcousticModelResources.ThrowIfFailed(errors);
            }
        }
        private sealed class Keyword : IMNNAcousticKeywordModel
        {
            private readonly MNNKeywordSpotter _model;
            internal Keyword(MNNKeywordSpotter model) { _model = model; }
            public MNNKeywordLanguage Language => _model.Language;
            public void Reset(string words, float threshold) => _model.Reset(words, threshold);
            public double AcceptWaveform(float[] samples)
            {
                double end = -1;
                foreach (var match in _model.AcceptWaveform(samples)) end = Math.Max(end, match.EndSeconds);
                return end;
            }
            public void Dispose() => _model.Dispose();
        }
        private sealed class Separator : IMNNAcousticSeparator
        {
            private readonly MNNSpeechSeparator _model;
            internal Separator(MNNSpeechSeparator model) { _model = model; }
            public float[][] Separate(float[] samples, CancellationToken token)
            {
                var result = _model.Separate(samples, 16000, token);
                return new[] { result.Speaker0, result.Speaker1 };
            }
            public void Dispose() => _model.Dispose();
        }
        private sealed class Speaker : IMNNAcousticSpeakerEncoder
        {
            private readonly MNNSpeakerEncoder _model;
            internal Speaker(MNNSpeakerEncoder model) { _model = model; }
            public float[] Encode(float[] samples, CancellationToken token) => _model.Encode(samples, 16000, token);
            public void Dispose() => _model.Dispose();
        }
    }
}
