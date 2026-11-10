using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>The pinned Chinese/English Zipformer2 KWS graph protocol.</summary>
    [Serializable]
    public sealed class MNNKeywordSpotterConfig
    {
        public const string FileName = "mnn_kws.json";
        public string repository = MNNKwsModel.Repository;
        public const string EnglishModelScopeRepository = MNNEnglishKwsModel.ModelScopeRepository;
        public const string EnglishDirectoryName = "English-KWS-GigaSpeech";
        public const string EnglishDefaultWords = "Siri\nHey Siri";
        public MNNKeywordLanguage Language => MNNEnglishKwsModel.IsRepository(repository) ? MNNKeywordLanguage.English : MNNKeywordLanguage.Chinese;

        public static MNNKeywordSpotterConfig Read(string directory) =>
            JsonUtility.FromJson<MNNKeywordSpotterConfig>(File.ReadAllText(Path.Combine(directory, FileName)))
            ?? throw new InvalidDataException("Invalid KWS profile.");
    }
}
