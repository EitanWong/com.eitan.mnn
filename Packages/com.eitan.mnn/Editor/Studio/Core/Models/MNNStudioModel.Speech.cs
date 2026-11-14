using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MNN.Unity.Editor
{
    internal sealed partial class MNNStudioModel
    {
        private readonly Lazy<string> _snapshotSupertonicPrecision;
        internal string SupertonicPrecision => _snapshotSupertonicPrecision.Value;
        private string ReadSupertonicPrecision() => new[]{"fp16", "fp32", "int8"}.FirstOrDefault(precision => new[]{"duration_predictor", "text_encoder", "vector_estimator", "vocoder"}.All(stage => File.Exists(Path.Combine(Directory, "mnn_models", precision, stage + ".mnn"))));
        internal string MmsTamilPrecision = "int8";
        internal bool IsSana => _taskConfig?.model_name == "Sana-Distill-v2";

        internal bool IsPiper => _taskConfig?.model_type == "piper";

        internal string PiperDataDirectory => MNNPiper.ResolveDataDirectory(Directory, ReadTaskConfig()?.asset_folder);
        internal bool IsBertVits2 => ReadTaskConfig()?.model_type == "bertvits";
        internal bool IsCosyVoice300M => TtsProfile == MNNSpeechModelKind.CosyVoice300M;
        private readonly Lazy<bool> _snapshotIsCosyVoice;
        internal bool IsCosyVoice => _snapshotIsCosyVoice.Value;
        private readonly Lazy<bool> _snapshotIsIndexTts;
        internal bool IsIndexTts => _snapshotIsIndexTts.Value;
        private readonly Lazy<bool> _snapshotIsFireRedTts;
        internal bool IsFireRedTts => _snapshotIsFireRedTts.Value;
        internal bool IsQwen3Explicit => TtsProfile == MNNSpeechModelKind.Qwen3Explicit;
        internal bool IsIndicF5 => TtsProfile == MNNSpeechModelKind.IndicF5;
        internal bool IsPocket => TtsProfile == MNNSpeechModelKind.Pocket;
        internal bool IsDhVaani => TtsProfile == MNNSpeechModelKind.DhVaani;
        internal bool RequiresReferenceTranscript => IsFireRedTts || IsDhVaani || IsIndicF5;
        internal bool RequiresReferenceAudio => IsIndexTts || IsFireRedTts || IsDhVaani || IsPocket || IsIndicF5;
        internal MNNSpeechModelKind TtsProfile
        {
            get
            {
                if (!_ttsProfile.HasValue)
                    _ttsProfile = MNNSpeechSynthesizer.DetectModelKind(Directory, Name);
                return _ttsProfile.Value;
            }
        }

        internal string[] SpeechLanguages
        {
            get
            {
                if (_speechLanguages != null)
                    return _speechLanguages;
                if (IsIndicF5)
                    return _speechLanguages = new[]{"hi", "ta"};
                if (IsPocket)
                    return _speechLanguages = new[]{"en"};
                if (TtsProfile != MNNSpeechModelKind.Supertonic)
                    return _speechLanguages = new[]{"zh", "en"};
                try
                {
                    return _speechLanguages = MNNSpeechSynthesizer.InstalledSupertonicLanguages(Directory);
                }
                catch (Exception error)when (error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
                {
                    return _speechLanguages = new[]{"en"};
                }
            }
        }

        private Config ReadTaskConfig() => _taskConfig;

        private Config ReadTaskConfigFromDisk()
        {
            try
            {
                string path = Path.Combine(Directory, "config.json");
                return File.Exists(path) ? JsonUtility.FromJson<Config>(File.ReadAllText(path)) : null;
            }
            catch (Exception error)when (error is IOException || error is ArgumentException)
            {
                return null;
            }
        }

        internal string[] TtsVoices => (string[])(_ttsVoices ?? (_ttsVoices = ReadTtsVoices())).Clone();
        private string[] ReadTtsVoices() => MNNSpeechSynthesizer.InstalledVoices(Directory);
    }
}
