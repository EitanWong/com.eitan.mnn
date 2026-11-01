using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor
{
    [Serializable]
    public sealed class MNNModelDownloadJob
    {
        public string Id;
        public MNNModelProvider Provider = MNNModelProvider.ModelScope;
        public string RepositoryId;
        public string Revision;
        public string ModelScopeId;
        public string DisplayName;
        public string StagingPath;
        public string InstalledPath;
        public MNNDownloadState State;
        public long ReceivedBytes;
        public long TotalBytes;
        public string CurrentFile;
        public string Error;
        [NonSerialized]
        public double BytesPerSecond;
        [NonSerialized]
        internal int ProgressId = -1;
        [NonSerialized]
        internal CancellationTokenSource Cancellation;
        [NonSerialized]
        internal Task Execution;
        [NonSerialized]
        internal long PendingBytes;
        [NonSerialized]
        internal long PendingTotal;
        [NonSerialized]
        internal string PendingFile;
        [NonSerialized]
        internal long LastBytes;
        [NonSerialized]
        internal double LastSample;
        [NonSerialized]
        internal readonly object Sync = new object ();
        public bool IsRunning => State == MNNDownloadState.Downloading || State == MNNDownloadState.Pausing || State == MNNDownloadState.Cancelling;
        public bool CanResume => State == MNNDownloadState.Paused || State == MNNDownloadState.Failed;
        public float Progress => TotalBytes > 0 ? Mathf.Clamp01((float)((double)ReceivedBytes / TotalBytes)) : 0;
    }

    public enum MNNDownloadState
    {
        Downloading,
        Pausing,
        Paused,
        Cancelling,
        Cancelled,
        Completed,
        Failed
    }
}
