using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using UnityEditor;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend
    {
        public MNNStudioResult Run(MNNStudioRequest request)
        {
            if (_dualTurn != null) return RunDualTurn(request);
            if (_turnSense != null) return RunTurnSense(request);
            if (_vbx != null) return RunVbx(request);
            if (_fsEend != null) return RunFsEend(request);
            if (_cueLight != null) return RunCueLight(request);
            if (_lsEend != null) return RunLsEend(request);
            if (_voxTurn != null) return RunVoxTurn(request);
            if (_dtlnAec != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio) || string.IsNullOrWhiteSpace(request.PlaybackReference))
                    throw new ArgumentException("Choose both a microphone WAV and its synchronized playback reference WAV.");
                var waveform = _dtlnAec.ProcessWaveFiles(request.Audio, request.PlaybackReference, request.Cancellation);
                return new MNNStudioResult {Text = "Echo cancellation complete. Output retains the model's 24 ms delay.", Waveform = waveform,
                    SampleRate = MNNDtlnAec.SampleRate, BackendDescription = "DTLN AEC " + _dtlnAec.Units + " · " + _dtlnAec.Backend + " / High · " + (MNNFftPlan.IsBurstAvailable ? "Burst FFT" : "C# FFT")};
            }
            if (_textDecision != null)
            {
                var result = _textDecision.Decide(request.Prompt, request.DecisionQuestion, request.Cancellation);
                return new MNNStudioResult{Decision = result, Tokens = result.InputTokens, Text = string.Join("\n", result.Labels.Select((label, i) => label + ": " + result.Probabilities[i].ToString("P3", System.Globalization.CultureInfo.InvariantCulture))), BackendDescription = "English Laya FP16 weights · CPU / High"};
            }

            if (_pageRecognizer != null)
            {
                if (request.VisionImage == null)
                    throw new ArgumentException("Choose an image before running page OCR.");
                if (request.OcrCroppedLine)
                {
                    var line = _pageRecognizer.RecognizeLine(request.VisionImage, request.Cancellation);
                    return new MNNStudioResult{Text = line.Text, RecognizedText = line, BackendDescription = "PP-OCRv5 cropped-line recognition · " + _pageRecognizer.RecognitionBackend};
                }

                var page = _pageRecognizer.Recognize(request.VisionImage, request.Cancellation);
                return new MNNStudioResult{Text = page.Text, Ocr = page, RecognizedText = new MNNTextRecognition(page.Text, page.Confidence), GeneratedImage = MNNStudioImage.DrawTextRegions(request.VisionImage, page), BackendDescription = string.Format(System.Globalization.CultureInfo.InvariantCulture, "PP-OCRv5 page OCR · detection {0} · recognition {1} · {2} regions · confidence {3:P1}", _pageRecognizer.DetectionBackend, _pageRecognizer.RecognitionBackend, page.Regions.Count, page.Confidence)};
            }

            if (_documentLayout != null)
            {
                if (request.VisionImage == null)
                    throw new ArgumentException("Choose a document page before analyzing layout.");
                var layout = _documentLayout.Analyze(request.VisionImage, request.LayoutScoreThreshold, request.Cancellation);
                var regions = layout.Regions;
                return new MNNStudioResult{DocumentLayout = layout, Detections = regions.Select(region => region.Detection).ToArray(), Text = string.Join("\n", regions.Select(region => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}: {1} ({2:P1}) · x={3:F1}, y={4:F1}, w={5:F1}, h={6:F1}", region.ReadingOrder, region.Detection.Label, region.Detection.Score, region.Detection.X, region.Detection.Y, region.Detection.Width, region.Detection.Height))), GeneratedImage = MNNStudioImage.DrawDetections(request.VisionImage, regions.Select(region => region.Detection).ToArray()), BackendDescription = "PP-DocLayoutV3 · " + _documentLayout.Backend};
            }

            if (_imageEmbedding != null)
            {
                if (request.VisionImage == null)
                    throw new ArgumentException("Choose an image before encoding it.");
                var imageVector = _imageEmbedding.EncodeImage(request.VisionImage, request.Cancellation);
                var textVector = _imageEmbedding.EncodeText(request.Prompt, request.Cancellation);
                return new MNNStudioResult{Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "Image/text cosine similarity: {0:F6}\nImage vector (512): {1}\nText vector (512): {2}", MNNImageEmbedding.Similarity(imageVector, textVector), string.Join(", ", imageVector.Select(v => v.ToString("G7", System.Globalization.CultureInfo.InvariantCulture))), string.Join(", ", textVector.Select(v => v.ToString("G7", System.Globalization.CultureInfo.InvariantCulture)))), BackendDescription = "MobileCLIP · image " + _imageEmbedding.ImageBackend + " / text " + _imageEmbedding.TextBackend};
            }

            if (_speaker != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before extracting the speaker vector.");
                var vector = _speaker.EncodeWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult{Text = "Speaker embedding · " + vector.Length + " dimensions\n" + string.Join(", ", vector.Select(value => value.ToString("G7", System.Globalization.CultureInfo.InvariantCulture))), BackendDescription = _speaker.ModelName + " speaker embedding · " + _speaker.Backend};
            }

            if (_keywordSpotter != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before keyword detection.");
                var matches = _keywordSpotter.DetectWaveFile(request.Audio, request.Keywords, request.KeywordThreshold, request.KeywordScore, request.Cancellation);
                return new MNNStudioResult{Text = FormatKeywords(matches), KeywordDetections = matches, BackendDescription = "Zipformer2 " + _keywordSpotter.Language + " keyword spotting · " + _keywordSpotter.Backend};
            }

            if (_smartTurn != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before turn detection.");
                var result = _smartTurn.PredictWaveFile(request.Audio, cancellationToken: request.Cancellation);
                return new MNNStudioResult{Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}\nCompletion probability: {1:P2}\nEvaluated the last {2:F2} s of this turn.", result.IsComplete ? "Turn complete." : "Turn incomplete.", result.CompletionProbability, Math.Min(8, result.DurationSeconds)), TurnDetection = result, BackendDescription = "Smart Turn v3.2 endpoint detection · " + _smartTurn.Backend};
            }

            if (_namoTurnDetector != null)
            {
                if (request.Prompt == null) throw new ArgumentException("Enter the current transcript before running NAMO.");
                float probability = _namoTurnDetector.Predict(request.Prompt, request.Cancellation);
                var result = new MNNTurnDetectionResult(probability, .5f, 0);
                return new MNNStudioResult { TurnDetection = result,
                    Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}\nEnd-of-turn probability: {1:P2}",
                        result.IsComplete ? "Turn complete." : "Turn incomplete.", probability),
                    BackendDescription = "NAMO multilingual transcript turn detector · " + _namoTurnDetector.Backend + " / High" };
            }

            if (_liveKitTurnDetector != null)
            {
                if (string.IsNullOrWhiteSpace(request.Prompt))
                    throw new ArgumentException("Enter the current ASR transcript before running the LiveKit turn detector.");
                float probability = _liveKitTurnDetector.Predict(request.Prompt, cancellationToken: request.Cancellation);
                var result = new MNNTurnDetectionResult(probability, .5f, 0);
                return new MNNStudioResult{Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}\nEnd-of-turn probability: {1:P2}\nInput: {2}", result.IsComplete ? "Turn complete." : "Turn incomplete.", probability, request.Prompt), TurnDetection = result, BackendDescription = "LiveKit transcript turn detector · " + _liveKitTurnDetector.Backend};
            }

            if (_avMossFormer2Tse != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio) || request.VisualFrames == null || request.VisualFrames.Length == 0)
                    throw new ArgumentException("Choose the mixture WAV and the target speaker's aligned 25 FPS face frames.");
                var audio = MNNWaveAudio.Read(request.Audio, out int sampleRate);
                if (audio.Length > (long)sampleRate * 30) throw new ArgumentException("Studio AV extraction accepts at most 30 seconds of audio.");
                var waveform = _avMossFormer2Tse.Extract(audio, request.VisualFrames, request.VisualFrames.Length / (112 * 112), sampleRate, request.Cancellation);
                return new MNNStudioResult{Text = "Visible target speaker extraction complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "AV-MossFormer2 TSE · " + _avMossFormer2Tse.Backend + " · synchronized 25 FPS target-face crops"};
            }

            if (_seg != null) return RunGestureExtraction(request);
            if (_spExPlus != null) return RunSpeakerExtraction(request);

            if (_avConvTasNet != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio) || request.VisualFrames == null || request.VisualFrames.Length == 0)
                    throw new ArgumentException("Choose the mixture WAV and synchronized25FPS target-face crops.");
                var waveform = _avConvTasNet.ExtractWaveFile(request.Audio, request.VisualFrames, request.VisualFrames.Length / (112 * 112), out int sampleRate, request.Cancellation);
                return new MNNStudioResult { Text = "AV-ConvTasNet visible target extraction complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "AV-ConvTasNet LRS2 · visual " + _avConvTasNet.Backend + " / audio " + _avConvTasNet.AudioBackend + " / High ·16kHz" };
            }

            if (_avDprnn != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio) || request.VisualFrames == null || request.VisualFrames.Length == 0)
                    throw new ArgumentException("Choose the mixture WAV and synchronized25FPS target-face crops.");
                var waveform = _avDprnn.ExtractWaveFile(request.Audio, request.VisualFrames, request.VisualFrames.Length / (112 * 112), out int sampleRate, request.Cancellation);
                return new MNNStudioResult { Text = "AV-DPRNN visible target extraction complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "AV-DPRNN VoxCeleb2 · visual " + _avDprnn.Backend + " / audio " + _avDprnn.AudioBackend + " / High ·16kHz" };
            }

            if (_sepReformer != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply a WAV for SepReformer separation.");
                var separated = _sepReformer.SeparateWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult { Text = "SepReformer separated two speaker streams. Studio previews speaker 0; use MNNSepReformerSeparation for both waveforms.",
                    Waveform = separated.Speaker0, SampleRate = separated.SampleRate,
                    BackendDescription = "SepReformer-B WSJ0-2mix · " + _sepReformer.Backend + " / High · 8 kHz" };
            }

            if (_dccrn != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply a WAV for Mayavoz DCCRN enhancement.");
                var waveform = _dccrn.EnhanceWaveFile(request.Audio, out int sampleRate, request.Cancellation);
                return new MNNStudioResult { Text = "Mayavoz DCCRN checkpoint-owned forward enhancement complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "Mayavoz DCCRN · encoder " + _dccrn.Backend + " / decoder " + _dccrn.DecoderBackend + " / CPU analysis and recurrence / High ·16kHz" };
            }

            if (_tiger != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply a WAV for TIGER separation.");
                var separated = _tiger.SeparateWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult { Text = "TIGER separated two source streams. Studio previews source0; use MNNTigerSeparation for both waveforms.",
                    Waveform = separated.Speaker0, SampleRate = separated.SampleRate,
                    BackendDescription = "TIGER · " + _tiger.Backend + " / High ·16kHz · audio separation" };
            }

            if (_sudoRmRf != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply a WAV for SudoRMRF separation.");
                var separated = _sudoRmRf.SeparateWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult { Text = "SudoRMRF separated two speaker streams. Studio previews speaker 0; use MNNSudoRmRfSeparation for both waveforms.",
                    Waveform = separated.Speaker0, SampleRate = separated.SampleRate,
                    BackendDescription = "SudoRMRF WSJ0-2mix · " + _sudoRmRf.Backend + " / High · 8 kHz" };
            }

            if (_dcunet != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply a WAV for DCUNet enhancement.");
                var waveform = _dcunet.EnhanceWaveFile(request.Audio, out int sampleRate, request.Cancellation);
                return new MNNStudioResult { Text = "DCUNet speech enhancement complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "DCUNet Libri1Mix · " + _dcunet.Backend + " / High · 16 kHz" };
            }
            if (_dccrnet != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply a WAV for DCCRNet enhancement.");
                var waveform = _dccrnet.EnhanceWaveFile(request.Audio, out int sampleRate, request.Cancellation);
                return new MNNStudioResult { Text = "DCCRNet speech enhancement complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "DCCRNet Libri1Mix · " + _dccrnet.Backend + "/" + _dccrnet.DecoderBackend + " + RNN " + _dccrnet.RecurrentBackend + " / High · 16 kHz" };
            }

            if (_dptnet != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply a WAV for DPTNet enhancement.");
                var waveform = _dptnet.EnhanceWaveFile(request.Audio, out int sampleRate, request.Cancellation);
                return new MNNStudioResult { Text = "DPTNet speech enhancement complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "DPTNet Libri1Mix · " + _dptnet.Backend + " / High · 16 kHz" };
            }

            if (_sortformer != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply a WAV for Sortformer diarization.");
                var activity = _sortformer.AnalyzeWaveFile(request.Audio, request.Cancellation);
                var text = new System.Text.StringBuilder("Sortformer speaker activity (80 ms frames, overlapping slots):\n");
                for (int speaker = 0; speaker < 4; ++speaker)
                {
                    int start = -1;
                    for (int frame = 0; frame <= activity.FrameCount; ++frame)
                    {
                        bool active = frame < activity.FrameCount && activity.Probabilities[frame * 4 + speaker] >= .5f;
                        if (active && start < 0) start = frame;
                        if (!active && start >= 0)
                        {
                            text.AppendFormat(System.Globalization.CultureInfo.InvariantCulture,
                                "speaker {0}: {1:F2}–{2:F2} s\n", speaker, start * .08, Math.Min(frame * .08, activity.DurationSeconds));
                            start = -1;
                        }
                    }
                }
                return new MNNStudioResult { Text = text.ToString(), BackendDescription = "Sortformer v2.1 · " + _sortformer.CoreBackend + " / High · 16 kHz" };
            }
            if (_dprnn != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply a WAV for DPRNN separation.");
                var separated = _dprnn.SeparateWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult{Text = "DPRNN separated two speaker streams. Studio previews speaker 0; use MNNDprnnSeparation for both waveforms.",
                    Waveform = separated.Speaker0, SampleRate = separated.SampleRate, BackendDescription = "DPRNN WHAM · " + _dprnn.Backend + " / High · 8 kHz"};
            }
            if (_convTasNet != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply a WAV for ConvTasNet separation.");
                var separated = _convTasNet.SeparateWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult{Text = "ConvTasNet separated two speaker streams. Studio previews speaker 0; use MNNConvTasNetSeparation for both waveforms.",
                    Waveform = separated.Speaker0, SampleRate = separated.SampleRate, BackendDescription = "ConvTasNet Libri2Mix · " + _convTasNet.Backend + " / High · 8 kHz"};
            }
            if (_sepFormer != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply a WAV for SepFormer separation.");
                var separated = _sepFormer.SeparateWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult{Text = "SepFormer separated two speaker streams. Studio previews speaker 0; use MNNSepFormerSeparation for both waveforms.",
                    Waveform = separated.Speaker0, SampleRate = separated.SampleRate, BackendDescription = "SepFormer WSJ02Mix · " + _sepFormer.Backend + " / High · 8 kHz"};
            }
            if (_mossFormer2 != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before speech separation.");
                var separated = _mossFormer2.SeparateWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult{Text = string.Format(System.Globalization.CultureInfo.InvariantCulture, "MossFormer2 separated two speaker streams.\nSpeaker 0: {0:F2} s\nSpeaker 1: {1:F2} s\nStudio previews speaker 0; use MNNMossFormer2Separation for both waveforms.", (double)separated.Speaker0.Length / separated.SampleRate, (double)separated.Speaker1.Length / separated.SampleRate), Waveform = separated.Speaker0, SampleRate = separated.SampleRate, BackendDescription = "MossFormer2 SS 16 kHz · " + _mossFormer2.Backend + " · managed chunk/overlap-add"};
            }

            if (_fsmnVad != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before voice activity detection.");
                var result = _fsmnVad.DetectWaveFile(request.Audio, cancellationToken: request.Cancellation);
                var text = result.Segments.Count == 0 ? "No speech detected." : string.Join("\n", result.Segments.Select((segment, index) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}. {1:F3}–{2:F3} s", index + 1, segment.StartSeconds, segment.EndSeconds)));
                return new MNNStudioResult{Text = text, VoiceActivity = result, BackendDescription = "FunASR FSMN VAD · " + _fsmnVad.Backend};
            }

            if (_resembleEnhancer != null) return RunResembleEnhancer(request);

            if (_resembleDenoiser != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply an audio WAV for Resemble Denoiser.");
                request.Cancellation.ThrowIfCancellationRequested();
                var waveform = _resembleDenoiser.EnhanceWaveFile(request.Audio, out int sampleRate, request.Cancellation);
                return new MNNStudioResult { Text = "Resemble speech denoising complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "Resemble-Denoiser-44K · " + _resembleDenoiser.Backend + " / High" };
            }
            if (_mossFormer2Se != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply an audio WAV for MossFormer2 SE.");
                request.Cancellation.ThrowIfCancellationRequested();
                var waveform = _mossFormer2Se.EnhanceWaveFile(request.Audio, out int sampleRate, request.Cancellation);
                return new MNNStudioResult{Text = "MossFormer2 SE speech enhancement complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "MossFormer2-SE-48K · " + _mossFormer2Se.Backend + " / High · seeded dither, Hamming STFT"};
            }
            if (_mossFormerGan != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio)) throw new ArgumentException("Supply an audio WAV for MossFormerGAN.");
                request.Cancellation.ThrowIfCancellationRequested();
                var waveform = _mossFormerGan.EnhanceWaveFile(request.Audio, out int sampleRate, request.Cancellation);
                return new MNNStudioResult{Text = "MossFormerGAN speech enhancement complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "MossFormerGAN-SE-16K · " + _mossFormerGan.Backend + " / High · RMS normalization, Hamming STFT"};
            }
            if (_fullSubNet != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before speech enhancement.");
                var waveform = _fullSubNet.EnhanceWaveFile(request.Audio, out int sampleRate, request.Cancellation);
                return new MNNStudioResult{Text = _fullSubNet.ModelName + " speech enhancement complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = _fullSubNet.ModelName + " · " + _fullSubNet.Backend + " / High · utterance normalization, centered Hann STFT"};
            }

            if (_gtcrn != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before speech enhancement.");
                var pcm = MNNWaveAudio.Read(request.Audio, out int sampleRate);
                var waveform = _gtcrn.Enhance(pcm, sampleRate, request.Cancellation);
                return new MNNStudioResult{Text = "GTCRN speech enhancement complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "GTCRN · " + _gtcrn.Backend + " / High · centered sqrt-Hann STFT, recurrent cache"};
            }

            if (_dtln != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before speech enhancement.");
                var waveform = _dtln.EnhanceWaveFile(request.Audio, out int sampleRate, request.Cancellation);
                return new MNNStudioResult{Text = "DTLN speech enhancement complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "DTLN · stage 1 " + _dtln.Stage1Backend + " / stage 2 " + _dtln.Stage2Backend + " · streaming FFT/overlap-add, delay compensated"};
            }

            if (_deepFilterNet3 != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before speech enhancement.");
                var waveform = _deepFilterNet3.EnhanceWaveFile(request.Audio, out int sampleRate, request.Cancellation);
                return new MNNStudioResult{Text = "DeepFilterNet3 speech enhancement complete.", Waveform = waveform, SampleRate = sampleRate,
                    BackendDescription = "DeepFilterNet3 · encoder " + _deepFilterNet3.EncoderBackend + " / ERB " + _deepFilterNet3.ErbDecoderBackend + " / DF " + _deepFilterNet3.DfDecoderBackend + " · managed libDF-compatible DSP"};
            }

            if (_frcrnSe != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before speech enhancement.");
                var waveform = _frcrnSe.EnhanceWaveFile(request.Audio, out int sampleRate, request.Cancellation);
                return new MNNStudioResult{Text = "FRCRN speech enhancement complete.", Waveform = waveform, SampleRate = sampleRate, BackendDescription = "FRCRN-SE-16K · " + _frcrnSe.Backend + " · managed STFT/iSTFT"};
            }

            if (_marbleNetVad != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before voice activity detection.");
                var result = _marbleNetVad.DetectWaveFile(request.Audio, cancellationToken: request.Cancellation);
                var text = result.Segments.Count == 0 ? "No speech detected." : string.Join("\n", result.Segments.Select((segment, index) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}. {1:F3}–{2:F3} s", index + 1, segment.StartSeconds, segment.EndSeconds)));
                return new MNNStudioResult{Text = text, VoiceActivity = result, BackendDescription = "MarbleNet multilingual VAD · " + _marbleNetVad.Backend};
            }

            if (_vad != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before voice activity detection.");
                var result = _vad.DetectWaveFile(request.Audio, cancellationToken: request.Cancellation);
                var text = new System.Text.StringBuilder();
                if (result.Segments.Count == 0)
                    text.Append("No speech detected.");
                else
                    for (int i = 0; i < result.Segments.Count; ++i)
                    {
                        if (i > 0)
                            text.AppendLine();
                        text.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "{0}. {1:F3}–{2:F3} s", i + 1, result.Segments[i].StartSeconds, result.Segments[i].EndSeconds);
                    }

                return new MNNStudioResult{Text = text.ToString(), VoiceActivity = result, BackendDescription = "Silero v5 voice activity detection · " + _vad.Backend};
            }

            if (_qwen3Asr != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before speech recognition.");
                var result = _qwen3Asr.RecognizeWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult{Text = result.Text, Tokens = result.TokenIds.Count, BackendDescription = "Qwen3-ASR 0.6B INT8 greedy ASR · " + _qwen3Asr.Backend + " / audio " + _qwen3Asr.MediaBackend};
            }

            if (_paraformer != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before speech recognition.");
                var result = _paraformer.RecognizeWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult{Text = result.Text, Tokens = result.TokenIds.Count, BackendDescription = "Paraformer Chinese greedy ASR · " + _paraformer.Backend + " / decoder " + _paraformer.DecoderBackend};
            }

            if (_whisper != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before speech recognition.");
                if (!string.IsNullOrWhiteSpace(request.RecognitionLanguage))
                    _whisper.Language = request.RecognitionLanguage;
                var result = _whisper.RecognizeWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult{Text = result.Text, Tokens = result.TokenIds.Count, BackendDescription = "Whisper " + _whisper.ModelName + " greedy ASR · " + _whisper.Backend};
            }

            if (_senseVoice != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before speech recognition.");
                var result = _senseVoice.RecognizeWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult{Text = result.Text, Tokens = result.TokenIds.Count, BackendDescription = "SenseVoiceSmall greedy CTC ASR · " + _senseVoice.Backend};
            }

            if (_speechRecognizer != null)
            {
                if (string.IsNullOrWhiteSpace(request.Audio))
                    throw new ArgumentException("Choose or record a WAV before speech recognition.");
                var result = _speechRecognizer.RecognizeWaveFile(request.Audio, request.Cancellation);
                return new MNNStudioResult{Text = result.Text, Tokens = result.TokenIds.Count, BackendDescription = "Zipformer v1 greedy ASR · " + _speechRecognizer.Backend};
            }

            if (_imageTask != null)
            {
                if (request.VisionImage == null)
                    throw new ArgumentException("Choose an image for the selected task.");
                var result = _imageTask.Process(request.VisionImage, request.Cancellation);
                string text = _imageTask.Profile.ToString();
                if (result.Depth != null)
                    text += " · relative inverse depth · larger means closer · no metric unit";
                if (result.Alpha != null)
                    text += " · " + result.Alpha.Length + " alpha values; preview composited on white.";
                if (result.Vector != null)
                    text += " · " + result.Vector.Length + " unit-normalized face features; similarity is not a calibrated identity decision.";
                if (result.Points != null)
                    text += "\n" + string.Join("\n", result.Points.Select((point, index) => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}: ({1:F2}, {2:F2})", index, point.X, point.Y)));
                if (result.Probabilities != null)
                    text += "\n" + string.Join("\n", Enumerable.Range(0, result.Probabilities.Length).OrderByDescending(i => result.Probabilities[i]).Take(5).Select(i => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} · {1:P2}", result.Labels[i], result.Probabilities[i])));
                if (result.Detections != null)
                    text += " · " + result.Detections.Length + " faces";
                return new MNNStudioResult{Text = text, ImageTask = result, Vector = result.Vector, Detections = result.Detections, GeneratedImage = result.Image ?? (result.Detections != null ? MNNStudioImage.DrawDetections(request.VisionImage, result.Detections) : null), BackendDescription = _imageTask.Profile + " · " + _imageTask.Backend};
            }

            if (_textRecognizer != null)
            {
                if (request.VisionImage == null)
                    throw new ArgumentException("Choose an upright cropped text-line image before recognition.");
                var result = _textRecognizer.Recognize(request.VisionImage, request.Cancellation);
                return new MNNStudioResult{Text = result.Text, RecognizedText = result, BackendDescription = string.Format(System.Globalization.CultureInfo.InvariantCulture, "PP-OCRv5 line recognition · {0} · confidence {1:P1}", _textRecognizer.Backend, result.Confidence)};
            }

            if (_detector != null)
            {
                if (request.VisionImage == null)
                    throw new ArgumentException("Choose and decode an image before running object detection.");
                var boxes = _detector.Detect(request.VisionImage, cancellationToken: request.Cancellation);
                var text = new System.Text.StringBuilder().Append(boxes.Length).Append(" objects");
                foreach (var box in boxes)
                    text.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "\n{0} · {1:P1} · [{2:0}, {3:0}, {4:0}, {5:0}]", box.Label, box.Score, box.X, box.Y, box.Width, box.Height);
                return new MNNStudioResult{Text = text.ToString(), Detections = boxes, GeneratedImage = MNNStudioImage.DrawDetections(request.VisionImage, boxes), BackendDescription = "YOLOv8 · " + _detector.Backend};
            }

            if (_indexTts != null || _fireRedTts != null)
            {
                request.Cancellation.ThrowIfCancellationRequested();
                var reference = MNNReferenceAudio.FromWaveFile(request.Audio);
                var audio = _indexTts != null ? _indexTts.Synthesize(request.Prompt, reference, request.SpeechLanguage, request.Seed, request.Cancellation) : _fireRedTts.Synthesize(request.Prompt, reference, request.ReferenceText, request.SpeechLanguage, request.Seed, request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_cosyVoice300M != null)
            {
                var audio = _cosyVoice300M.Synthesize(request.Prompt, request.Seed, cancellationToken: request.Cancellation);
                return new MNNStudioResult {Text = request.Prompt, Waveform = audio.Waveform, SampleRate = audio.SampleRate,
                    Tokens = _cosyVoice300M.LastSpeechTokens, BackendDescription = string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "CosyVoice-300M-MNN · AR CPU / Flow {0} / HiFT {1} · 22050 Hz · RTF {2:F3} · Text/AR {3:F3}s / Flow {4:F3}s / HiFT/DSP {5:F3}s{6}",
                        _cosyVoice300M.FlowBackend, _cosyVoice300M.VocoderBackend, _cosyVoice300M.LastRealTimeFactor,
                        _cosyVoice300M.LastTokenSeconds, _cosyVoice300M.LastFlowSeconds, _cosyVoice300M.LastVocoderSeconds,
                        _cosyVoice300M.LastAcousticCpuRetry ? " · acoustic CPU retry" : string.Empty)};
            }

            if (_cosyVoice != null)
            {
                var audio = _cosyVoice.Synthesize(request.Prompt, request.Seed, request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate, BackendDescription = "LLM: " + _cosyVoice.Backend + " · Conditioner / Flow / F0 / HiFT: CPU"};
            }

            if (_piper != null || _bertVits != null)
            {
                var audio = _piper != null ? _piper.Synthesize(request.Prompt, request.Voice, request.Cancellation) : _bertVits.Synthesize(request.Prompt, request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_kokoro != null)
            {
                var audio = _kokoro.Synthesize(request.Prompt, request.Voice, request.Speed, request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_qwen3Explicit != null)
            {
                var audio = _qwen3Explicit.Synthesize(request.Prompt, request.SpeechLanguage, request.Seed, cancellationToken: request.Cancellation, voice: request.Voice);
                return new MNNStudioResult{BackendDescription = "Qwen3-TTS · CPU · 24 kHz · Prepared voice: " + request.Voice, Text = request.Prompt, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_indicF5 != null)
            {
                var audio = _indicF5.Synthesize(request.Prompt, MNNReferenceAudio.FromWaveFile(request.Audio), request.ReferenceText, request.Seed, request.Steps, request.Speed, request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_pocket != null)
            {
                var audio = _pocket.Synthesize(request.Prompt, MNNReferenceAudio.FromWaveFile(request.Audio), request.Seed, cancellationToken: request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_dhVaani != null)
            {
                var audio = _dhVaani.Synthesize(request.Prompt, MNNReferenceAudio.FromWaveFile(request.Audio), request.ReferenceText, request.Steps, request.Speed, request.Seed, request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_mossNano != null)
            {
                var audio = _mossNano.Synthesize(request.Prompt, request.Voice, request.Seed, cancellationToken: request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_sherpaVits != null)
            {
                int speaker = Array.IndexOf(_sherpaVits.Voices, request.Voice);
                if (speaker < 0)
                    throw new ArgumentException("Select an installed Chinese VITS speaker.");
                var audio = _sherpaVits.Synthesize(request.Prompt, speaker, request.Speed, request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_mmsTamil != null)
            {
                var audio = _mmsTamil.Synthesize(request.Prompt, request.Speed, request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_indicVits != null)
            {
                int speaker = Array.IndexOf(_indicVits.Voices, request.Voice);
                if (speaker < 0)
                    throw new ArgumentException("Select an installed Indic VITS speaker.");
                var audio = _indicVits.Synthesize(request.Prompt, speaker, request.EmotionId, request.Cancellation);
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_tts != null)
            {
                var audio = _tts.Synthesize(request.Prompt, request.Voice, request.Steps, request.Speed, request.Seed, request.Cancellation, request.SpeechLanguage ?? "en");
                return new MNNStudioResult{Text = audio.Text, Waveform = audio.Waveform, SampleRate = audio.SampleRate};
            }

            if (_diffusion != null)
            {
                var image = _diffusion.Generate(request.Prompt, request.NegativePrompt, request.Steps, request.Guidance, request.Seed, request.Cancellation, percent => request.Progress?.Invoke(new MNNGenerationUpdate("Generating image: " + percent + "%", 0)));
                return new MNNStudioResult{Text = image.Prompt, GeneratedImage = image};
            }

            if (_sana != null)
            {
                var image = _sana.Edit(request.Prompt, request.ReferenceRgb, request.Steps, request.Seed, request.Cancellation, percent => request.Progress?.Invoke(new MNNGenerationUpdate("Editing image: " + percent + "%", 0)), request.Guidance);
                return new MNNStudioResult{Text = image.Prompt, GeneratedImage = image};
            }

            if (_llm != null)
            {
                if (request.Conversation != null || request.History != null)
                {
                    string summary = request.ContextSummary;
                    var conversationInput = request.Conversation;
                    int mediaReserve = (request.Image != null ? 2048 : 0) + (request.Audio != null ? 2048 : 0);
                    if (request.History != null)
                    {
                        var prepared = MNNStudioContext.Prepare(request.History, request.Prompt, summary, request.SummarizedMessages, request.ContextTokens, request.TokenLimit, mediaReserve, _llm.CountTokens, prompt => _llm.GenerateConversation(prompt, MNNStudioContext.SummaryOutputTokens, cancellationToken: request.Cancellation).Text, request.MemoryProgress, request.Cancellation, () => request.ContextProgress?.Invoke(true));
                        request.ContextProgress?.Invoke(false);
                        conversationInput = prepared.Conversation;
                        summary = prepared.Summary;
                    }
                    else if (MNNStudioContext.Estimate(conversationInput, _llm.CountTokens) > request.ContextTokens - request.TokenLimit - mediaReserve - 512)
                        throw new ArgumentException("The conversation exceeds the configured context budget.");
                    using (var stop = CancellationTokenSource.CreateLinkedTokenSource(request.Cancellation))
                    {
                        bool repeated = false;
                        string cleanText = null;
                        var conversation = _llm.GenerateConversationStreaming(conversationInput, update =>
                        {
                            string visible = MNNStudioPrompt.CleanResponse(update.Text);
                            if (MNNStudioRepetition.TryTrim(visible, out var trimmed))
                            {
                                repeated = true;
                                cleanText = trimmed;
                                stop.Cancel();
                            }

                            request.Progress?.Invoke(new MNNGenerationUpdate(repeated ? cleanText : visible, update.GeneratedTokens));
                        }, request.TokenLimit, request.Image, request.Audio, request.Speech, cancellationToken: stop.Token);
                        return new MNNStudioResult{Text = repeated ? cleanText : MNNStudioPrompt.CleanResponse(conversation.Text), Tokens = conversation.GeneratedTokens, Limited = conversation.ReachedTokenLimit, RepetitionStopped = repeated, Waveform = conversation.Waveform, SampleRate = conversation.SampleRate, Cancelled = request.Cancellation.IsCancellationRequested || (conversation.Cancelled && !repeated), ContextSummary = summary};
                    }
                }

                if (request.Image != null || request.Audio != null || request.Speech)
                {
                    var result = _llm.GenerateMultimodal(request.Prompt, request.Image, request.Audio, request.TokenLimit, request.Speech);
                    return new MNNStudioResult{Text = result.Text, Tokens = result.GeneratedTokens, Limited = result.ReachedTokenLimit, Waveform = result.Waveform, SampleRate = result.SampleRate};
                }

                var text = _llm.Generate(request.Prompt, request.TokenLimit);
                return new MNNStudioResult{Text = text.Text, Tokens = text.GeneratedTokens, Limited = text.ReachedTokenLimit};
            }

            if (_embedding != null)
            {
                var query = _embedding.EncodeQuery(request.Prompt);
                var document = _embedding.Encode(request.Document);
                return new MNNStudioResult{Vector = query, Score = MNNStudioPrompt.Cosine(query, document)};
            }

            if (_reranker != null)
                return new MNNStudioResult{Score = _reranker.Score(request.Prompt, request.Document)};
            throw new ObjectDisposedException(nameof(MNNStudioNativeBackend));
        }

        internal static string FormatKeywords(MNNKeywordDetection[] matches) => matches.Length == 0 ? "No wake word detected." : string.Join("\n", Array.ConvertAll(matches, match => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0} · {1:0.00}–{2:0.00} s · confidence {3:P1}", match.Keyword, match.StartSeconds, match.EndSeconds, match.Confidence)));
    }
}
