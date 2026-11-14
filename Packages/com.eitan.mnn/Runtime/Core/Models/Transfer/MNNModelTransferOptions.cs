using System;

namespace MNN.Unity
{
    public class MNNModelTransferOptions
    {
        public int MaxConnections = 4;
        public long ParallelThresholdBytes = 8 * 1024 * 1024;
        public int RetryCount = 2;
        public bool PreservePartialDownloads;
        /// <summary>Free bytes kept for manifests, receipts and other application writes. Defaults to 16 MiB.</summary>
        public long DiskSpaceReserveBytes = 16 * 1024 * 1024;
        /// <summary>Optional platform-specific destination-volume query. Return available bytes; called on workers.</summary>
        public Func<string, long> AvailableSpaceBytes;
        internal MNNModelTransferOptions Snapshot()
        {
            if (MaxConnections < 1 || MaxConnections > 16 || RetryCount < 0 || RetryCount > 32 || ParallelThresholdBytes < 1 || DiskSpaceReserveBytes < 0)
                throw new ArgumentOutOfRangeException(nameof(MNNModelTransferOptions));
            return new MNNModelTransferOptions
            {
                MaxConnections = MaxConnections,
                RetryCount = RetryCount,
                ParallelThresholdBytes = ParallelThresholdBytes,
                PreservePartialDownloads = PreservePartialDownloads,
                DiskSpaceReserveBytes = DiskSpaceReserveBytes,
                AvailableSpaceBytes = AvailableSpaceBytes
            };
        }
    }
}
