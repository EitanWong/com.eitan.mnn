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
        internal static MNNStudioNativeBackend Load(MNNStudioModel model, int threads, bool thinking)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (!MNNStudioTasks.Get((int)model.Task).CanRun(model))
                throw new NotSupportedException(MNNStudioTasks.Get((int)model.Task).Name + " inference is not connected to Studio. Downloaded model files do not enable this pipeline.");
            if (!MNNPlatformSupport.IsSupported)
                throw new PlatformNotSupportedException(MNNPlatformSupport.UnsupportedReason);
            var backend = new MNNStudioNativeBackend(Path.GetFullPath(Path.Combine("Library", "MNN", "Studio", Guid.NewGuid().ToString("N"))));
            try
            {
                if (model.Task == MNNStudioTask.Chat)
                {
                    string package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(MNNStudioNativeBackend).Assembly).resolvedPath;
                    backend._cacheLease = MNNStudioLlmCache.Acquire(Path.GetFullPath(Path.Combine("Library", "MNN", "Studio", "ShaderCache")),
                        model.Directory, Path.Combine(package, "Runtime", "Plugins", "macOS", "libMNN.dylib"),
                        MNNInferenceCache.GraphicsDevice, threads);
                    backend._cache = backend._cacheLease.DirectoryPath;
                }
                switch (model.Task)
                {
                    case MNNStudioTask.EchoCancellation:
                        backend._dtlnAec = MNNDtlnAec.Load(model.Directory);
                        break;
                    case MNNStudioTask.ImageClassification:
                    case MNNStudioTask.ImageRestoration:
                    case MNNStudioTask.StyleTransfer:
                    case MNNStudioTask.Segmentation:
                    case MNNStudioTask.Pose:
                    case MNNStudioTask.DepthEstimation:
                    case MNNStudioTask.ImageMatting:
                    case MNNStudioTask.SuperResolution:
                    case MNNStudioTask.DocumentRectification:
                    case MNNStudioTask.FaceDetection:
                    case MNNStudioTask.FaceRecognition:
                    case MNNStudioTask.FaceLandmarks:
                        backend._imageTask = MNNImageTask.Load(model.Directory, threads);
                        break;
                    case MNNStudioTask.DocumentLayout:
                        backend._documentLayout = MNNDocumentLayout.Load(model.Directory, threads);
                        break;
                    case MNNStudioTask.NLP:
                        backend._textDecision = MNNTextDecision.Load(model.Directory, threads);
                        break;
                    case MNNStudioTask.ImageEmbedding:
                        backend._imageEmbedding = MNNImageEmbedding.Load(model.Directory, threads);
                        break;
                    case MNNStudioTask.SpeakerRecognition:
                        if (File.Exists(Path.Combine(model.Directory, MNNVbxConfig.FileName)))
                            backend._vbx = MNNVbxEmbedding.Load(model.Directory);
                        else backend._speaker = MNNSpeakerEmbedding.Load(model.Directory, threads);
                        break;
                    case MNNStudioTask.KeywordSpotting:
                        backend._keywordSpotter = MNNKeywordSpotter.Load(model.Directory, threads);
                        break;
                    case MNNStudioTask.AudioProcessing:
                        if (File.Exists(Path.Combine(model.Directory, MNNDualTurnEndpointingConfig.FileName)))
                            backend._dualTurn = MNNDualTurnEndpointing.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNTurnSenseConfig.FileName)))
                            backend._turnSense = MNNTurnSense.Load(model.Directory);
                        else if (File.Exists(Path.Combine(model.Directory, MNNVoxTurnModel.ConfigFile)))
                            backend._voxTurn = MNNVoxTurn.Load(model.Directory);
                        else if (File.Exists(Path.Combine(model.Directory, MNNSegConfig.FileName)))
                            backend._seg = MNNSegExtraction.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNSpExPlusConfig.FileName)))
                            backend._spExPlus = MNNSpExPlusExtraction.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNAvConvTasNetConfig.FileName)))
                            backend._avConvTasNet = MNNAvConvTasNetExtraction.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNAvDprnnConfig.FileName)))
                            backend._avDprnn = MNNAvDprnnExtraction.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNSepReformerConfig.FileName)))
                            backend._sepReformer = MNNSepReformerSeparation.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNDcunetConfig.FileName)))
                            backend._dcunet = MNNDcunetEnhancement.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNDccrnetConfig.FileName)))
                            backend._dccrnet = MNNDccrnetEnhancement.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNDccrnConfig.FileName)))
                            backend._dccrn = MNNDccrnEnhancement.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNTigerConfig.FileName)))
                            backend._tiger = MNNTigerSeparation.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNSudoRmRfConfig.FileName)))
                            backend._sudoRmRf = MNNSudoRmRfSeparation.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNNamoTurnDetectorConfig.FileName)))
                            backend._namoTurnDetector = MNNNamoTurnDetector.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNDptnetConfig.FileName)))
                            backend._dptnet = MNNDptnetEnhancement.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNFsEendConfig.FileName)))
                            backend._fsEend = MNNFsEendDiarization.Load(model.Directory);
                        else if (File.Exists(Path.Combine(model.Directory, MNNCueLightConfig.FileName)))
                            backend._cueLight = MNNCueLightTurnDetector.Load(model.Directory);
                        else if (File.Exists(Path.Combine(model.Directory, MNNLsEendConfig.FileName)))
                            backend._lsEend = MNNLsEendDiarization.Load(model.Directory);
                        else if (File.Exists(Path.Combine(model.Directory, MNNSortformerConfig.FileName)))
                            backend._sortformer = MNNSortformerDiarization.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNDprnnConfig.FileName)))
                            backend._dprnn = MNNDprnnSeparation.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNConvTasNetConfig.FileName)))
                            backend._convTasNet = MNNConvTasNetSeparation.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNSepFormerConfig.FileName)))
                            backend._sepFormer = MNNSepFormerSeparation.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNResembleEnhancerConfig.FileName)))
                            backend._resembleEnhancer = MNNResembleEnhancer.Load(model.Directory);
                        else if (File.Exists(Path.Combine(model.Directory, MNNResembleDenoiserConfig.FileName)))
                            backend._resembleDenoiser = MNNResembleDenoiser.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNMossFormer2SeConfig.FileName)))
                            backend._mossFormer2Se = MNNMossFormer2Se.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNMossFormerGanConfig.FileName)))
                            backend._mossFormerGan = MNNMossFormerGan.Load(model.Directory);
                        else if (File.Exists(Path.Combine(model.Directory, MNNFullSubNetConfig.FileName)))
                            backend._fullSubNet = MNNFullSubNet.Load(model.Directory);
                        else if (File.Exists(Path.Combine(model.Directory, MNNGtcrnConfig.FileName)))
                            backend._gtcrn = MNNGtcrn.Load(model.Directory);
                        else if (File.Exists(Path.Combine(model.Directory, MNNDtlnConfig.FileName)))
                            backend._dtln = MNNDtln.Load(model.Directory);
                        else if (File.Exists(Path.Combine(model.Directory, MNNAvMossFormer2TseConfig.FileName)))
                            backend._avMossFormer2Tse = MNNAvMossFormer2Tse.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNDeepFilterNet3Config.FileName)))
                            backend._deepFilterNet3 = MNNDeepFilterNet3.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNLiveKitTurnDetectorConfig.FileName)))
                            backend._liveKitTurnDetector = MNNLiveKitTurnDetector.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNMossFormer2Config.FileName)))
                            backend._mossFormer2 = MNNMossFormer2Separation.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNSmartTurnConfig.FileName)))
                            backend._smartTurn = MNNSmartTurn.Load(model.Directory);
                        else if (File.Exists(Path.Combine(model.Directory, MNNFsmnVadConfig.FileName)))
                            backend._fsmnVad = MNNFsmnVad.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNFrcrnSeConfig.FileName)))
                            backend._frcrnSe = MNNFrcrnSe.Load(model.Directory, threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNMarbleNetVadConfig.FileName)))
                            backend._marbleNetVad = MNNMarbleNetVad.Load(model.Directory, threads);
                        else
                            backend._vad = MNNSileroVad.Load(model.Directory);
                        break;
                    case MNNStudioTask.SpeechRecognition:
                        if (File.Exists(Path.Combine(model.Directory, MNNQwen3AsrConfig.FileName)))
                            backend._qwen3Asr = MNNQwen3AsrRecognizer.Load(model.Directory, threadCount: threads, cacheDirectory: backend._cache);
                        else if (File.Exists(Path.Combine(model.Directory, MNNParaformerConfig.FileName)))
                            backend._paraformer = MNNParaformerRecognizer.Load(model.Directory, threadCount: threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNWhisperConfig.FileName)))
                            backend._whisper = MNNWhisperRecognizer.Load(model.Directory, threadCount: threads);
                        else if (File.Exists(Path.Combine(model.Directory, MNNSenseVoiceConfig.FileName)))
                            backend._senseVoice = MNNSenseVoiceRecognizer.Load(model.Directory, threadCount: threads);
                        else
                            backend._speechRecognizer = MNNZipformerRecognizer.Load(model.Directory, threadCount: threads);
                        break;
                    case MNNStudioTask.OCR:
                        if (model.IsPaddlePage)
                            backend._pageRecognizer = MNNPaddleOcr.Load(model.OcrPageRoot ?? model.Directory, model.OcrRecognizerProfile, threadCount: threads);
                        else
                            backend._textRecognizer = MNNPaddleTextRecognizer.Load(model.Directory, threadCount: threads);
                        break;
                    case MNNStudioTask.ObjectDetection:
                        backend._detector = MNNYoloV8.Load(model.Directory, threadCount: threads);
                        break;
                    case MNNStudioTask.Chat:
                        backend._llm = MNNLlm.LoadForChat(model.Directory, threads, thinking, backend._cache);
                        break;
                    case MNNStudioTask.Embedding:
                        backend._embedding = MNNEmbedding.Load(model.Directory, threads, backend._cache);
                        break;
                    case MNNStudioTask.Reranker:
                        backend._reranker = MNNReranker.Load(model.Directory, threads, backend._cache);
                        break;
                    case MNNStudioTask.SpeechSynthesis:
                        if (model.IsIndexTts)
                            backend._indexTts = MNNIndexTts25.Load(model.Directory, threads);
                        else if (model.IsFireRedTts)
                            backend._fireRedTts = MNNFireRedTts3.Load(model.Directory, threads);
                        else if (model.IsCosyVoice300M)
                            backend._cosyVoice300M = MNNCosyVoice300M.Load(model.Directory, threads);
                        else if (model.IsCosyVoice)
                            backend._cosyVoice = MNNCosyVoice.Load(model.Directory, threads);
                        else if (model.IsQwen3Explicit)
                            backend._qwen3Explicit = MNNQwen3ExplicitTts.Load(model.Directory, threads);
                        else if (model.IsIndicF5)
                            backend._indicF5 = MNNIndicF5Tts.Load(model.Directory, threads);
                        else if (model.IsPocket)
                            backend._pocket = MNNPocketTts.Load(model.Directory, threads);
                        else if (model.IsDhVaani)
                            backend._dhVaani = MNNDhVaani.Load(model.Directory, threads);
                        else if (model.TtsProfile == MNNTtsModelProfile.MossNano)
                            backend._mossNano = MNNMossNano.Load(model.Directory, threads);
                        else if (model.TtsProfile == MNNTtsModelProfile.Kokoro)
                            backend._kokoro = MNNKokoro.Load(model.Directory, threads);
                        else if (model.TtsProfile == MNNTtsModelProfile.SherpaVitsChinese)
                            backend._sherpaVits = MNNSherpaVitsChinese.Load(model.Directory, threads);
                        else if (model.TtsProfile == MNNTtsModelProfile.MmsTamil)
                            backend._mmsTamil = MNNMmsTamil.Load(model.Directory, threads, model.MmsTamilPrecision);
                        else if (model.TtsProfile == MNNTtsModelProfile.IndicVits)
                            backend._indicVits = MNNIndicVits.Load(model.Directory, threads);
                        else if (model.IsPiper)
                            backend._piper = MNNPiper.Load(model.Directory, threads, MNNStudioPiperSettings.ResolveExecutable(model.Directory), MNNStudioPiperSettings.ResolveDataDirectory(model));
                        else if (model.IsBertVits2)
                            backend._bertVits = MNNBertVits2.Load(model.Directory, threads);
                        else if (model.TtsProfile == MNNTtsModelProfile.Supertonic)
                            backend._tts = MNNSupertonic.Load(model.Directory, threads, model.SupertonicPrecision);
                        else
                            throw new NotSupportedException("This TTS repository needs its matching tokenizer and inference pipeline before Studio can synthesize speech.");
                        break;
                    case MNNStudioTask.ImageGeneration:
                        if (model.IsSana)
                            backend._sana = MNNSana.Load(model.Directory, threads);
                        else
                            backend._diffusion = MNNStableDiffusion.Load(model.Directory, threads);
                        break;
                }

                return backend;
            }
            catch
            {
                backend.Dispose();
                throw;
            }
        }
    }
}
