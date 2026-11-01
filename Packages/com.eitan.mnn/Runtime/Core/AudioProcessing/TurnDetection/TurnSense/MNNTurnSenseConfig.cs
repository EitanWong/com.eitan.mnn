using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNTurnSenseConfig
    {
        public const string FileName = "mnn_turnsense.json";
        public string repository, revision, source_sha256, graph, tokenizer, prefix, output;
        public int maximum_tokens, pad_token_id, end_of_utterance_class;

        public static MNNTurnSenseConfig Read(string directory) =>
            JsonUtility.FromJson<MNNTurnSenseConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid TurnSense profile.");
    }
}
