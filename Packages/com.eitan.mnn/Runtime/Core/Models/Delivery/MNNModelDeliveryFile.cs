using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace MNN.Unity
{
    [Serializable]
    public sealed class MNNModelDeliveryFile
    {
        public string path;
        public string url;
        public string sha256;
        public long bytes;
        public bool bundled;
    }
}
