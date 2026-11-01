namespace MNN.Unity
{
    /// <summary>An Android NNAPI driver reported by the OS; not per-operator execution evidence.</summary>
    public sealed class MNNNeuralDevice
    {
        public string Name { get; }
        /// <summary>NDK device type: 0 unknown, 1 other, 2 CPU, 3 GPU, 4 accelerator.</summary>
        public int DeviceType { get; }
        public long FeatureLevel { get; }
        public bool IsNeuralAccelerator => DeviceType == 4;
        internal MNNNeuralDevice(string name, int type, long featureLevel)
        { Name = name; DeviceType = type; FeatureLevel = featureLevel; }
        public override string ToString() => Name + " (type=" + DeviceType + ", feature=" + FeatureLevel + ")";
    }
}
