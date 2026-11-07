using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    /// <summary>Availability hints from an installed export manifest. Runners still verify the full export contract.</summary>
    public static class MNNAudioProfileFiles
    {
        public static string[] Graphs(string directory, string profile)
        {
            if (string.IsNullOrEmpty(profile)) throw new InvalidDataException("No compatible audio export profile.");
            var paths = new HashSet<string>(StringComparer.Ordinal);
            Collect(MNNModelData.Read(Path.Combine(directory, profile)), paths);
            if (paths.Count == 0) throw new InvalidDataException("Audio export profile names no graph files.");
            foreach (string path in paths) MNNModelAssetDownloader.ResolvePath(directory, path);
            return paths.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        }
        private static void Collect(object value, HashSet<string> paths)
        {
            if (value is string path)
            {
                if (path.EndsWith(".mnn", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".mnn.weight", StringComparison.OrdinalIgnoreCase)) paths.Add(path);
            }
            else if (value is IDictionary<string, object> map)
                foreach (var entry in map) Collect(entry.Value, paths);
            else if (value is IEnumerable<object> list)
                foreach (var item in list) Collect(item, paths);
        }
    }
}
