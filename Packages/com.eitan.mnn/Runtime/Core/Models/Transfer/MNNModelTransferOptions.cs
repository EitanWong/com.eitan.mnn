namespace MNN.Unity
{
    public class MNNModelTransferOptions
    {
        public int MaxConnections = 4;
        public long ParallelThresholdBytes = 8 * 1024 * 1024;
        public int RetryCount = 2;
        public bool PreservePartialDownloads;
    }
}
