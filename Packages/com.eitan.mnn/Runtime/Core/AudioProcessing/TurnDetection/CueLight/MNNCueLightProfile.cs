namespace MNN.Unity
{
    internal sealed class MNNCueLightProfile
    {
        internal readonly string Name, Repository, Revision, Checkpoint, CpuHash, MetalHash;
        internal readonly int HiddenDimension, StateSize, CpuBytes, MetalBytes;
        internal readonly double SnappyThreshold, CarefulThreshold;
        internal bool SupportsMetal => MetalHash != null;

        private MNNCueLightProfile(string name, string repository, string revision, string checkpoint,
            int hidden, int state, int cpuBytes, string cpuHash, int metalBytes, string metalHash, double snappy, double careful)
        {
            Name = name; Repository = repository; Revision = revision; Checkpoint = checkpoint;
            HiddenDimension = hidden; StateSize = state; CpuBytes = cpuBytes; CpuHash = cpuHash;
            MetalBytes = metalBytes; MetalHash = metalHash; SnappyThreshold = snappy; CarefulThreshold = careful;
        }

        private static readonly MNNCueLightProfile Nano = new MNNCueLightProfile("CueLight-Nano-MNN", "sshah03/cuelight-nano",
            "8bc69fe809cd9b6968e65f3bd00439724b5a7c67", "4d4ec1abb7d32d6268035117d0ab93116f145cdb55e510d3c71450ecd0adf841",
            112, 112, 1993228, "e8e7dc004c9decc48c25cc4b94b25c2c94d5b35461410bc78ca1619e7a0112d3",
            2000936, "d73504f638574627ea3fa3da2d7074948357219536c5401dc476a455a7be52a3", .73, .76);
        private static readonly MNNCueLightProfile NanoSsm = new MNNCueLightProfile("CueLight-Nano-SSM-MNN", "sshah03/cuelight-nano-ssm",
            "4f686d9a0c6679c4c1ead32e0627ce60a46f2f72", "ed37719c0017bde5a07f285a39a915aa44d3730da7924ed858553ff9de27addc",
            106, 13886, 2007084, "8d540a682838c75c8c02b5c6995c5ef23c96c0a084b5257739177be876c396e7",
            0, null, .8, .8);

        internal static MNNCueLightProfile Find(string repository) =>
            repository == Nano.Repository ? Nano : repository == NanoSsm.Repository ? NanoSsm : null;
    }
}
