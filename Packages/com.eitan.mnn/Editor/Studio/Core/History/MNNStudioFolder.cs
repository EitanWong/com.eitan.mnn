using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MNN.Unity.Editor
{
    [Serializable]
    internal sealed class MNNStudioFolder
    {
        public string Id, Name;
        public bool Collapsed;
    }
}
