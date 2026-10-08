using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>
    /// MNN模型管理器窗口
    /// </summary>
    public class MNNModelManagerWindow : EditorWindow
    {
        private MNNModelRepository _repository;
        private List<MNNModelInfo> _filteredModels;
        private MNNModelCategory _selectedCategory = MNNModelCategory.All;
        private string _searchQuery = "";
        private Vector2 _scrollPosition;
        private MNNModelInfo _selectedModel;

        // UI状态
        private bool _isDownloading;
        private float _downloadProgress;
        private string _downloadStatus = "";

        // 样式
        private GUIStyle _headerStyle;
        private GUIStyle _categoryButtonStyle;
        private GUIStyle _modelCardStyle;
        private GUIStyle _selectedCardStyle;

        private const string MODELS_FOLDER = "Assets/MNN/Models";

        [MenuItem("Window/MNN/Model Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<MNNModelManagerWindow>("MNN Model Manager");
            window.minSize = new Vector2(800, 600);
            window.Show();
        }

        private void OnEnable()
        {
            _repository = MNNModelRepository.GetDefaultRepository();
            RefreshModelList();
            CheckInstalledModels();
        }

        private void InitializeStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                margin = new RectOffset(10, 10, 10, 10)
            };

            _categoryButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fixedHeight = 30,
                fontSize = 12
            };

            _modelCardStyle = new GUIStyle(EditorStyles.helpBox)
            {
                margin = new RectOffset(5, 5, 5, 5),
                padding = new RectOffset(10, 10, 10, 10)
            };

            _selectedCardStyle = new GUIStyle(_modelCardStyle)
            {
                normal = { background = MakeTex(2, 2, new Color(0.3f, 0.5f, 0.8f, 0.3f)) }
            };
        }

        private void OnGUI()
        {
            InitializeStyles();

            EditorGUILayout.BeginVertical();
            {
                DrawHeader();
                DrawToolbar();

                EditorGUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
                {
                    DrawCategoryPanel();
                    DrawModelListPanel();
                    DrawDetailPanel();
                }
                EditorGUILayout.EndHorizontal();

                DrawStatusBar();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label("MNN Model Manager", _headerStyle);
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80)))
                {
                    RefreshRepository();
                }

                if (GUILayout.Button("Open Folder", EditorStyles.toolbarButton, GUILayout.Width(100)))
                {
                    OpenModelsFolder();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label("Search:", GUILayout.Width(50));

                EditorGUI.BeginChangeCheck();
                _searchQuery = EditorGUILayout.TextField(_searchQuery, EditorStyles.toolbarSearchField);
                if (EditorGUI.EndChangeCheck())
                {
                    RefreshModelList();
                }

                if (GUILayout.Button("", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    _searchQuery = "";
                    RefreshModelList();
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCategoryPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(180));
            {
                GUILayout.Label("Categories", EditorStyles.boldLabel);

                _scrollPosition.x = EditorGUILayout.BeginScrollView(
                    new Vector2(_scrollPosition.x, 0),
                    GUILayout.Width(180)
                ).x;

                foreach (MNNModelCategory category in Enum.GetValues(typeof(MNNModelCategory)))
                {
                    var count = category == MNNModelCategory.All
                        ? _repository.models.Count
                        : _repository.GetModelsByCategory(category).Count;

                    var label = $"{GetCategoryDisplayName(category)} ({count})";
                    var isSelected = _selectedCategory == category;

                    var style = isSelected
                        ? new GUIStyle(_categoryButtonStyle) { fontStyle = FontStyle.Bold }
                        : _categoryButtonStyle;

                    if (GUILayout.Button(label, style))
                    {
                        _selectedCategory = category;
                        RefreshModelList();
                    }
                }

                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawModelListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(350));
            {
                GUILayout.Label($"Models ({_filteredModels.Count})", EditorStyles.boldLabel);

                _scrollPosition.y = EditorGUILayout.BeginScrollView(
                    new Vector2(0, _scrollPosition.y)
                ).y;

                foreach (var model in _filteredModels)
                {
                    DrawModelCard(model);
                }

                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawModelCard(MNNModelInfo model)
        {
            var style = _selectedModel == model ? _selectedCardStyle : _modelCardStyle;

            EditorGUILayout.BeginVertical(style);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    // 模型图标
                    var icon = GetCategoryIcon(model.category);
                    GUILayout.Label(icon, GUILayout.Width(40), GUILayout.Height(40));

                    // 模型信息
                    EditorGUILayout.BeginVertical();
                    {
                        EditorGUILayout.LabelField(model.displayName, EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(model.version, EditorStyles.miniLabel);

                        // 标签
                        EditorGUILayout.BeginHorizontal();
                        foreach (var tag in model.tags.Take(3))
                        {
                            GUILayout.Label(tag, EditorStyles.miniLabel);
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.EndVertical();

                    GUILayout.FlexibleSpace();

                    // 状态图标
                    if (model.isInstalled)
                    {
                        GUILayout.Label("✓", new GUIStyle(EditorStyles.boldLabel)
                        {
                            normal = { textColor = Color.green },
                            fontSize = 20
                        });
                    }
                }
                EditorGUILayout.EndHorizontal();

                // 文件大小
                EditorGUILayout.LabelField($"Size: {FormatFileSize(model.fileSize)}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();

            // 点击选择
            if (Event.current.type == EventType.MouseDown &&
                GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                _selectedModel = model;
                Repaint();
            }
        }

        private void DrawDetailPanel()
        {
            EditorGUILayout.BeginVertical();
            {
                if (_selectedModel != null)
                {
                    GUILayout.Label("Model Details", EditorStyles.boldLabel);

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    {
                        // 基本信息
                        EditorGUILayout.LabelField("Name:", _selectedModel.displayName);
                        EditorGUILayout.LabelField("Category:", GetCategoryDisplayName(_selectedModel.category));
                        EditorGUILayout.LabelField("Version:", _selectedModel.version);
                        EditorGUILayout.LabelField("Author:", _selectedModel.author);
                        EditorGUILayout.Space();

                        // 描述
                        EditorGUILayout.LabelField("Description:", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField(_selectedModel.description, EditorStyles.wordWrappedLabel);
                        EditorGUILayout.Space();

                        // 输入规格
                        if (_selectedModel.inputSpec != null)
                        {
                            EditorGUILayout.LabelField("Input Specification:", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField($"Shape: [{string.Join(", ", _selectedModel.inputSpec.shape)}]");
                            EditorGUILayout.LabelField($"Format: {_selectedModel.inputSpec.format}");
                            EditorGUILayout.LabelField($"Type: {_selectedModel.inputSpec.dataType}");
                            EditorGUILayout.Space();
                        }

                        // 输出规格
                        if (_selectedModel.outputSpec != null)
                        {
                            EditorGUILayout.LabelField("Output Specification:", EditorStyles.boldLabel);
                            EditorGUILayout.LabelField($"Shape: [{string.Join(", ", _selectedModel.outputSpec.shape)}]");
                            EditorGUILayout.LabelField($"Type: {_selectedModel.outputSpec.dataType}");
                            if (_selectedModel.outputSpec.classCount > 0)
                            {
                                EditorGUILayout.LabelField($"Classes: {_selectedModel.outputSpec.classCount}");
                            }
                            EditorGUILayout.Space();
                        }

                        // 文件信息
                        EditorGUILayout.LabelField("File Size:", FormatFileSize(_selectedModel.fileSize));

                        if (_selectedModel.isInstalled)
                        {
                            EditorGUILayout.LabelField("Local Path:", _selectedModel.localPath);
                        }
                    }
                    EditorGUILayout.EndVertical();

                    EditorGUILayout.Space();

                    // 操作按钮
                    EditorGUILayout.BeginHorizontal();
                    {
                        GUI.enabled = !_isDownloading;

                        if (_selectedModel.isInstalled)
                        {
                            if (GUILayout.Button("Remove", GUILayout.Height(30)))
                            {
                                RemoveModel(_selectedModel);
                            }

                            if (GUILayout.Button("Locate", GUILayout.Height(30)))
                            {
                                LocateModel(_selectedModel);
                            }
                        }
                        else
                        {
                            if (GUILayout.Button("Download", GUILayout.Height(30)))
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
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Select a model to view details", EditorStyles.centeredGreyMiniLabel);
                    GUILayout.FlexibleSpace();
                }
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawStatusBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(20));
            {
                if (_isDownloading)
                {
                    GUILayout.Label(_downloadStatus, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();

                    var rect = GUILayoutUtility.GetRect(200, 16);
                    EditorGUI.ProgressBar(rect, _downloadProgress, $"{(_downloadProgress * 100):F1}%");
                }
                else
                {
                    GUILayout.Label($"{_filteredModels.Count} models | {_filteredModels.Count(m => m.isInstalled)} installed",
                        EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndHorizontal();
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
            _downloadStatus = $"Downloading {model.displayName}...";

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
                            _downloadStatus = $"Downloading {model.displayName}: {FormatFileSize(totalRead)} / {FormatFileSize(totalBytes)}";

                            Repaint();
                        }
                    }
                }

                model.isInstalled = true;
                model.localPath = targetPath;

                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Success",
                    $"{model.displayName} has been downloaded successfully!",
                    "OK");
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Error",
                    $"Failed to download model:\n{e.Message}",
                    "OK");
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
            if (EditorUtility.DisplayDialog("Confirm",
                $"Are you sure you want to remove {model.displayName}?",
                "Remove", "Cancel"))
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
            return category switch
            {
                MNNModelCategory.All => "All Models",
                MNNModelCategory.ImageClassification => "Image Classification",
                MNNModelCategory.ObjectDetection => "Object Detection",
                MNNModelCategory.SemanticSegmentation => "Semantic Segmentation",
                MNNModelCategory.PoseEstimation => "Pose Estimation",
                MNNModelCategory.FaceDetection => "Face Detection",
                MNNModelCategory.FaceRecognition => "Face Recognition",
                MNNModelCategory.OCR => "OCR",
                MNNModelCategory.NLP => "NLP",
                MNNModelCategory.AudioProcessing => "Audio Processing",
                MNNModelCategory.SuperResolution => "Super Resolution",
                MNNModelCategory.StyleTransfer => "Style Transfer",
                _ => category.ToString()
            };
        }

        private string GetCategoryDisplayName(string category)
        {
            if (Enum.TryParse<MNNModelCategory>(category, out var cat))
            {
                return GetCategoryDisplayName(cat);
            }
            return category;
        }

        private GUIContent GetCategoryIcon(string category)
        {
            // 使用Unity内置图标
            return category switch
            {
                "ImageClassification" => EditorGUIUtility.IconContent("d_FilterByType"),
                "ObjectDetection" => EditorGUIUtility.IconContent("d_ViewToolZoom"),
                "FaceDetection" => EditorGUIUtility.IconContent("d_AvatarSelector"),
                "SemanticSegmentation" => EditorGUIUtility.IconContent("d_Grid.Default"),
                "PoseEstimation" => EditorGUIUtility.IconContent("d_AvatarPivot"),
                "SuperResolution" => EditorGUIUtility.IconContent("d_ScaleTool"),
                _ => EditorGUIUtility.IconContent("d_Prefab Icon")
            };
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private Texture2D MakeTex(int width, int height, Color col)
        {
            Color[] pix = new Color[width * height];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = col;

            Texture2D result = new Texture2D(width, height);
            result.SetPixels(pix);
            result.Apply();
            return result;
        }

        #endregion
    }
}
