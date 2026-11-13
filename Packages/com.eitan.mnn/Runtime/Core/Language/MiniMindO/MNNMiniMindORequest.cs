namespace MNN.Unity
{
    /// <summary>One completed input turn. Audio is mono 16 kHz PCM; pixels are planar RGB 256x256 in [-1,1].</summary>
    public sealed class MNNMiniMindORequest
    {
        public string Text { get; set; } = "";
        /// <summary>Previous text turns, excluding the current user turn. Historical media must be supplied again to be understood.</summary>
        public MNNChatMessage[] History { get; set; }
        public float[] Audio16Khz { get; set; }
        public float[] ImagePixels { get; set; }
    }
}
