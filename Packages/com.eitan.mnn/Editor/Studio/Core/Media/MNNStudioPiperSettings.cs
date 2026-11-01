using System.IO;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor
{
    internal static class MNNStudioPiperSettings
    {
        internal const string ExecutablePreference = "MNN.Studio.EspeakPath";
        internal static string FindExecutable(string modelDirectory) => MNNPiper.FindEspeak(modelDirectory);

        internal static string ResolveExecutable(string modelDirectory)
        {
            string selected = EditorPrefs.GetString(ExecutablePreference, "");
            // Older Studio versions exposed an executable field. Ignore a stale
            // selection (including dictionary files) and use automatic discovery.
            if (MNNPiper.IsEspeakExecutable(selected))
                return MNNPiper.ResolveEspeak(selected, modelDirectory);
            if (!string.IsNullOrWhiteSpace(selected))
                EditorPrefs.DeleteKey(ExecutablePreference);
            return FindExecutable(modelDirectory);
        }

        internal static string DataPreference(string modelDirectory) => "MNN.Studio.PiperData." + Hash128.Compute(Path.GetFullPath(modelDirectory));
        internal static string ResolveDataDirectory(MNNStudioModel model)
        {
            string selected = EditorPrefs.GetString(DataPreference(model.Directory), "");
            return string.IsNullOrWhiteSpace(selected) ? model.PiperDataDirectory : MNNPiper.ResolveDataDirectory(model.Directory, selected: selected);
        }

        internal static string PickerDirectory(string selected, string modelDirectory)
        {
            if (!string.IsNullOrWhiteSpace(selected) && Directory.Exists(selected))
                return Path.GetFullPath(selected);
            try
            {
                return MNNPiper.ResolveDataDirectory(modelDirectory);
            }
            catch (IOException)
            {
                return Path.GetFullPath(modelDirectory);
            }
        }
    }
}
