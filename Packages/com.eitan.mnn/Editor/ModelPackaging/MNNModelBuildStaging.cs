using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>Journaled rename on the project volume: no duplicate multi-GB model cache and no AssetDatabase deletion.</summary>
    internal sealed class MNNModelBuildStaging
    {
        private readonly string _source;
        private readonly string _work;
        private string Backup => Path.Combine(_work, "Models");
        private string Journal => Path.Combine(_work, "transaction.json");
        internal string ManifestPath => Path.Combine(_work, "model-delivery.json");
        internal bool HasTransaction => File.Exists(Journal);

        internal MNNModelBuildStaging(string source, string work)
        {
            _source = Path.GetFullPath(source);
            _work = Path.GetFullPath(work);
        }

        internal void Begin(MNNModelDeliveryManifest manifest)
        {
            if (HasTransaction || Directory.Exists(Backup) || File.Exists(Backup + ".meta"))
                throw new IOException("An MNN build staging transaction needs recovery: " + _work);
            Directory.CreateDirectory(_work);
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(manifest, true));
            // Persist intent before either rename. Recovery checks each move independently.
            File.WriteAllText(Journal, "{\"schemaVersion\":1}");
            try
            {
                if (Directory.Exists(_source)) Directory.Move(_source, Backup);
                if (File.Exists(_source + ".meta")) File.Move(_source + ".meta", Backup + ".meta");
            }
            catch { Restore(); throw; }
        }

        internal string SourceFile(string relative) => MNNModelAssetDownloader.ResolvePath(Backup, relative);

        internal void Restore()
        {
            if (!HasTransaction) return;
            // Never overwrite user changes or delete an unexpected directory during recovery.
            if (Directory.Exists(Backup))
            {
                if (Directory.Exists(_source) || File.Exists(_source))
                    throw new IOException("Cannot restore MNN models because the original path was recreated. Keep both copies and resolve: " + _source + " and " + Backup);
                Directory.CreateDirectory(Path.GetDirectoryName(_source));
                Directory.Move(Backup, _source);
            }
            if (File.Exists(Backup + ".meta"))
            {
                if (File.Exists(_source + ".meta")) throw new IOException("Cannot overwrite recreated MNN Models.meta: " + _source);
                File.Move(Backup + ".meta", _source + ".meta");
            }
            if (File.Exists(ManifestPath)) File.Delete(ManifestPath);
            File.Delete(Journal);
        }
    }
}
