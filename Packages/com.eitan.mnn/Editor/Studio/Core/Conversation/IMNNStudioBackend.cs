using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using UnityEditor;

namespace MNN.Unity.Editor
{
    internal interface IMNNStudioBackend : IDisposable
    {
        MNNStudioResult Run(MNNStudioRequest request);
    }
}
