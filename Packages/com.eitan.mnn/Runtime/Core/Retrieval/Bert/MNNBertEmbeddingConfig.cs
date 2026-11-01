using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    public enum MNNBertPooling
    {
        Cls,
        Mean
    }

    /// <summary>Explicit graph/tokenizer contract for a BERT WordPiece sentence encoder.</summary>
    [Serializable]
    public sealed class MNNBertEmbeddingConfig
    {
        public const string FileName = "mnn_embedding.json";
        public string graph;
        public string vocabulary = "vocab.txt";
        public string tokenizer = "tokenizer.json";
        public string inputIds = "input_ids";
        public string attentionMask = "attention_mask";
        public string tokenTypes = "token_type_ids";
        public string output = "last_hidden_state";
        public int dimension = 384;
        public int maximumTokens = 512;
        public MNNBertPooling pooling = MNNBertPooling.Cls;
        public bool normalize = true;
        public bool lowerCase = true;
        public string queryPrefix;

        public static MNNBertEmbeddingConfig Read(string directory)
        {
            string profile = Path.Combine(directory, FileName);
            if (File.Exists(profile))
                return JsonUtility.FromJson<MNNBertEmbeddingConfig>(File.ReadAllText(profile)) ?? throw new InvalidDataException("Invalid BERT embedding profile.");
            // This publisher explicitly supplies the graph, pooling and input/output protocol.
            var config = MNNModelData.Object(MNNModelData.Read(Path.Combine(directory, "config.json")));
            if (!config.TryGetValue("model_type", out var type) || (string)type != "bert" || !config.ContainsKey("mnn_model") || !config.ContainsKey("pooling_type"))
                throw new InvalidDataException("BERT graph embeddings require an explicit " + FileName + " profile.");
            string pooling = (string)config["pooling_type"];
            if (pooling != "cls" && pooling != "mean")
                throw new NotSupportedException("Unsupported BERT pooling: " + pooling);
            return new MNNBertEmbeddingConfig
            {
                graph = (string)config["mnn_model"],
                dimension = MNNModelData.Integer(MNNModelData.Get(config, "embedding_dim")),
                maximumTokens = MNNModelData.Integer(MNNModelData.Get(config, "max_position_embeddings")),
                normalize = config.TryGetValue("normalize", out var normalize) && normalize is bool value && value,
                pooling = pooling == "cls" ? MNNBertPooling.Cls : MNNBertPooling.Mean
            };
        }

        public static bool IsConfigured(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                return false;
            if (File.Exists(Path.Combine(directory, FileName)))
                return true;
            string config = Path.Combine(directory, "config.json");
            if (!File.Exists(config))
                return false;
            var data = MNNModelData.Object(MNNModelData.Read(config));
            return data.TryGetValue("model_type", out var type) && type as string == "bert" && data.ContainsKey("mnn_model") && data.ContainsKey("pooling_type");
        }

        internal MNNBertEmbeddingConfig Clone() => JsonUtility.FromJson<MNNBertEmbeddingConfig>(JsonUtility.ToJson(this));

        internal static string ResolveFile(string directory, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative))
                throw new InvalidDataException("Embedding profile paths must be relative to the model directory.");
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, relative));
            if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
                throw new FileNotFoundException("Missing embedding model file: " + relative);
            return path;
        }
    }
}
