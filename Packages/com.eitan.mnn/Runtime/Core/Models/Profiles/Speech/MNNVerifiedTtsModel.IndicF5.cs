using System;

namespace MNN.Unity
{
    internal static partial class MNNVerifiedTtsModel
    {
        internal const string IndicF5Repository = "developerabu/IndicF5-MNN";
        internal const string IndicF5Revision = "72f3cd93d93c3bbff4091ae2ab1d351e6d68889c";
        internal const string IndicF5FrontendRepository = "mshdev/IndicF5-reelgen";
        internal const string IndicF5FrontendRevision = "125ce96087918bf4f815b5b9de6ae3579ebe991f";
        internal static bool IsIndicF5Repository(string repository) => string.Equals(repository, IndicF5Repository, StringComparison.OrdinalIgnoreCase);
        internal static FileEntry[] IndicF5Files() => new[]
        {
            new FileEntry("indicf5_dit_int8wq.mnn", 346515768L, "7d19cdb4ef19b6209b0945c900f285031c2c36882256ec10b4afeb0f45f460dd", IndicF5Revision),
            new FileEntry("indicf5_vocos_fp32_dynamic.mnn", 62580312L, "574450cfc77f08ca76c08f876b34a5b241ddfa8c4d2f8f7d93b53d53fcfdbf61", IndicF5Revision),
            new FileEntry("checkpoints/vocab.txt", 11283L, "d3a5ff6aac12ea4fb50628a66a97b20c1e83b9e5ca356e5afae17b511cda96df", IndicF5FrontendRevision, IndicF5FrontendRepository),
            new FileEntry("LICENSE", 1066L, "3b92f014d3d6361f1c76566750ccc143f50a966ad1e3f17882b7e4f7f614415c", IndicF5FrontendRevision, IndicF5FrontendRepository),
            new FileEntry("vocos/config.yaml", 461L, "da9033922f969a47f0c160010226919e59f27761fd5066f3828d46de6650b0fc", IndicF5FrontendRevision, IndicF5FrontendRepository),
            new FileEntry("samples/hi_short_mnn_vocos_fp32.wav", 73260L, "bba311adbb98af7f07f45a35c173c641080eef5f8764d07b8335897c42b0e32a", IndicF5Revision),
            new FileEntry("README.md", 1438L, "5e0d98eb93419a7be8b1118a678e6e44ae2ca16a989524b2d735cd4f01274f7e", IndicF5Revision),
        };
    }
}
