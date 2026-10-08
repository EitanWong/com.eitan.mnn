using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    /// <summary>
    /// MNN模型管理器窗口 - 使用可复用UI组件和本地化
    /// </summary>
    public class MNNModelManagerWindow : EditorWindow
    {
        private MNNModelRepository _repository;
        private List<MNNModelInfo> _filteredModels;
        private MNNModelCategory _selectedCategory = MNNModelCategory.All;
        private string _searchQuery = "";
        private Vector2 _scrollPosition;
        private Vector2 _categoryScrollPosition;
        private Vector2 _detailScrollPosition;
        private MNNModelInfo _selectedModel;

        // UI状态
        private bool _isDownloading;
        private float _downloadProgress;
        private string _downloadStatus = "";

        private const string MODELS_FOLDER = "Assets/MNN/Models";

        [MenuItem("Window/MNN/Model Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<MNNModelManagerWindow>();
            window.titleContent = new GUIContent(MNNLocalization.Get("modelmanager.title"));
            window.minSize = new Vector2(900, 600);
            window.Show();
        }

        private void OnEnable()
        {
            _repository = MNNModelRepository.GetDefaultRepository();
            RefreshModelList();
            CheckInstalledModels();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginVertical();
            {
                DrawHeader();
                DrawSearchBar();

                EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
                {
                    DrawCategoryPanel();
                    MNNEditorUI.DrawSeparator();
                    DrawModelListPanel();
                    MNNEditorUI.DrawSeparator();
                    DrawDetailPanel();
                }
                EditorGUILayout.EndHorizontal();

                DrawStatusBar();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            MNNEditorUI.DrawToolbar(() =>
            {
                MNNEditorUI.DrawToolbarTitle("modelmanager.title");
                GUILayout.FlexibleSpace();

                if (MNNEditorUI.DrawToolbarButton("common.refresh", 80))
                {
                    RefreshRepository();
                }

                if (MNNEditorUI.DrawToolbarButton("modelmanager.openfolder", 100))
                {
                    OpenModelsFolder();
                }
            });
        }

        private void DrawSearchBar()
        {
            EditorGUI.BeginChangeCheck();
            _searchQuery = MNNEditorUI.DrawSearchField(_searchQuery);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshModelList();
            }
        }

        private void DrawCategoryPanel()
        {
            MNNEditorUI.DrawSidePanel(200, () =>
            {
                GUILayout.Label(MNNLocalization.Get("modelmanager.categories"), EditorStyles.boldLabel);

                _selectedCategory = MNNEditorUI.DrawCategoryList(
                    _selectedCategory,
                    GetCategoryDisplayName,
                    cat => cat == MNNModelCategory.All
                        ? _repository.models.Count
                        : _repository.GetModelsByCategory(cat).Count,
                    cat =>
                    {
                        _selectedCategory = cat;
                        RefreshModelList();
                    }
                );
            }, ref _categoryScrollPosition);
        }

        private void DrawModelListPanel()
        {
            MNNEditorUI.DrawContentPanel(() =>
            {
                GUILayout.Label(
                    $"{MNNLocalization.Get("modelmanager.models")} ({_filteredModels.Count})",
                    EditorStyles.boldLabel
                );

                if (_filteredModels.Count == 0)
                {
                    MNNEditorUI.DrawEmptyState("modelmanager.selectmodel");
                }
                else
                {
                    foreach (var model in _filteredModels)
                    {
                        DrawModelCard(model);
                    }
                }
            }, ref _scrollPosition);
        }

        private void DrawModelCard(MNNModelInfo model)
        {
            MNNEditorUI.DrawCard(_selectedModel == model, () =>
            {
                MNNEditorUI.DrawCardHeader(
                    GetCategoryIcon(model.category),
                    model.displayName,
                    model.version,
                    () =>
                    {
                        if (model.isInstalled)
                        {
                            GUILayout.Label("✓", new GUIStyle(EditorStyles.boldLabel)
                            {
                                normal = { textColor = Color.green },
                                fontSize = 20
                            });
                        }
                    }
                );

                // 标签
                EditorGUILayout.BeginHorizontal();
                foreach (var tag in model.tags.Take(3))
                {
                    GUILayout.Label(tag, EditorStyles.miniLabel);
                }
                EditorGUILayout.EndHorizontal();

                // 文件大小
                EditorGUILayout.LabelField(
                    $"{MNNLocalization.Get("status.size")} {MNNEditorUI.FormatFileSize(model.fileSize)}",
                    EditorStyles.miniLabel
                );
            }, () =>
            {
                _selectedModel = model;
                Repaint();
            });
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(350));
            {
                if (_selectedModel != null)
                {
                    GUILayout.Label(MNNLocalization.Get("modelmanager.details"), EditorStyles.boldLabel);

                    _detailScrollPosition = EditorGUILayout.BeginScrollView(_detailScrollPosition);
                    {
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        {
                            // 基本信息
                            MNNEditorUI.DrawInfoRow("detail.name", _selectedModel.displayName);
                            MNNEditorUI.DrawInfoRow("detail.category", GetCategoryDisplayName(_selectedModel.category));
                            MNNEditorUI.DrawInfoRow("detail.version", _selectedModel.version);
                            MNNEditorUI.DrawInfoRow("detail.author", _selectedModel.author);

                            MNNEditorUI.DrawSeparator();

                            // 描述
                            EditorGUILayout.LabelField(MNNLocalization.Get("detail.description"), EditorStyles.boldLabel);
                            EditorGUILayout.LabelField(_selectedModel.description, EditorStyles.wordWrappedLabel);
                            EditorGUILayout.Space();

                            // 输入规格
                            if (_selectedModel.inputSpec != null)
                            {
                                EditorGUILayout.LabelField(MNNLocalization.Get("detail.inputspec"), EditorStyles.boldLabel);
                                MNNEditorUI.DrawInfoRow("detail.shape", $"[{string.Join(", ", _selectedModel.inputSpec.shape)}]");
                                MNNEditorUI.DrawInfoRow("detail.format", _selectedModel.inputSpec.format);
                                MNNEditorUI.DrawInfoRow("detail.type", _selectedModel.inputSpec.dataType);
                                EditorGUILayout.Space();
                            }

                            // 输出规格
                            if (_selectedModel.outputSpec != null)
                            {
                                EditorGUILayout.LabelField(MNNLocalization.Get("detail.outputspec"), EditorStyles.boldLabel);
                                MNNEditorUI.DrawInfoRow("detail.shape", $"[{string.Join(", ", _selectedModel.outputSpec.shape)}]");
                                MNNEditorUI.DrawInfoRow("detail.type", _selectedModel.outputSpec.dataType);
                                if (_selectedModel.outputSpec.classCount > 0)
                                {
                                    MNNEditorUI.DrawInfoRow("detail.classes", _selectedModel.outputSpec.classCount.ToString());
                                }
                                EditorGUILayout.Space();
                            }

                            // 文件信息
                            MNNEditorUI.DrawInfoRow("detail.filesize", MNNEditorUI.FormatFileSize(_selectedModel.fileSize));

                            if (_selectedModel.isInstalled)
                            {
                                MNNEditorUI.DrawInfoRow("detail.localpath", _selectedModel.localPath);
                            }
                        }
                        EditorGUILayout.EndVertical();
                    }
                    EditorGUILayout.EndScrollView();

                    EditorGUILayout.Space();

                    // 操作按钮
                    EditorGUILayout.BeginHorizontal();
                    {
                        GUI.enabled = !_isDownloading;

                        if (_selectedModel.isInstalled)
                        {
                            if (MNNEditorUI.DrawSecondaryButton("common.remove"))
                            {
                                RemoveModel(_selectedModel);
                            }

                            if (MNNEditorUI.DrawSecondaryButton("modelmanager.locate"))
                            {
                                LocateModel(_selectedModel);
                            }
                        }
                        else
                        {
                            if (MNNEditorUI.DrawPrimaryButton("common.download"))
                            {
                                DownloadModel(_selectedModel);
                            }
                        }

                        GUI.enabled = true;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                else
                {
                    MNNEditorUI.DrawEmptyState("modelmanager.selectmodel");
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawStatusBar()
        {
            if (_isDownloading)
            {
                MNNEditorUI.DrawDownloadStatusBar(_isDownloading, _downloadProgress, _downloadStatus);
            }
            else
            {
                var statusText = MNNLocalization.Format(
                    "status.models",
                    _filteredModels.Count,
                    _filteredModels.Count(m => m.isInstalled)
                );
                MNNEditorUI.DrawStatusBar(statusText);
            }
        }

        #region Helper Methods

        private void RefreshModelList()
        {
            if (string.IsNullOrEmpty(_searchQuery))
            {
                _filteredModels = _repository.GetModelsByCategory(_selectedCategory);
            }
            else
            {
                var searchResults = _repository.SearchModels(_searchQuery);
                _filteredModels = _selectedCategory == MNNModelCategory.All
                    ? searchResults
                    : searchResults.FindAll(m => m.category == _selectedCategory.ToString());
            }
        }

        private void CheckInstalledModels()
        {
            if (!Directory.Exists(MODELS_FOLDER))
                return;

            foreach (var model in _repository.models)
            {
                var path = Path.Combine(MODELS_FOLDER, $"{model.name}.mnn");
                model.isInstalled = File.Exists(path);
                if (model.isInstalled)
                {
                    model.localPath = path;
                }
            }
        }

        private async void DownloadModel(MNNModelInfo model)
        {
            _isDownloading = true;
            _downloadProgress = 0;
            _downloadStatus = $"{MNNLocalization.Get("download.downloading")} {model.displayName}...";

            try
            {
                if (!Directory.Exists(MODELS_FOLDER))
                {
                    Directory.CreateDirectory(MODELS_FOLDER);
                }

                var targetPath = Path.Combine(MODELS_FOLDER, $"{model.name}.mnn");

                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromMinutes(10);

                    var response = await client.GetAsync(model.downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? model.fileSize;
                    var buffer = new byte[8192];
                    var totalRead = 0L;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        int read;
                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read);
                            totalRead += read;

                            _downloadProgress = (float)totalRead / totalBytes;
                            _downloadStatus = $"{MNNLocalization.Get("download.downloading")} {model.displayName}: " +
                                            $"{MNNEditorUI.FormatFileSize(totalRead)} / {MNNEditorUI.FormatFileSize(totalBytes)}";

                            Repaint();
                        }
                    }
                }

                model.isInstalled = true;
                model.localPath = targetPath;

                AssetDatabase.Refresh();

                MNNEditorUI.ShowInfoDialog(
                    "download.success",
                    $"{model.displayName} {MNNLocalization.Get("download.success")}"
                );
            }
            catch (Exception e)
            {
                MNNEditorUI.ShowErrorDialog(
                    $"{MNNLocalization.Get("download.failed")}\n{e.Message}"
                );
            }
            finally
            {
                _isDownloading = false;
                _downloadStatus = "";
                Repaint();
            }
        }

        private void RemoveModel(MNNModelInfo model)
        {
            if (MNNEditorUI.ShowConfirmDialog(
                "download.confirm",
                $"{MNNLocalization.Get("download.remove.confirm")} {model.displayName}?",
                "common.remove",
                "common.cancel"))
            {
                if (File.Exists(model.localPath))
                {
                    File.Delete(model.localPath);
                    File.Delete(model.localPath + ".meta");
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
        }

        private void RefreshRepository()
        {
            _repository = MNNModelRepository.GetDefaultRepository();
            CheckInstalledModels();
            RefreshModelList();
            Repaint();
        }

        private void OpenModelsFolder()
        {
            if (!Directory.Exists(MODELS_FOLDER))
            {
                Directory.CreateDirectory(MODELS_FOLDER);
            }
            EditorUtility.RevealInFinder(MODELS_FOLDER);
        }

        private string GetCategoryDisplayName(MNNModelCategory category)
        {
            return MNNLocalization.Get($"category.{category.ToString().ToLower()}");
        }

        private string GetCategoryDisplayName(string category)
        {
            return MNNLocalization.Get($"category.{category.ToLower()}");
        }

        private GUIContent GetCategoryIcon(string category)
        {
            return category switch
            {
                "ImageClassification" => MNNEditorUI.GetIcon("d_FilterByType"),
                "ObjectDetection" => MNNEditorUI.GetIcon("d_ViewToolZoom"),
                "FaceDetection" => MNNEditorUI.GetIcon("d_AvatarSelector"),
                "SemanticSegmentation" => MNNEditorUI.GetIcon("d_Grid.Default"),
                "PoseEstimation" => MNNEditorUI.GetIcon("d_AvatarPivot"),
                "SuperResolution" => MNNEditorUI.GetIcon("d_ScaleTool"),
                _ => MNNEditorUI.GetIcon("d_Prefab Icon")
            };
        }

        #endregion
    }
}
