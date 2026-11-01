using System;

namespace MNN.Unity
{
    internal static partial class MNNVerifiedTtsModel
    {
        internal const string PiperLessacRepository = "ekremabiii/pipertts_mnn";
        internal const string PiperLessacRevision = "56874de93c7d2908d7c384d656ddadea34ef2870";
        internal const string PiperDataRepository = "csukuangfj/vits-piper-en_US-lessac-high";
        internal const string PiperDataRevision = "a3cffbe82b4df3b9edefa73189f4443ed638d9a1";
        internal static bool IsPiperLessacRepository(string repository) => string.Equals(repository, PiperLessacRepository, StringComparison.OrdinalIgnoreCase);
        internal static FileEntry[] PiperLessacFiles() => new[]
        {
            new FileEntry("config.json", 147L, "28d00394cb26505c3344704b4476bb9d5a1b84d75d54140117de4e0a7476d936", PiperLessacRevision),
            new FileEntry("en_US-lessac-high.mnn", 113896524L, "d7353e9855bfc16dbd0bb8f3422b8cb1b6015da696400f3619dd7bde11864975", PiperLessacRevision),
            new FileEntry("assets/en_US-lessac-high.onnx.json", 4883L, "db42b97d9859f257bc1561b8ed980e7fb2398402050a74ddd6cbec931a92412f", PiperLessacRevision),
            new FileEntry("MODEL_CARD", 347L, "7671826d947a0ffc11dd76af0bd890d93e956b00358696ad71348f21aa827100", PiperDataRevision, PiperDataRepository),
            new FileEntry("espeak-ng-data/phontab", 55796L, "886f3fa402cb0ba73d483aa8ad000af47a6b7cc06293c75a97913fba68a530f6", PiperDataRevision, PiperDataRepository),
            new FileEntry("espeak-ng-data/phondata", 550424L, "4e0288957874029a8c3c9f41a8f517ad4bf18127046decbdd4b9d1d6807ce3a3", PiperDataRevision, PiperDataRepository),
            new FileEntry("espeak-ng-data/phonindex", 39074L, "3ca7b8fa3b42624e4b0f152707e7a39245fce569aa99ea47c055d9e622fcf0c4", PiperDataRevision, PiperDataRepository),
            new FileEntry("espeak-ng-data/intonations", 2040L, "3f8af65fd3eda9759a10f021d61361c120871f463515229c925995c7f90918cc", PiperDataRevision, PiperDataRepository),
            new FileEntry("espeak-ng-data/en_dict", 166944L, "71bd330ba8a2e3e8076e631508208ef49449d6147c17b7bd2b4b1e1468292e35", PiperDataRevision, PiperDataRepository),
            new FileEntry("espeak-ng-data/lang/gmw/en", 140L, "4605d5330801de3641c6e366d15f129ea1f5ffbce8722642aba01ace07ab9c83", PiperDataRevision, PiperDataRepository),
            new FileEntry("espeak-ng-data/lang/gmw/en-US", 257L, "41534c2a22df5dd4f1052ff9e1a33a3ea7bff5a26b5c02bdad5ba8ddb7524704", PiperDataRevision, PiperDataRepository),
        };
    }
}
