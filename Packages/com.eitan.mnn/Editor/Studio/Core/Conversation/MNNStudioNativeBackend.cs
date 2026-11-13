using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using UnityEditor;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioNativeBackend : IMNNStudioBackend
    {
        private MNNMiniMindO _miniMindO;
        private MNNLlm _llm;
        private MNNEmbedding _embedding;
        private MNNReranker _reranker;
        private MNNSupertonic _tts;
        private MNNKokoro _kokoro;
        private MNNIndicVits _indicVits;
        private MNNMmsTamil _mmsTamil;
        private MNNSherpaVitsChinese _sherpaVits;
        private MNNMossNano _mossNano;
        private MNNIndicF5Tts _indicF5;
        private MNNPocketTts _pocket;
        private MNNDhVaani _dhVaani;
        private MNNQwen3ExplicitTts _qwen3Explicit;
        private MNNPiper _piper;
        private MNNBertVits2 _bertVits;
        private MNNCosyVoice _cosyVoice;
        private MNNCosyVoice300M _cosyVoice300M;
        private MNNIndexTts25 _indexTts;
        private MNNFireRedTts3 _fireRedTts;
        private MNNStableDiffusion _diffusion;
        private MNNSana _sana;
        private MNNYoloV8 _detector;
        private MNNImageTask _imageTask;
        private MNNDocumentLayout _documentLayout;
        private MNNTextDecision _textDecision;
        private MNNSpeakerEmbedding _speaker;
        private MNNImageEmbedding _imageEmbedding;
        private MNNPaddleTextRecognizer _textRecognizer;
        private MNNPaddleOcr _pageRecognizer;
        private MNNZipformerRecognizer _speechRecognizer;
        private MNNSenseVoiceRecognizer _senseVoice;
        private MNNWhisperRecognizer _whisper;
        private MNNQwen3AsrRecognizer _qwen3Asr;
        private MNNParaformerRecognizer _paraformer;
        private MNNSileroVad _vad;
        private MNNFsmnVad _fsmnVad;
        private MNNFrcrnSe _frcrnSe;
        private MNNDeepFilterNet3 _deepFilterNet3;
        private MNNDtln _dtln;
        private MNNGtcrn _gtcrn;
        private MNNFullSubNet _fullSubNet;
        private MNNResembleDenoiser _resembleDenoiser;
        private MNNMossFormerGan _mossFormerGan;
        private MNNMossFormer2Se _mossFormer2Se;
        private MNNAvMossFormer2Tse _avMossFormer2Tse;
        private MNNMarbleNetVad _marbleNetVad;
        private MNNMossFormer2Separation _mossFormer2;
        private MNNSepFormerSeparation _sepFormer;
        private MNNConvTasNetSeparation _convTasNet;
        private MNNDprnnSeparation _dprnn;
        private MNNSortformerDiarization _sortformer;
        private MNNDptnetEnhancement _dptnet;
        private MNNDcunetEnhancement _dcunet;
        private MNNDccrnetEnhancement _dccrnet;
        private MNNAvDprnnExtraction _avDprnn;
        private MNNAvConvTasNetExtraction _avConvTasNet;
        private MNNSpExPlusExtraction _spExPlus;
        private MNNSegExtraction _seg;
        private MNNSepReformerSeparation _sepReformer;
        private MNNSudoRmRfSeparation _sudoRmRf;
        private MNNTigerSeparation _tiger;
        private MNNDccrnEnhancement _dccrn;
        private MNNLiveKitTurnDetector _liveKitTurnDetector;
        private MNNNamoTurnDetector _namoTurnDetector;
        private MNNVoxTurn _voxTurn;
        private MNNKeywordSpotter _keywordSpotter;
        private MNNSmartTurn _smartTurn;
        private MNNDtlnAec _dtlnAec;
        private string _cache;
        private MNNStudioLlmCache _cacheLease;
        private MNNStudioNativeBackend(string cache) => _cache = cache;
        public void Dispose()
        {
            DisposeVbx();
            DisposeFsEend();
            DisposeCueLight();
            DisposeLsEend();
            _dtlnAec?.Dispose();
            _dtlnAec = null;
            _keywordSpotter?.Dispose();
            _keywordSpotter = null;
            _detector?.Dispose();
            _documentLayout?.Dispose();
            _documentLayout = null;
            _textDecision?.Dispose();
            _textDecision = null;
            _imageEmbedding?.Dispose();
            _imageEmbedding = null;
            _speaker?.Dispose();
            _speaker = null;
            _imageTask?.Dispose();
            _imageTask = null;
            _detector = null;
            _textRecognizer?.Dispose();
            _textRecognizer = null;
            _pageRecognizer?.Dispose();
            _pageRecognizer = null;
            _senseVoice?.Dispose();
            _senseVoice = null;
            _smartTurn?.Dispose();
            _smartTurn = null;
            _fsmnVad?.Dispose();
            _fsmnVad = null;
            _frcrnSe?.Dispose();
            _frcrnSe = null;
            _deepFilterNet3?.Dispose();
            _deepFilterNet3 = null;
            _dtln?.Dispose();
            _dtln = null;
            _gtcrn?.Dispose();
            _gtcrn = null;
            _fullSubNet?.Dispose();
            _fullSubNet = null;
            DisposeResembleEnhancer();
            _resembleDenoiser?.Dispose();
            _resembleDenoiser = null;
            _mossFormerGan?.Dispose();
            _mossFormerGan = null;
            _mossFormer2Se?.Dispose();
            _mossFormer2Se = null;
            _avMossFormer2Tse?.Dispose();
            _avMossFormer2Tse = null;
            _marbleNetVad?.Dispose();
            _marbleNetVad = null;
            _mossFormer2?.Dispose();
            _mossFormer2 = null;
            _sortformer?.Dispose(); _dptnet?.Dispose();
            _dcunet?.Dispose(); _dcunet = null;
            _dccrnet?.Dispose(); _dccrnet = null;
            _avDprnn?.Dispose(); _avDprnn = null;
            _avConvTasNet?.Dispose(); _avConvTasNet = null;
            _spExPlus?.Dispose(); _spExPlus = null;
            _seg?.Dispose(); _seg = null;
            _sepReformer?.Dispose(); _sepReformer = null;
            _sudoRmRf?.Dispose(); _sudoRmRf = null;
            _tiger?.Dispose(); _tiger = null;
            _dccrn?.Dispose(); _dccrn = null;
            _sortformer = null;
            _dprnn?.Dispose();
            _dprnn = null;
            _convTasNet?.Dispose();
            _convTasNet = null;
            _sepFormer?.Dispose();
            _sepFormer = null;
            _liveKitTurnDetector?.Dispose();
            _liveKitTurnDetector = null;
            DisposeTurnSense();
            DisposeDualTurn();
            _namoTurnDetector?.Dispose();
            _namoTurnDetector = null;
            _voxTurn?.Dispose();
            _voxTurn = null;
            _vad?.Dispose();
            _vad = null;
            _paraformer?.Dispose();
            _paraformer = null;
            _qwen3Asr?.Dispose();
            _qwen3Asr = null;
            _whisper?.Dispose();
            _whisper = null;
            _speechRecognizer?.Dispose();
            _speechRecognizer = null;
            _miniMindO?.Dispose();
            _miniMindO = null;
            _llm?.Dispose();
            _llm = null;
            _embedding?.Dispose();
            _embedding = null;
            _reranker?.Dispose();
            _reranker = null;
            _kokoro?.Dispose();
            _kokoro = null;
            _qwen3Explicit?.Dispose();
            _qwen3Explicit = null;
            _indicF5?.Dispose();
            _indicF5 = null;
            _pocket?.Dispose();
            _pocket = null;
            _dhVaani?.Dispose();
            _dhVaani = null;
            _mossNano?.Dispose();
            _mossNano = null;
            _sherpaVits?.Dispose();
            _sherpaVits = null;
            _mmsTamil?.Dispose();
            _mmsTamil = null;
            _indicVits?.Dispose();
            _indicVits = null;
            _tts?.Dispose();
            _tts = null;
            _piper?.Dispose();
            _piper = null;
            _bertVits?.Dispose();
            _bertVits = null;
            _indexTts?.Dispose();
            _indexTts = null;
            _fireRedTts?.Dispose();
            _fireRedTts = null;
            _cosyVoice300M?.Dispose();
            _cosyVoice300M = null;
            _cosyVoice?.Dispose();
            _cosyVoice = null;
            _diffusion?.Dispose();
            _diffusion = null;
            _sana?.Dispose();
            _sana = null;
            if (_cacheLease != null)
            {
                _cacheLease.Dispose();
                return;
            }
            try
            {
                if (Directory.Exists(_cache))
                    Directory.Delete(_cache, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
