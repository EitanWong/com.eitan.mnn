using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>Shared installed-voice picker for component inspectors.</summary>
    public static class MNNSpeechVoiceField
    {
        public static void Draw(Rect position, SerializedProperty voice, GUIContent label, string directory)
        {
            try
            {
                var options = MNNSpeechSynthesizer.GetVoiceOptions(directory);
                string[] ids = options.VoiceIds;
                var labels = new string[ids.Length + 1];
                labels[0] = options.RequiresReferenceAudio ? "Reference audio" : "Model default";
                Array.Copy(ids, 0, labels, 1, ids.Length);
                int selected = string.IsNullOrWhiteSpace(voice.stringValue) ? 0 : Array.IndexOf(ids, voice.stringValue) + 1;
                if (!options.SupportsVoice(voice.stringValue)) selected = -1;
                EditorGUI.BeginChangeCheck();
                int next = EditorGUI.Popup(position, label, selected, Array.ConvertAll(labels, text => new GUIContent(text)));
                if (EditorGUI.EndChangeCheck() && next >= 0)
                    voice.stringValue = next == 0 ? "" : ids[next - 1];
            }
            catch (Exception error) when (error is InvalidDataException || error is IOException || error is ArgumentException || error is UnauthorizedAccessException)
            {
                EditorGUI.LabelField(position, label, new GUIContent("Voice metadata unavailable", error.Message));
            }
        }
    }

    [CustomPropertyDrawer(typeof(MNNSpeechVoiceAttribute))]
    internal sealed class MNNSpeechVoiceDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var setting = (MNNSpeechVoiceAttribute)attribute;
            int dot = property.propertyPath.LastIndexOf('.');
            string parent = dot < 0 ? "" : property.propertyPath.Substring(0, dot + 1);
            var directory = property.serializedObject.FindProperty(parent + setting.DirectoryField);
            if (property.propertyType != SerializedPropertyType.String || directory?.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Voice and model directory must be string fields.");
                return;
            }
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                using (new EditorGUI.DisabledScope(directory.hasMultipleDifferentValues))
                    MNNSpeechVoiceField.Draw(position, property, label, directory.stringValue);
            }
            finally { EditorGUI.EndProperty(); }
        }
    }
}
