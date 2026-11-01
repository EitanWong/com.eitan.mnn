using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using Input = MNN.Unity.MNNGenerationGraph.Input;

namespace MNN.Unity
{
    public sealed partial class MNNPiper
    {
        public MNNGeneratedAudio Synthesize(string text, string voice = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > 1000)
                throw new ArgumentException("Enter 1–1000 characters for Piper.", nameof(text));
            lock (_gate)
            {
                Check();
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(voice))
                    voice = _voice;
                if (!_voices.Contains(voice, StringComparer.Ordinal))
                    throw new ArgumentException("Select an installed Piper voice.", nameof(voice));
                if (voice != _voice)
                    LoadGraph(voice, _threads);
                if (!MNNSpeechLanguages.HasSpeech(text))
                    return new MNNGeneratedAudio(new float[SampleRate / 100], SampleRate, text);
                if (!MNNSpeechLanguages.SupportsText(text, Languages))
                {
                    string[] scripts = MNNSpeechLanguages.Scripts(text);
                    if (scripts.Length != 2 || !scripts.Contains("han") || !scripts.Contains("latin"))
                    {
                        // Kana and Han belong to one Japanese request. Splitting
                        // them independently would misroute kanji as Chinese.
                        var audio = _languageFallback.Synthesize(text, Languages, () => throw new MNNSpeechLanguageException("Piper voice does not support this input."), cancellationToken);
                        _lastSynthesisModelDirectory = _languageFallback.LastModelDirectory;
                        return audio;
                    }

                    // Keep the trained English voice for Latin runs rather than making
                    // a Chinese voice approximate English in a mixed-language sentence.
                    var waves = new System.Collections.Generic.List<float[]>();
                    string fallbackDirectory = null;
                    foreach (string run in MNNSpeechLanguages.SplitScriptRuns(text))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (MNNSpeechLanguages.SupportsText(run, Languages))
                            waves.Add(Synthesize(run, voice, cancellationToken).Waveform);
                        else
                        {
                            var audio = _languageFallback.Synthesize(run, Languages, () => throw new MNNSpeechLanguageException("Piper voice does not support this input."), cancellationToken);
                            waves.Add(audio.Waveform);
                            fallbackDirectory = _languageFallback.LastModelDirectory;
                        }
                    }

                    _lastSynthesisModelDirectory = fallbackDirectory;
                    return new MNNGeneratedAudio(MNNSpeechDsp.JoinWaveforms(waves, SampleRate), SampleRate, text);
                }

                _lastSynthesisModelDirectory = _directory;
                var ipa = new StringBuilder();
                foreach (string clause in SplitClauses(text))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string value = clause.Trim();
                    if (value.Length == 0)
                        continue;
                    char last = value[value.Length - 1];
                    bool punctuation = ".,;:!?。，；：！？".IndexOf(last) >= 0;
                    string words = punctuation ? value.Substring(0, value.Length - 1) : value;
                    if (!string.IsNullOrWhiteSpace(words))
                        ipa.Append(Phonemize(words)).Append(punctuation ? "" : " ");
                    if (punctuation)
                        ipa.Append("。，；：！？".IndexOf(last) < 0 ? last : ".,;:!?"["。，；：！？".IndexOf(last)]).Append(' ');
                }

                int[] ids = MNNPiperPhonemes.Encode(ipa.ToString().Trim(), _phonemeMap);
                if (ipa.Length == 0 || ids.Length <= 3)
                    throw new InvalidDataException("eSpeak-NG returned no Piper phonemes.");
                cancellationToken.ThrowIfCancellationRequested();
                var samples = _graph.Run("output", new Input("input", ids, 1, ids.Length), new Input("input_lengths", new[]{ids.Length}, 1), new Input("scales", _scales, 3));
                cancellationToken.ThrowIfCancellationRequested();
                if (samples.Length < SampleRate / 10 || samples.Length > SampleRate * 60)
                    throw new InvalidDataException("Piper returned an invalid waveform length.");
                for (int i = 0; i < samples.Length; ++i)
                    samples[i] = Math.Max(-1, Math.Min(1, samples[i]));
                return new MNNGeneratedAudio(samples, SampleRate, text);
            }
        }
    }
}
