using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNModelAssetFile
    {
        public string path;
        public string url;
        public string sha256;
        public long bytes;
    }
}
