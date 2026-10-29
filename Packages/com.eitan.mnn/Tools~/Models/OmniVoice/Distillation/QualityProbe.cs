using System;
using System.IO;
using System.Linq;
using MNN.Unity;
using UnityEngine;

public static class QualityProbe
{
    [Serializable] public sealed class Row { public string wav; public string transcript; public float speakerCosine; }
    [Serializable] public sealed class Report { public Row[] rows; }
    public static void Run()
    {
        string root = Environment.GetEnvironmentVariable("MNN_MODELS_DIRECTORY");
        string folder = Environment.GetEnvironmentVariable("OMNIVOICE_DISTILLATION_QUALITY_DIRECTORY");
        using (var asr = MNNQwen3AsrRecognizer.Load(Path.Combine(root, "hb-dev--Qwen3-ASR-0.6B-INT8-MNN")))
        using (var speaker = MNNSpeakerEmbedding.Load(Path.Combine(root, "Jeryuk--ERes2NetV2"), backendType: MNNBackendType.CPU))
        {
            var reference = speaker.EncodeWaveFile(Path.Combine(root, "OmniVoice-MNN", "zero_shot_prompt.wav"));
            var rows = Directory.GetFiles(folder, "*.wav").OrderBy(x => x).Select(path =>
            {
                var row = new Row { wav = Path.GetFileName(path), transcript = asr.RecognizeWaveFile(path).Text,
                    speakerCosine = MNNSpeakerEmbedding.Similarity(reference, speaker.EncodeWaveFile(path)) };
                Debug.Log(row.wav + " | " + row.transcript + " | speaker " + row.speakerCosine);
                return row;
            }).ToArray();
            File.WriteAllText(Path.Combine(folder, "quality.json"), JsonUtility.ToJson(new Report { rows = rows }, true));
        }
    }
}
