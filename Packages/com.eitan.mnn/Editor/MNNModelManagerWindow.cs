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
    /// MNN模型管理器窗口 - 优化版布局
    /// </summary>
    public class MNNModelManagerWindow : EditorWindow
    {
        // 数据
        private MNNModelRepository _repository;
        private List<MNNModelInfo> _filteredModels;
        private MNNModelCategory _selectedCategory = MNNModelCategory.All;
        private string _searchQuery = "";
        private MNNModelInfo _selectedModel;

        // 滚动位置
        private Vector2 _categoryScrollPos;
        private Vector2 _modelListScrollPos;
        private Vector2 _detailScrollPos;

        // 下载状态
        private bool _isDownloading;
        private float _downloadProgress;
        private string _downloadStatus = "";

        // 布局常量
        private const float CATEGORY_WIDTH = 200f;
        private const float MODEL_LIST_WIDTH = 380f;
        private const float MIN_DETAIL_WIDTH = 350f;
        private const float TOOLBAR_HEIGHT = 22f;
        private const float STATUSBAR_HEIGHT = 22f;
        private const float SEPARATOR_WIDTH = 1f;
        private const float PADDING = 5f;

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
            // 使用BeginVertical确保垂直布局
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawToolbarSection();
                DrawSearchSection();
                DrawMainContent();
                DrawStatusBarSection();
            }
        }

        #region Layout Sections

        private void DrawToolbarSection()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(TOOLBAR_HEIGHT)))
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
            }
        }

        private void DrawSearchSection()
        {
            EditorGUI.BeginChangeCheck();
            _searchQuery = MNNEditorUI.DrawSearchField(_searchQuery);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshModelList();
            }
        }

        private void DrawMainContent()
        {
            // 计算可用空间
            var availableHeight = position.height - TOOLBAR_HEIGHT * 2 - STATUSBAR_HEIGHT - PADDING * 2;

            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.Height(availableHeight)))
            {
                DrawCategoryPanel();
                DrawVerticalSeparator();
                DrawModelListPanel();
                DrawVerticalSeparator();
                DrawDetailPanel();
            }
        }

        private void DrawStatusBarSection()
        {
            if (_isDownloading)
            {
                MNNEditorUI.DrawDownloadStatusBar(_isDownloading, _downloadProgress, _downloadStatus);
            }
            else
            {
                var statusText = $"{_filteredModels.Count} {MNNLocalization.Get("status.models")} | " +
                               $"{_filteredModels.Count(m => m.isInstalled)} {MNNLocalization.Get("modelmanager.installed")}";
                MNNEditorUI.DrawStatusBar(statusText);
            }
        }

        #endregion

        #region Panel Components

        private void DrawCategoryPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(CATEGORY_WIDTH)))
            {
                // 标题
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label(MNNLocalization.Get("modelmanager.categories"), EditorStyles.boldLabel);
                }

                // 分类列表
                _categoryScrollPos = EditorGUILayout.BeginScrollView(_categoryScrollPos);
                {
                    foreach (MNNModelCategory category in Enum.GetValues(typeof(MNNModelCategory)))
                    {
                        DrawCategoryButton(category);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawCategoryButton(MNNModelCategory category)
        {
            var count = category == MNNModelCategory.All
                ? _repository.models.Count
                : _repository.GetModelsByCategory(category).Count;

            var displayName = GetCategoryDisplayName(category);
            var label = $"{displayName} ({count})";
            var isSelected = _selectedCategory == category;

            var style = isSelected
                ? new GUIStyle(MNNEditorUI.CategoryButtonStyle) { fontStyle = FontStyle.Bold }
                : MNNEditorUI.CategoryButtonStyle;

            if (GUILayout.Button(label, style))
            {
                _selectedCategory = category;
                RefreshModelList();
                _selectedModel = null; // 清除选中的模型
            }
        }

        private void DrawModelListPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(MODEL_LIST_WIDTH)))
            {
                // 标题
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label(
                        $"{MNNLocalization.Get("modelmanager.models")} ({_filteredModels.Count})",
                        EditorStyles.boldLabel
                    );
                }

                // 模型列表
                _modelListScrollPos = EditorGUILayout.BeginScrollView(_modelListScrollPos);
                {
                    if (_filteredModels.Count == 0)
                    {
                        DrawEmptyModelList();
                    }
                    else
                    {
                        foreach (var model in _filteredModels)
                        {
                            DrawModelCard(model);
                            GUILayout.Space(2); // 卡片间距
                        }
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawEmptyModelList()
        {
            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope())
                {
                    var icon = MNNEditorUI.GetIcon("d_console.infoicon");
                    GUILayout.Label(icon, GUILayout.Height(48));
                    GUILayout.Label(MNNLocalization.Get("modelmanager.nomodels"), EditorStyles.centeredGreyMiniLabel);
                }
                GUILayout.FlexibleSpace();
            }

            GUILayout.FlexibleSpace();
        }

        private void DrawModelCard(MNNModelInfo model)
        {
            var isSelected = _selectedModel == model;
            var style = isSelected ? MNNEditorUI.SelectedCardStyle : MNNEditorUI.CardStyle;

            var rect = EditorGUILayout.BeginVertical(style);
            {
                DrawModelCardHeader(model);
                DrawModelCardBody(model);
                DrawModelCardFooter(model);
            }
            EditorGUILayout.EndVertical();

            // 处理点击事件
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                _selectedModel = model;
                Event.current.Use();
                Repaint();
            }
        }

        private void DrawModelCardHeader(MNNModelInfo model)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // 图标
                var icon = GetCategoryIcon(model.category);
                GUILayout.Label(icon, GUILayout.Width(40), GUILayout.Height(40));

                // 信息
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(model.displayName, EditorStyles.boldLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField(model.version, EditorStyles.miniLabel, GUILayout.Width(60));
                        EditorGUILayout.LabelField(
                            GetCategoryDisplayName(model.category),
                            EditorStyles.miniLabel
                        );
                    }
                }

                GUILayout.FlexibleSpace();

                // 安装状态
                if (model.isInstalled)
                {
                    var checkStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        normal = { textColor = new Color(0.3f, 0.8f, 0.3f) },
                        fontSize = 18
                    };
                    GUILayout.Label("✓", checkStyle, GUILayout.Width(20));
                }
            }
        }

        private void DrawModelCardBody(MNNModelInfo model)
        {
            // 标签
            if (model.tags != null && model.tags.Length > 0)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (var tag in model.tags.Take(3))
                    {
                        var tagStyle = new GUIStyle(EditorStyles.miniLabel)
                        {
                            normal = { background = MNNEditorUI.GetColorTexture(new Color(0.3f, 0.3f, 0.3f, 0.3f)) },
                            padding = new RectOffset(4, 4, 2, 2),
                            margin = new RectOffset(0, 4, 2, 2)
                        };
                        GUILayout.Label(tag, tagStyle);
                    }
                }
            }
        }

        private void DrawModelCardFooter(MNNModelInfo model)
        {
            // 文件大小
            EditorGUILayout.LabelField(
                $"{MNNLocalization.Get("status.size")} {MNNEditorUI.FormatFileSize(model.fileSize)}",
                EditorStyles.miniLabel
            );
        }

        private void DrawDetailPanel()
        {
            // 使用FlexibleSpace让详情面板占据剩余空间
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                // 标题
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                {
                    GUILayout.Label(MNNLocalization.Get("modelmanager.details"), EditorStyles.boldLabel);
                }

                if (_selectedModel != null)
                {
                    DrawModelDetails();
                }
                else
                {
                    DrawEmptyDetails();
                }
            }
        }

        private void DrawModelDetails()
        {
            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos);
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    DrawBasicInfo();
                    MNNEditorUI.DrawSeparator();
                    DrawDescription();
                    MNNEditorUI.DrawSeparator();
                    DrawInputSpec();
                    MNNEditorUI.DrawSeparator();
                    DrawOutputSpec();
                    MNNEditorUI.DrawSeparator();
                    DrawFileInfo();
                }

                GUILayout.Space(10);
                DrawActionButtons();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawBasicInfo()
        {
            GUILayout.Label(MNNLocalization.Get("detail.name"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_selectedModel.displayName);
            GUILayout.Space(5);

            MNNEditorUI.DrawInfoRow("detail.category", GetCategoryDisplayName(_selectedModel.category));
            MNNEditorUI.DrawInfoRow("detail.version", _selectedModel.version);
            MNNEditorUI.DrawInfoRow("detail.author", _selectedModel.author);
        }

        private void DrawDescription()
        {
            GUILayout.Label(MNNLocalization.Get("detail.description"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_selectedModel.description, EditorStyles.wordWrappedLabel);
        }

        private void DrawInputSpec()
        {
            if (_selectedModel.inputSpec == null) return;

            GUILayout.Label(MNNLocalization.Get("detail.inputspec"), EditorStyles.boldLabel);
            MNNEditorUI.DrawInfoRow("detail.shape", $"[{string.Join(", ", _selectedModel.inputSpec.shape)}]");
            MNNEditorUI.DrawInfoRow("detail.format", _selectedModel.inputSpec.format);
            MNNEditorUI.DrawInfoRow("detail.type", _selectedModel.inputSpec.dataType);
        }

        private void DrawOutputSpec()
        {
            if (_selectedModel.outputSpec == null) return;

            GUILayout.Label(MNNLocalization.Get("detail.outputspec"), EditorStyles.boldLabel);
            MNNEditorUI.DrawInfoRow("detail.shape", $"[{string.Join(", ", _selectedModel.outputSpec.shape)}]");
            MNNEditorUI.DrawInfoRow("detail.type", _selectedModel.outputSpec.dataType);

            if (_selectedModel.outputSpec.classCount > 0)
            {
                MNNEditorUI.DrawInfoRow("detail.classes", _selectedModel.outputSpec.classCount.ToString());
            }
        }

        private void DrawFileInfo()
        {
            MNNEditorUI.DrawInfoRow("detail.filesize", MNNEditorUI.FormatFileSize(_selectedModel.fileSize));

            if (_selectedModel.isInstalled)
            {
                GUILayout.Label(MNNLocalization.Get("detail.localpath"), EditorStyles.boldLabel);
                EditorGUILayout.SelectableLabel(_selectedModel.localPath, EditorStyles.miniLabel, GUILayout.Height(32));
            }
        }

        private void DrawActionButtons()
        {
            GUI.enabled = !_isDownloading;

            using (new EditorGUILayout.HorizontalScope())
            {
                if (_selectedModel.isInstalled)
                {
                    if (MNNEditorUI.DrawSecondaryButton("modelmanager.locate", 30))
                    {
                        LocateModel(_selectedModel);
                    }

                    if (MNNEditorUI.DrawSecondaryButton("common.remove", 30))
                    {
                        RemoveModel(_selectedModel);
                    }
                }
                else
                {
                    if (MNNEditorUI.DrawPrimaryButton("common.download", 35))
                    {
                        DownloadModel(_selectedModel);
                    }
                }
            }

            GUI.enabled = true;
        }

        private void DrawEmptyDetails()
        {
            GUILayout.FlexibleSpace();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUILayout.VerticalScope())
                {
                    var icon = MNNEditorUI.GetIcon("d_UnityEditor.InspectorWindow");
                    GUILayout.Label(icon, GUILayout.Height(64));
                    GUILayout.Label(
                        MNNLocalization.Get("modelmanager.selectmodel"),
                        EditorStyles.centeredGreyMiniLabel
                    );
                }
                GUILayout.FlexibleSpace();
            }

            GUILayout.FlexibleSpace();
        }

        private void DrawVerticalSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(SEPARATOR_WIDTH), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        #endregion

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
                    "common.ok",
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
