namespace MNN.Unity
{
    internal sealed class MNNVbxProfile
    {
        internal readonly string Name, CheckpointSha256, GraphSha256, CoefficientSha256;
        internal readonly int SampleRate, FrameSize, FrameShift, FftSize, MinimumSamples, MaximumSamples, CoefficientBytes, DefaultCpuThreads;
        private MNNVbxProfile(string name, string checkpoint, string graph, string coefficients, int rate)
        {
            Name = name; CheckpointSha256 = checkpoint; GraphSha256 = graph; CoefficientSha256 = coefficients;
            SampleRate = rate; FrameSize = rate / 40; FrameShift = rate / 100; FftSize = rate == 8000 ? 256 : 512;
            MinimumSamples = rate * 95 / 1000; MaximumSamples = rate * 10; CoefficientBytes = (FrameSize + (FftSize / 2 + 1) * 64) * 8;
            DefaultCpuThreads = rate == 8000 ? 4 : 8;
        }
        private static readonly MNNVbxProfile Wideband = new MNNVbxProfile("VBx-ResNet101-16K-MNN",
            "f550bf3d7766478a0684b8f564184db81c3346a7c99a61051f033b42b8f08373",
            "d7042c7d4c98b4f8bd77adc964c8603fe292a414f953f15207bcaafec38f05a1",
            "cdbe527fdedfa3f169ef3656d3a05f0940f033ab90207dcf4e873b08497c99c4", 16000);
        private static readonly MNNVbxProfile Telephone = new MNNVbxProfile("VBx-ResNet101-8K-MNN",
            "3ed741330f8e56a151430c382a172839f1bb7e78d73554a6189e199a2d0a4816",
            "4b85e6b5b5a350eee339fc18eff6b4b7a7e1548d1b8700f9f4ff1eb50b7c652b",
            "6691d81237773bd824c0ffd4d571e068624c9fdc8ddce91741462909cb61fa55", 8000);
        internal static MNNVbxProfile Find(string checkpoint)
            => checkpoint == Wideband.CheckpointSha256 ? Wideband : checkpoint == Telephone.CheckpointSha256 ? Telephone : null;
    }
}
