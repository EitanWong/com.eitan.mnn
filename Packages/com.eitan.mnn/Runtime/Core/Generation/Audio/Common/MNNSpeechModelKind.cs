using System;
using System.IO;

namespace MNN.Unity
{
    /// <summary>
    /// Describes the managed TTS frontends that are currently implemented by the
    /// package. A repository being classified as SpeechSynthesis is not enough to
    /// select a frontend: each graph family has a different tokenizer and tensor
    /// contract.
    /// </summary>
    public enum MNNSpeechModelKind
    {
        Unknown,
        Supertonic,
        Piper,
        Kokoro,
        IndicVits,
        BertVits2,
        CosyVoice,
        IndexTts25,
        FireRedTts3,
        MmsTamil,
        SherpaVitsChinese,
        MossNano,
        DhVaani,
        Qwen3Explicit,
        Pocket,
        IndicF5,
        CosyVoice300M
    }
}
