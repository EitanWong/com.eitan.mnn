using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MNN.Unity.Editor
{
    internal sealed class MNNStudioMessage
    {
        internal readonly bool IsUser;
        internal readonly string Text, ImagePath, AudioPath;
        internal readonly MNNStudioResult Result;
        internal MNNStudioMessage(bool user, string text, string image = null, string audio = null, MNNStudioResult result = null)
        {
            IsUser = user;
            Text = text ?? string.Empty;
            ImagePath = image;
            AudioPath = audio;
            Result = result;
        }
    }
}
