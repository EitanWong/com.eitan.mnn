using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNNamoTurnDetectorConfig
    {
        public const string FileName = "mnn_namo_turn_detector.json";
        public string repository, revision, source, graph, tokenizer, input_ids, attention_mask, output;
        public int maximum_tokens, end_of_turn_class;

        public static MNNNamoTurnDetectorConfig Read(string directory) =>
            JsonUtility.FromJson<MNNNamoTurnDetectorConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid NAMO turn detector profile.");
    }
}
