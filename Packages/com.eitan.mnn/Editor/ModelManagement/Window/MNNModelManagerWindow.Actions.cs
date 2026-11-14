using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public partial class MNNModelManagerWindow
    {
        private void DownloadModel(MNNModelInfo model)
        {
            try
            {
                var job = MNNModelDownloadTasks.instance.StartDownload(model, Path.Combine(DOWNLOAD_STAGING_FOLDER, MNNModelRepository.InstallFolderName(model)), GetInstalledPath(model));
                _selectedDownloadId = job.Id;
            }
            catch (Exception error)
            {
                MNNEditorUI.ShowErrorDialog(error.Message);
            }
        }

        private static string SanitizeFolderName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safe = string.IsNullOrWhiteSpace(name) ? "model" : new string (name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().Trim('.');
            return string.IsNullOrEmpty(safe) ? "model" : safe;
        }

        private static string GetInstalledPath(MNNModelInfo model, MNNModelInstallIndex installations = null)
        {
            if (model.repositoryId == MNNMiniMindO.Repository)
            {
                string prepared = Path.Combine(MODELS_FOLDER, MNNMiniMindO.InstallationName);
                if (MNNMiniMindO.IsComplete(prepared)) return prepared;
            }
            var legacy = Path.Combine(MODELS_FOLDER, SanitizeFolderName(model.name));
            var marker = Path.Combine(legacy, INSTALL_MARKER);
            if (File.Exists(marker) && MNNModelRepository.MatchesInstallMarker(model, File.ReadAllText(marker).Trim()))
                return legacy;
            string canonical = Path.Combine(MODELS_FOLDER, MNNModelRepository.InstallFolderName(model));
            string canonicalMarker = Path.Combine(canonical, INSTALL_MARKER);
            if (File.Exists(canonicalMarker) && MNNModelRepository.MatchesInstallMarker(model, File.ReadAllText(canonicalMarker).Trim()))
                return canonical;
            return (installations ?? MNNModelInstallIndex.Read(MODELS_FOLDER)).Find(model) ?? canonical;
        }

        private void RemoveModel(MNNModelInfo model)
        {
            if (MNNEditorUI.ShowConfirmDialog("download.confirm", $"{MNNLocalization.Get("download.remove.confirm")} {model.displayName}?", "common.remove", "common.cancel"))
            {
                if (model.isRepository && Directory.Exists(model.localPath))
                {
                    Directory.Delete(model.localPath, true);
                    var metaPath = model.localPath + ".meta";
                    if (File.Exists(metaPath))
                        File.Delete(metaPath);
                    model.isInstalled = false;
                    model.localPath = null;
                    AssetDatabase.Refresh();
                }
                else if (File.Exists(model.localPath))
                {
                    File.Delete(model.localPath);
                    var metaPath = model.localPath + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Delete(metaPath);
                    }

                    model.isInstalled = false;
                    model.localPath = null;
                    AssetDatabase.Refresh();
                }
            }
        }

        private void LocateModel(MNNModelInfo model)
        {
            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(model.localPath);
            if (obj != null)
            {
                EditorGUIUtility.PingObject(obj);
                Selection.activeObject = obj;
            }
            else if (!string.IsNullOrEmpty(model.localPath) && File.Exists(model.localPath))
            {
                EditorUtility.RevealInFinder(model.localPath);
            }
        }

        private void OpenModelsFolder()
        {
            if (!Directory.Exists(MODELS_FOLDER))
            {
                Directory.CreateDirectory(MODELS_FOLDER);
            }

            EditorUtility.RevealInFinder(Path.GetFullPath(MODELS_FOLDER));
        }
    }
}
