using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MNN.Unity;
using UnityEngine;

namespace MNN.Unity
{
    public sealed partial class MNNConversationModelCatalog
    {
        private readonly List<string> _warnings = new List<string>();
        private bool _discoverySucceeded;
        public IReadOnlyList<string> DiscoveryWarnings { get { lock (_sync) return _warnings.ToArray(); } }
        public bool DiscoverySucceeded { get { lock (_sync) return _discoverySucceeded; } }

        public void ApplyOnlineModels(IEnumerable<MNNRemoteModel> models, IEnumerable<string> warnings)
        {
            var snapshots = models.Select(MNNRemoteModelSnapshot.Copy).ToArray();
            var warningSnapshot = warnings.ToArray();
            lock (_sync)
            {
                foreach (var model in snapshots)
                {
                    var entry = AddCatalogModelCore(model);
                    if (entry == null) continue;
                    // Refresh metadata while preserving built-in IDs and locally selected aliases.
                    entry.online = true;
                }
                _warnings.Clear();
                _warnings.AddRange(warningSnapshot);
            }
        }

        public MNNConversationModelCatalog()
        {
            AddDefaults();
        }

        public Entry AddRemote(MNNRemoteModel model)
        {
            model = MNNRemoteModelSnapshot.Copy(model);
            if (!TryGetAdapter(model, out var adapter)) return null;
            lock (_sync)
            {
                string repository = model.repositoryId ?? model.modelScopeId;
                _index.TryGetValue(MNNModelRepositoryIdentity.FromModel(model), out var existing);
                if (existing != null) return Copy(existing);
                var snapshot = MNNRemoteModelSnapshot.Copy(model);
                var result = new Entry
                {
                    modelId = model.provider + ":" + repository + "@" + (model.revision ?? DefaultRevision(model.provider)),
                    repository = repository,
                    revision = model.revision,
                    adapter = adapter,
                    bytes = model.fileSize,
                    modelScope = model.provider == MNNModelSource.ModelScope,
                    remote = snapshot
                };
                AddEntry(result);
                return Copy(result);
            }
        }

        public static string DefaultRevision(MNNModelSource source) => source == MNNModelSource.ModelScope ? "master" : "main";

        public static string DefaultInstallationName(MNNRemoteModel model) => model != null && model.provider == MNNModelSource.ModelScope &&
            MNNEnglishKwsModel.IsModelScopeRepository(model.repositoryId ?? model.modelScopeId)
            ? MNNKeywordSpotterConfig.EnglishDirectoryName : model != null && model.provider == MNNModelSource.ModelScope &&
            MNNPocketModel.IsRepository(model.repositoryId ?? model.modelScopeId) && model.revision == MNNPocketModel.Revision
            ? MNNPocketModel.InstallationName : DtlnProfile(model)?.name ?? Defaults.OrderByDescending(known => known.Id == model.name).FirstOrDefault(known =>
            known.Source == model.provider && string.Equals(known.Repository, model.repositoryId ?? model.modelScopeId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(known.Revision, model.revision, StringComparison.Ordinal))?.Id;

        public static bool TryGetAdapter(MNNRemoteModel model, out MNNConversationModelAdapter adapter)
        {
            adapter = default;
            if (!IsCatalogModel(model) || !model.runtimeAvailable) return false;
            string repository = model.repositoryId ?? model.modelScopeId;
            // Speech graphs have task-specific tokenizers, preprocessing and installation plans.
            // Match registered export protocols, never infer an adapter from a display name.
            foreach (var known in Defaults)
            {
                if (known.Source == model.provider && string.Equals(known.Repository, repository, StringComparison.OrdinalIgnoreCase))
                {
                    adapter = known.Adapter;
                    return true;
                }
            }
            if (IsDtlnModel(model)) { adapter = MNNConversationModelAdapter.DtlnAec; return true; }
            switch (model.provider + ":" + repository.ToLowerInvariant())
            {
                case "ModelScope:dr3334/kokoro-82m-v1.0-mnn":
                case "HuggingFace:wangjazz/kokoro-82m-mnn":
                case "ModelScope:kindbrave/kokoro-multi-lang-v1_1-mnn-custom":
                case "HuggingFace:kindbrave/kokoro-multi-lang-v1_1-mnn-custom":
                case "ModelScope:starsharp06sharp/kokoro-multi-lang-v1.1-mnn-multilingual":
                case "HuggingFace:starsharp06sharp/kokoro-multi-lang-v1.1-mnn-multilingual":
                    adapter = MNNConversationModelAdapter.Kokoro; return true;
                case "ModelScope:dr3334/supertonic-3-int8-mnn":
                    adapter = MNNConversationModelAdapter.Supertonic; return true;
                case "ModelScope:kindbrave/bert-vits2-mnn-custom":
                case "HuggingFace:kindbrave/bert-vits2-mnn-custom":
                    adapter = MNNConversationModelAdapter.BertVits2; return true;
                case "ModelScope:kindbrave/sherpa-vits-zh-ll-mnn-custom":
                    adapter = MNNConversationModelAdapter.SherpaVitsChinese; return true;
                case "ModelScope:zhangzes/moss-tts-nano-mnn":
                    adapter = MNNConversationModelAdapter.MossNano; return true;
                case "ModelScope:kindbrave/speech_paraformer-large_asr_nat-zh-cn-16k-common-vocab8404-online-mnn":
                case "HuggingFace:guoera/speech_paraformer-large_asr_nat-zh-cn-16k-common-vocab8404-online-mnn":
                    adapter = MNNConversationModelAdapter.Paraformer; return true;
                case "HuggingFace:xinliu/sensevoice_mnn":
                    adapter = MNNConversationModelAdapter.SenseVoice; return true;
                case "HuggingFace:hb-dev/qwen3-asr-0.6b-int8-mnn":
                case "ModelScope:huangzhengxiang/qwen3-asr-0.6b-int8-mnn":
                    adapter = MNNConversationModelAdapter.Qwen3Asr; return true;
            }
            if (model.category == MNNModelCatalogCategory.LargeLanguageModel.ToString() ||
                model.category == MNNModelCatalogCategory.TextGeneration.ToString() ||
                model.category == MNNModelCatalogCategory.CodeModel.ToString() ||
                model.category == MNNModelCatalogCategory.VisionLanguageModel.ToString() ||
                model.category == MNNModelCatalogCategory.OmniModel.ToString() ||
                model.category == MNNModelCatalogCategory.AudioLanguageModel.ToString())
            {
                // Discovery checked the root configuration, tokenizer and graph. Loading still
                // validates architecture/operators and the requested audio capabilities.
                adapter = model.category == MNNModelCatalogCategory.OmniModel.ToString() ||
                    model.category == MNNModelCatalogCategory.AudioLanguageModel.ToString()
                    ? MNNConversationModelAdapter.Omni : MNNConversationModelAdapter.Language;
                return true;
            }
            if (model.category == MNNModelCatalogCategory.VoiceActivityDetection.ToString())
            { adapter = MNNConversationModelAdapter.VoiceActivity; return true; }
            if (model.category == MNNModelCatalogCategory.TurnDetection.ToString())
            { adapter = model.requiresTranscript ? MNNConversationModelAdapter.TextTurn : MNNConversationModelAdapter.AudioTurn; return true; }
            if (MNNSpeechSynthesizer.SupportsRepository(model))
            { adapter = MNNConversationModelAdapter.SpeechSynthesis; return true; }
            if (MNNSpeechRecognizer.SupportsRepository(model))
            { adapter = MNNConversationModelAdapter.SpeechRecognition; return true; }
            if (model.category == MNNModelCatalogCategory.SpeechEnhancement.ToString())
            { adapter = MNNConversationModelAdapter.NoiseReduction; return true; }
            if (model.category == MNNModelCatalogCategory.SpeechSeparation.ToString())
            { adapter = MNNConversationModelAdapter.Separation; return true; }
            if (model.category == MNNModelCatalogCategory.SpeakerRecognition.ToString())
            { adapter = MNNConversationModelAdapter.SpeakerEmbedding; return true; }
            if (model.category == MNNModelCatalogCategory.KeywordSpotting.ToString())
            { adapter = MNNConversationModelAdapter.KeywordSpotting; return true; }
            return false;
        }

        private async Task<int> DiscoverAsync(CancellationToken token, HttpClient client)
        {
            token.ThrowIfCancellationRequested();
            var scopes = new MNNModelCatalog();
            var faces = new MNNModelCatalog();
            var failures = new string[2];
            async Task<bool> Discover(MNNModelCatalog catalog, MNNModelSource source, int index)
            {
                try
                {
                    if (source == MNNModelSource.ModelScope)
                        await catalog.DiscoverModelScopeModelsAsync(httpClient: client, cancellationToken: token);
                    else await catalog.DiscoverHuggingFaceModelsAsync(httpClient: client, cancellationToken: token);
                    return true;
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception error) { failures[index] = source + ": " + error.Message; return false; }
            }
            var succeeded = await Task.WhenAll(Discover(scopes, MNNModelSource.ModelScope, 0), Discover(faces, MNNModelSource.HuggingFace, 1));
            token.ThrowIfCancellationRequested();
            lock (_sync)
            {
                _discoverySucceeded = succeeded.Any(success => success);
                _warnings.Clear();
                int count = 0;
                var catalogs = new[] { scopes, faces };
                for (int i = 0; i < catalogs.Length; ++i)
                {
                    if (!succeeded[i]) { _warnings.Add(failures[i]); continue; }
                    _warnings.AddRange(catalogs[i].DiscoveryWarnings);
                    foreach (var remote in catalogs[i].Models)
                    {
                        var entry = AddCatalogModelCore(remote);
                        if (entry == null) continue;
                        entry.online = true;
                        ++count;
                    }
                }
                _entries.Sort((a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.modelId, b.modelId));
                return count;
            }
        }

        private sealed class DefaultModel
        {
            internal string Id, Repository, Revision;
            internal MNNModelSource Source;
            internal MNNConversationModelAdapter Adapter;
            internal long Bytes;
        }

        private static readonly MNNRemoteModel[] DtlnProfiles = MNNModelCatalog.CreateDefault().Models.Where(
            model => model.category == MNNModelCatalogCategory.EchoCancellation.ToString()).ToArray();

        private static MNNRemoteModel DtlnProfile(MNNRemoteModel model) => model == null ? null :
            DtlnProfiles.FirstOrDefault(known => known.provider == model.provider &&
                known.repositoryId == (model.repositoryId ?? model.modelScopeId) && known.revision == model.revision);
        public static bool IsDtlnModel(MNNRemoteModel model) => DtlnProfile(model) != null;

        private void AddDefaults()
        {
            foreach (var profile in DtlnProfiles)
            {
                var remote = MNNRemoteModelSnapshot.Copy(profile);
                AddEntry(new Entry
                {
                    modelId = remote.name,
                    repository = remote.repositoryId,
                    revision = remote.revision,
                    adapter = MNNConversationModelAdapter.DtlnAec,
                    bytes = remote.fileSize,
                    modelScope = true,
                    remote = remote
                });
            }
            foreach (var known in Defaults)
            {
                var remote = new MNNRemoteModel
                {
                    id = known.Repository,
                    repositoryId = known.Repository,
                    provider = known.Source,
                    revision = known.Revision,
                    name = known.Id,
                    displayName = known.Id,
                    isRepository = true,
                    runtimeAvailable = true,
                    fileSize = known.Bytes,
                    supportSummary = "Built-in runtime export profile."
                };
                AddEntry(new Entry
                {
                    modelId = known.Id,
                    repository = known.Repository,
                    revision = known.Revision,
                    adapter = known.Adapter,
                    bytes = known.Bytes,
                    modelScope = known.Source == MNNModelSource.ModelScope,
                    remote = remote
                });
            }
            foreach (var remote in MNNModelCatalog.CreateDefault().Models) AddCatalogModel(remote);
        }

        // Metadata only: installation, hashes, extraction and configuration belong to MNNModelDownloader.
        private static readonly DefaultModel[] Defaults =
        {
            new DefaultModel { Id = MNNPocketModel.ModelId, Repository = MNNPocketModel.Repository, Revision = MNNPocketModel.Revision, Source = MNNModelSource.ModelScope, Adapter = MNNConversationModelAdapter.Language, Bytes = MNNPocketModel.Bytes },
            new DefaultModel { Id = "KindBrave-paraformer-online", Repository = "KindBrave/speech_paraformer-large_asr_nat-zh-cn-16k-common-vocab8404-online-mnn", Revision = "b6b9dd4471cffd6b59f283b2998284d38f4d31e0", Source = MNNModelSource.HuggingFace, Adapter = MNNConversationModelAdapter.Paraformer, Bytes = 865040775L },
            new DefaultModel { Id = "bert-vits2-MNN", Repository = "taobao-mnn/bert-vits2-MNN", Revision = "cec57c91063c58ae2c5f705b669b57988f772d4a", Source = MNNModelSource.HuggingFace, Adapter = MNNConversationModelAdapter.BertVits2, Bytes = 1392019964L },
            new DefaultModel { Id = "Cosy-Voice-MNN", Repository = "VicenTrent/Cosy-Voice-MNN", Revision = "948b6a2600df25d9d01960a6fa34c126c02b66da", Source = MNNModelSource.HuggingFace, Adapter = MNNConversationModelAdapter.CosyVoice, Bytes = 2798165016L },
            new DefaultModel { Id = "Hunyuan-1.8B-Instruct-MNN", Repository = "taobao-mnn/Hunyuan-1.8B-Instruct-MNN", Revision = "da12899a30fec71a9bf4edf703edac72df542fb7", Source = MNNModelSource.HuggingFace, Adapter = MNNConversationModelAdapter.Language, Bytes = 1246079496L },
            new DefaultModel { Id = "MossNano", Repository = "kiven2008/moss-tts-nano-100m-mnn", Revision = "8439c3521b301f2ad57a3ffe5a0fb3fcbef3881e", Source = MNNModelSource.ModelScope, Adapter = MNNConversationModelAdapter.MossNano, Bytes = 1184247683L },
            new DefaultModel { Id = "Qwen2.5-Omni-3B-MNN", Repository = "taobao-mnn/Qwen2.5-Omni-3B-MNN", Revision = "00dc2e9131a4bb325b43a47f4210dd6450116687", Source = MNNModelSource.HuggingFace, Adapter = MNNConversationModelAdapter.Omni, Bytes = 4013158356L },
            new DefaultModel { Id = "PiperLessac", Repository = "ekremabiii/pipertts_mnn", Revision = "56874de93c7d2908d7c384d656ddadea34ef2870", Source = MNNModelSource.HuggingFace, Adapter = MNNConversationModelAdapter.Piper, Bytes = 114716576L },
            new DefaultModel { Id = "wo105090--SenseVoiceSmall-MNN", Repository = "wo105090/SenseVoiceSmall-MNN", Revision = "e7809552caf44ef8e40f2f0e086fb4037d1e3b88", Source = MNNModelSource.ModelScope, Adapter = MNNConversationModelAdapter.SenseVoice, Bytes = 137002375L },
            new DefaultModel { Id = "sherpa-vits-zh-ll", Repository = "KindBrave/sherpa-vits-zh-ll-MNN-Custom", Revision = "debc94f4d18e3783ec0d054a8c7401b4c064c511", Source = MNNModelSource.HuggingFace, Adapter = MNNConversationModelAdapter.SherpaVitsChinese, Bytes = 32641251L },
            new DefaultModel { Id = "Jeryuk--kokoro-v1_0-MNN", Repository = "Jeryuk/kokoro-v1_0-MNN", Revision = "0a8c2982a6fad791eb39746f83ef9482252ceca6", Source = MNNModelSource.HuggingFace, Adapter = MNNConversationModelAdapter.Kokoro, Bytes = 109372470L },
            new DefaultModel { Id = "Supertonic-3-INT8-MNN", Repository = "yunfengwang/supertonic-tts-mnn", Revision = "c0425ea0b5884cdb9c23a99b2373dcdb47f142fa", Source = MNNModelSource.HuggingFace, Adapter = MNNConversationModelAdapter.Supertonic, Bytes = 111036647L },
            new DefaultModel { Id = "supertonic-tts-mnn", Repository = "yunfengwang/supertonic-tts-mnn", Revision = "c0425ea0b5884cdb9c23a99b2373dcdb47f142fa", Source = MNNModelSource.HuggingFace, Adapter = MNNConversationModelAdapter.Supertonic, Bytes = 134056954L },
            new DefaultModel { Id = "Qwen3.5-0.8B-MNN", Repository = "MNN/Qwen3.5-0.8B-MNN", Revision = "594e8d3c5dcdd8ae7ff488a3ba5920c503721fe6", Source = MNNModelSource.ModelScope, Adapter = MNNConversationModelAdapter.Language, Bytes = 484349697L },
            new DefaultModel { Id = "SmartTurn-v3.2-MNN", Repository = "Jeryuk/smart-turn-v3.2-gpu-MNN", Revision = "1cc21f7b6f904eb1a5daadc27c6f4931d1f78794", Source = MNNModelSource.HuggingFace, Adapter = MNNConversationModelAdapter.SmartTurn, Bytes = 9061752L },
            new DefaultModel { Id = "Silero-v5-MNN", Repository = "Jeryuk/silero_vad-MNN", Revision = "e2502b184542017860ab1ca83c18f8742b221ecd", Source = MNNModelSource.HuggingFace, Adapter = MNNConversationModelAdapter.Silero, Bytes = 1486656L },
            new DefaultModel { Id = "Silero-v5.1-MNN", Repository = "EitanWang/Silero-v5.1-MNN", Revision = "master", Source = MNNModelSource.ModelScope, Adapter = MNNConversationModelAdapter.Silero, Bytes = 1266761L },
        };
    }
}
