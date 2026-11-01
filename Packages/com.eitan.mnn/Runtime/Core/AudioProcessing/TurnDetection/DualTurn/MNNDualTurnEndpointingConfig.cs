using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNDualTurnEndpointingConfig
    {
        public const string FileName = "mnn_dualturn_endpointing.json";
        public string repository, revision, source_sha256, graph, weights;
        public int sample_rate, tick_samples, audio_context, kv_frames, transformer_history;

        public static MNNDualTurnEndpointingConfig Read(string directory)
        {
            return JsonUtility.FromJson<MNNDualTurnEndpointingConfig>(
                File.ReadAllText(Path.Combine(directory, FileName)))
                ?? throw new InvalidDataException("Invalid DualTurn endpointing profile.");
        }
    }
}
