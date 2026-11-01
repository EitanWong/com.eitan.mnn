using System;
using System.Collections.Generic;
using System.IO;
using MNN.Unity.Interop;
using MNN.Unity.Interop.Handles;
using UnityEngine;

namespace MNN.Unity
{
    // MNN 3.6.2 emits a scalar causal mask for text embeddings. Older exports
    // with kv_cache=false interpret that scalar as unmasked. The official
    // Embedding API resets its KV state before each independent encode.
    // Change only public serialized Attention parameters in an owned copy.
    internal sealed class MNNEmbeddingGraph : IDisposable
    {
        private string _directory;
        private string _relativeGraph;
        private bool _prepared;

        [Serializable]
        private sealed class ModelOverride
        {
            public string llm_model;
        }

        internal EmbeddingHandle Create(string config, string options)
        {
            if (!_prepared)
            {
                PrepareFile(config);
                _prepared = true;
            }
            if (_relativeGraph != null)
            {
                string extra = JsonUtility.ToJson(new ModelOverride { llm_model = _relativeGraph });
                options = options.TrimEnd();
                options = options.Substring(0, options.Length - 1) + "," + extra.Substring(1);
            }
            return MNNInterop.MNN_Embedding_create(config, options);
        }

        private void PrepareFile(string configPath)
        {
            var config = MNNModelData.Object(MNNModelData.Read(configPath));
            string baseDirectory = Text(config, "base_dir", Path.GetDirectoryName(configPath) + Path.DirectorySeparatorChar);
            string metadataPath = baseDirectory + Text(config, "llm_config", "llm_config.json");
            if (File.Exists(metadataPath))
                foreach (var pair in MNNModelData.Object(MNNModelData.Read(metadataPath))) config[pair.Key] = pair.Value;
            if (Text(config, "attention_mask", "int") != "float") return;
            string sourcePath = Path.GetFullPath(baseDirectory + Text(config, "llm_model", "llm.mnn"));
            byte[] source = File.ReadAllBytes(sourcePath);
            byte[] compatible = Prepare(source);
            if (ReferenceEquals(source, compatible)) return;
            _directory = Path.Combine(MNNInferenceCache.DirectoryFor("EmbeddingGraphs"), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            string destination = Path.Combine(_directory, "embedding.mnn");
            File.WriteAllBytes(destination, compatible);
            var origin = new Uri(Path.GetFullPath(baseDirectory));
            _relativeGraph = Uri.UnescapeDataString(origin.MakeRelativeUri(new Uri(destination)).ToString());
        }

        private static string Text(Dictionary<string, object> config, string key, string fallback)
        {
            if (!config.TryGetValue(key, out var value)) return fallback;
            return value as string ?? throw new InvalidDataException("Expected string model setting: " + key);
        }

        internal static byte[] Prepare(byte[] source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var reader = new MNNSerializedGraph.Reader(source);
            int net = reader.Int(0);
            var fields = new List<int>();
            FindLegacyAttention(source, reader, reader.Field(net, 3), fields);
            int subgraphsField = reader.Field(net, 10);
            if (subgraphsField != 0)
            {
                int graphs = reader.Follow(subgraphsField);
                for (int i = 0; i < reader.Count(graphs); ++i)
                {
                    int graph = reader.Follow(graphs + 4 + i * 4);
                    FindLegacyAttention(source, reader, reader.Field(graph, 4), fields);
                }
            }
            if (fields.Count == 0) return source;
            var result = (byte[])source.Clone();
            foreach (int field in fields) result[field] = 1;
            return result;
        }

        private static void FindLegacyAttention(byte[] source, MNNSerializedGraph.Reader reader, int field, List<int> fields)
        {
            if (field == 0) return;
            int operations = reader.Follow(field);
            for (int i = 0; i < reader.Count(operations); ++i)
            {
                int op = reader.Follow(operations + 4 + i * 4), type = reader.Field(op, 5);
                if (type == 0 || reader.Int(type) != 299) continue; // public OpType::Attention
                int parameterField = reader.Field(op, 2);
                if (parameterField == 0) throw new InvalidDataException("Missing MNN Attention parameters.");
                int parameter = reader.Follow(parameterField);
                int kvCache = reader.Field(parameter, 0); // public AttentionParam::kv_cache, default true
                if (kvCache < 0 || kvCache >= source.Length) throw new InvalidDataException("Invalid MNN Attention flag.");
                if (kvCache != 0 && source[kvCache] == 0) fields.Add(kvCache);
            }
        }

        public void Dispose()
        {
            MNNModelOptions.RemoveOwnedCache(_directory);
            _directory = null;
        }
    }
}
