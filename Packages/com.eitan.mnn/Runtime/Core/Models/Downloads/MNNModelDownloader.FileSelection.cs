using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    public sealed partial class MNNModelDownloader
    {
        // Official MNN LLM repositories often publish conversion inputs, safetensors,
        // datasets and several documentation copies alongside the runnable export.
        // Keep the complete MNN graph family and the metadata needed by MNNLlm while
        // avoiding those unrelated blobs. Special installers retain their explicit plans.
        private static MNNModelRepositoryFile[] SelectInferenceFiles(string repository, MNNModelRepositoryFile[] files)
        {
            var blobs = (files ?? Array.Empty<MNNModelRepositoryFile>()).Where(file => file != null && string.Equals(file.Type, "blob", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (blobs.Length == 0 || IsSpecialRepository(repository) || !blobs.Any(file => IsMnnGraph(file.Path)))
                return blobs;
            var selected = blobs.Where(file => IsMnnGraph(file.Path) || IsInferenceMetadata(file.Path)).ToArray();
            // A repository with an unusual tokenizer/config layout must remain usable;
            // filtering is enabled only when the standard metadata is present.
            return HasRootFile(selected, "llm_config.json") && HasRootFile(selected, "config.json") && selected.Any(file => IsTokenizer(file.Path) && !file.Path.Contains("/")) ? selected : blobs;
        }

        private static bool HasRootFile(MNNModelRepositoryFile[] files, string name) => files.Any(file => string.Equals(file.Path?.Replace('\\', '/'), name, StringComparison.OrdinalIgnoreCase));
        private static bool IsSpecialRepository(string repository)
        {
            return MNNModelCatalog.IsPublishedSpeechRepository(repository) || MNNCosyVoiceModel.IsRepository(repository) || MNNReferenceSpeechModel.IsRepository(repository) || MNNBertEmbeddingModel.IsRepository(repository) || MNNPaddleTextModel.IsRepository(repository) || MNNQwen3AsrModel.IsRepository(repository) || MNNParaformerModel.IsHuggingFaceRepository(repository) || MNNWhisperModel.IsRepository(repository) || MNNSileroModel.IsRepository(repository) || MNNSmartTurnModel.IsRepository(repository) || MNNKwsModel.IsRepository(repository);
        }

        private static bool IsMnnGraph(string path)
        {
            string name = Path.GetFileName(path ?? string.Empty);
            return name.EndsWith(".mnn", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".mnn.weight", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".mnn.json", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTokenizer(string path)
        {
            string name = Path.GetFileName(path ?? string.Empty).ToLowerInvariant();
            return name == "tokenizer.json" || name == "tokenizer.txt" || name == "tokenizer.model" || name == "tokenizer.mtok" || name == "vocab.txt" || name == "vocab.json";
        }

        private static bool IsInferenceMetadata(string path)
        {
            string name = Path.GetFileName(path ?? string.Empty).ToLowerInvariant();
            return IsTokenizer(path) || name == "config.json" || name == "llm_config.json" || name == "configuration.json" || name == "generation_config.json" || name == "tokenizer_config.json" || name == "special_tokens_map.json" || name == "added_tokens.json" || name == "merges.txt" || name == "export_args.json" || name.EndsWith(".mtok", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) || name == "readme.md" || name == "license";
        }
    }
}
