using System;

namespace MNN.Unity
{
    public class MNNModelTransferOptions
    {
        public int MaxConnections = 4;
        public long ParallelThresholdBytes = 8 * 1024 * 1024;
        public int RetryCount = 2;
        public bool PreservePartialDownloads;
        internal MNNModelTransferOptions Snapshot()
        {
            if (MaxConnections < 1 || MaxConnections > 16 || RetryCount < 0 || RetryCount > 32 || ParallelThresholdBytes < 1)
                throw new ArgumentOutOfRangeException(nameof(MNNModelTransferOptions));
            return new MNNModelTransferOptions
            {
                MaxConnections = MaxConnections,
                RetryCount = RetryCount,
                ParallelThresholdBytes = ParallelThresholdBytes,
                PreservePartialDownloads = PreservePartialDownloads
            };
        }
    }
}
