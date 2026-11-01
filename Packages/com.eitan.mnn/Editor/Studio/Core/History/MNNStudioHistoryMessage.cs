using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MNN.Unity.Editor
{
    [Serializable]
    internal sealed class MNNStudioHistoryMessage
    {
        public bool IsUser, HasResult, Limited, Cancelled, RepetitionStopped;
        public string Text, ImagePath, AudioPath, BackendDescription;
        public int Tokens;
        public double Seconds, AudioSeconds;
    }
}
