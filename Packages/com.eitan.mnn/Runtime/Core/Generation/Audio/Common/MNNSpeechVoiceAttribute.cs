using System;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Display a serialized voice string as installed choices from a sibling model directory string.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class MNNSpeechVoiceAttribute : PropertyAttribute
    {
        public string DirectoryField { get; }
        public MNNSpeechVoiceAttribute(string directoryField) => DirectoryField = directoryField;
    }
}
