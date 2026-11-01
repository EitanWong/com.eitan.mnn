namespace MNN.Unity
{
    internal static class MNNFullSubNetPlusModel
    {
        internal const string Repository = "RookieJunChen/FullSubNet-plus";
        internal const string Revision = "b115416d6e2a983c3724fc77d0cdc3d8896a2855";
        internal const string Graph = "fullsubnetplus_fp32.mnn";
        internal const string FrontGraph = "fullsubnetplus_front_fp32.mnn";
        internal const string SubbandGraph = "fullsubnetplus_subband_32_fp32.mnn";
        internal static bool Matches(MNNFullSubNetConfig config) => config.repository == Repository && config.source_revision == Revision &&
            config.graph == Graph && config.fullband_graph == FrontGraph && config.subband_graph == SubbandGraph &&
            config.sample_rate == 16000 && config.fft_size == 512 && config.hop_size == 256 && config.look_ahead == 2 &&
            config.normalization == "offline_laplace_norm" && config.recurrent_block_frames == 32;
        internal static bool VerifyFiles(string directory) =>
            MNNFullSubNetModel.Verify(directory, Graph, 34965564, "9407d32b29597949686315b4d089b9ff02304973a63dad33d94e469f4e021093") &&
            MNNFullSubNetModel.Verify(directory, FrontGraph, 27642408, "3d2d67a72ded45bb1525a71f0e29bda3febca2e7b340c9f1f7dd02dcc3f84971") &&
            MNNFullSubNetModel.Verify(directory, SubbandGraph, 401440, "2fb3007c784849988de3efc3c135655042c547e10919cc624e1e148eb3ad950b") &&
            MNNFullSubNetModel.Verify(directory, SubbandGraph + ".weight", 7314440, "b727244ed95528227c95b500b1e36d52fb5ccc7fd0532e70f057aac0b6f7cb9c");
    }
}
