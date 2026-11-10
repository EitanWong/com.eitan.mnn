using System;
using System.IO;

namespace MNN.Unity
{
    /// <summary>A model download cannot fit on its destination volume. Catch this at the application boundary.</summary>
    public sealed class MNNInsufficientDiskSpaceException : IOException
    {
        public const string ErrorCode = "insufficient_disk_space";
        public string DirectoryPath { get; }
        public string RepositoryId { get; }
        /// <summary>Additional free bytes required, including assembly workspace and the configured reserve.</summary>
        public long RequiredBytes { get; }
        public long AvailableBytes { get; }
        public long BytesToFree => Math.Max(0, RequiredBytes - AvailableBytes);

        internal MNNInsufficientDiskSpaceException(string directory, string repository, long required, long available, Exception inner = null)
            : base($"Insufficient disk space for model '{repository}' in '{directory}': required {required} bytes, available {available} bytes. Free space or choose another model directory and retry.", inner)
        {
            DirectoryPath = directory;
            RepositoryId = repository;
            RequiredBytes = required;
            AvailableBytes = available;
        }
    }
}
