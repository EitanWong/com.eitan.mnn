namespace MNN.Unity
{
    internal sealed class MNNFsEendProfile
    {
        private static readonly MNNFsEendProfile Simu = new MNNFsEendProfile("FS-EEND-Simu-8K-MNN",
            "172fa6ee613476dac0daa5cfc14671faeed68a4c29d5db313768a7e857e3fa3d",
            "77e706ed223fbf6b790daca598852aed8eb1a562710b511c874b445f22651721", 39670976);
        private static readonly MNNFsEendProfile Callhome = new MNNFsEendProfile("FS-EEND-CALLHOME-8K-MNN",
            "eda37490029b9112d5a40849e2e825404ec55e12671dd982d07b81780339c949",
            "77e68536a6185ba40dd0f771001090102f5313581413e1f15bf02c7d2f746433", 39670980);

        internal string Name { get; }
        internal string CheckpointSha256 { get; }
        internal string GraphSha256 { get; }
        internal long GraphBytes { get; }

        private MNNFsEendProfile(string name, string checkpoint, string graph, long graphBytes)
        {
            Name = name;
            CheckpointSha256 = checkpoint;
            GraphSha256 = graph;
            GraphBytes = graphBytes;
        }

        internal static MNNFsEendProfile Find(string checkpoint)
        {
            if (checkpoint == Simu.CheckpointSha256) return Simu;
            if (checkpoint == Callhome.CheckpointSha256) return Callhome;
            return null;
        }
    }
}
