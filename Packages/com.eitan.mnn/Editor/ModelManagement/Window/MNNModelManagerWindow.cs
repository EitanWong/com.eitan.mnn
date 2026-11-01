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
    /// <summary>
    /// MNN模型管理器窗口 - 优化版布局
    /// </summary>
    public partial class MNNModelManagerWindow : EditorWindow
    {
        // 数据
        private MNNModelRepository _repository;
        private List<MNNModelInfo> _filteredModels;
        private MNNModelCategory[] _categories;
        private string[] _categoryLabels;
        private MNNModelCategory _selectedCategory = MNNModelCategory.All;
        private string _selectedFamily = string.Empty;
        private string _selectedGeneration = string.Empty;
        private string[] _familyValues = Array.Empty<string>();
        private string[] _familyLabels = Array.Empty<string>();
        private string[] _generationValues = Array.Empty<string>();
        private string[] _generationLabels = Array.Empty<string>();
        private string[] _sizeFilterLabels;
        private string[] _parameterFilterLabels;
        private string[] _sortLabels;
        private int _sizeFilter;
        private int _parameterFilter;
        private int _sortMode;
        private bool _showAdvancedFilters;
        private string _searchQuery = "";
        private MNNStudioTaskInfo _workspaceTask;
        private MNNModelInfo _selectedModel;
        private bool _isClosing;
        private CancellationTokenSource _catalogCancellation;
        private CancellationTokenSource _searchCancellation;
        private int _searchScopePage;
        private string _searchFaceCursor;
        private bool _searchFaceMore, _searchReady;
        // 滚动位置
        private Vector2 _categoryScrollPos;
        private Vector2 _modelListScrollPos;
        private Vector2 _detailScrollPos;
        // 下载状态
        private bool _isLoadingModels;
        private bool _catalogHasMore, _loadMorePending;
        private string _repositoryError;
        private string _selectedDownloadId;
        private const float CATEGORY_WIDTH = 176f;
        private const float MODEL_LIST_MIN_WIDTH = 250f;
        private const float THREE_PANE_MIN_WIDTH = 1080f;
        private const float SEPARATOR_WIDTH = 1f;
        private const string MODELS_FOLDER = "Assets/StreamingAssets/MNN/Models";
        private const string DOWNLOAD_STAGING_FOLDER = "Library/MNN/Downloads";
        private const string INSTALL_MARKER = ".mnn-modelscope-installed";
        [MenuItem("Window/MNN/Model Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<MNNModelManagerWindow>();
            window.titleContent = new GUIContent(MNNLocalization.Get("modelmanager.title"));
            window.minSize = new Vector2(760, 480);
            if (window._workspaceTask != null)
            {
                window._workspaceTask = null;
                window._selectedCategory = MNNModelCategory.All;
                window._searchQuery = "";
                window.ResetDependentFilters();
                window.RebuildFamilyOptions();
                window.RefreshModelList();
            }

            window.Show();
        }

        internal static void ShowForTask(MNNStudioTaskInfo task)
        {
            ShowWindow();
            var window = GetWindow<MNNModelManagerWindow>();
            window._workspaceTask = task;
            window._selectedCategory = MNNModelCategory.All;
            window._searchQuery = "";
            window.ResetDependentFilters();
            window._sizeFilter = window._parameterFilter = 0;
            window.RebuildFamilyOptions();
            window.RefreshModelList();
            window.Focus();
        }

        private void OnEnable()
        {
            _isClosing = false;
            _isLoadingModels = false;
            _repository = MNNModelRepository.GetDefaultRepository();
            foreach (MNNModelProvider provider in Enum.GetValues(typeof(MNNModelProvider)))
                if (MNNModelCatalogCache.Shared.TryRead(provider, out var cached))
                    _repository.models.AddRange(cached.models);
            MNNModelDownloadTasks.instance.Changed += OnDownloadsChanged;
            RebuildCategoryOptions();
            _sizeFilterLabels = GetLabels("filter.size.any", "filter.size.under100mb", "filter.size.100mb1gb", "filter.size.1gb5gb", "filter.size.over5gb");
            _parameterFilterLabels = GetLabels("filter.params.any", "filter.params.under1b", "filter.params.1b3b", "filter.params.3b7b", "filter.params.over7b");
            _sortLabels = GetLabels("sort.relevance", "sort.name", "sort.sizeascending", "sort.sizedescending", "sort.paramsascending", "sort.paramsdescending", "sort.downloads");
            RefreshModelList();
            CheckInstalledModels();
            RebuildFamilyOptions();
            LoadRepository();
        }

        private void OnDisable()
        {
            _isClosing = true;
            _catalogCancellation?.Cancel();
            _searchCancellation?.Cancel();
            _searchCancellation?.Dispose();
            _searchCancellation = null;
            MNNModelDownloadTasks.instance.Changed -= OnDownloadsChanged;
        }

        private void OnDownloadsChanged()
        {
            CheckInstalledModels();
            Repaint();
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                DrawToolbarSection();
                DrawSearchSection();
                if (!string.IsNullOrEmpty(_repositoryError) && _repository.models.Count > 0)
                    EditorGUILayout.HelpBox(_repositoryError, MessageType.Warning);
                DrawMainContent();
                DrawStatusBarSection();
            }
        }
        private void DrawToolbarSection()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                MNNEditorUI.DrawToolbarTitle("modelmanager.title");
                GUILayout.FlexibleSpace();
                if (_isLoadingModels)
                    GUILayout.Label(MNNLocalization.Get("modelmanager.loading"), EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(_isLoadingModels))
                {
                    if (MNNEditorUI.DrawToolbarButton("common.refresh"))
                        RefreshRepository();
                }

                if (MNNEditorUI.DrawToolbarButton("modelmanager.openfolder"))
                {
                    OpenModelsFolder();
                }
            }
        }

        private void DrawSearchSection()
        {
            if (_workspaceTask != null)
            {
                EditorGUILayout.HelpBox("Models for " + _workspaceTask.Name + ". Check the model card and download the complete repository. " + (_workspaceTask.Runnable ? "Studio availability depends on the model format and media capabilities." : _workspaceTask.UnavailableReason), MessageType.Info);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(_workspaceTask.Name, EditorStyles.boldLabel);
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Show all models", GUILayout.Width(126)))
                    {
                        _workspaceTask = null;
                        RebuildFamilyOptions();
                        RefreshModelList();
                    }
                }
            }

            EditorGUI.BeginChangeCheck();
            _searchQuery = MNNEditorUI.DrawSearchField(_searchQuery);
            if (EditorGUI.EndChangeCheck())
            {
                RefreshModelList();
                SearchOnlineRepository(_searchQuery);
            }
        }

        private void DrawMainContent()
        {
            using (new EditorGUILayout.HorizontalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                var showCategoryPanel = position.width >= THREE_PANE_MIN_WIDTH;
                if (showCategoryPanel)
                {
                    DrawCategoryPanel();
                    DrawVerticalSeparator();
                }

                DrawModelListPanel(showCategoryPanel);
                DrawVerticalSeparator();
                DrawDetailPanel();
            }
        }

        private void DrawStatusBarSection()
        {
            var tasks = MNNModelDownloadTasks.instance;
            var jobs = tasks.Jobs;
            if (jobs.Count > 0)
            {
                var index = jobs.ToList().FindIndex(job => job.Id == _selectedDownloadId);
                if (index < 0)
                    index = jobs.Count - 1;
                if (jobs.Count > 1)
                {
                    index = MNNEditorUI.DrawLabeledPopup("download.tasks", index, jobs.Select(job => job.DisplayName + " · " + MNNLocalization.Get("download." + job.State.ToString().ToLowerInvariant())).ToArray());
                }

                var job = jobs[index];
                _selectedDownloadId = job.Id;
                switch (MNNEditorUI.DrawDownloadStatusBar(job))
                {
                    case MNNDownloadAction.Pause:
                        tasks.Pause(job);
                        break;
                    case MNNDownloadAction.Resume:
                        tasks.Resume(job);
                        break;
                    case MNNDownloadAction.Cancel:
                        tasks.Cancel(job);
                        break;
                    case MNNDownloadAction.Dismiss:
                        tasks.Dismiss(job);
                        break;
                }
            }
            else
            {
                var statusText = $"{_filteredModels.Count} {MNNLocalization.Get("status.models")} | " + $"{_filteredModels.Count(m => m.isInstalled)} {MNNLocalization.Get("modelmanager.installed")}";
                MNNEditorUI.DrawStatusBar(statusText);
            }
        }

        private void DrawVerticalSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(SEPARATOR_WIDTH), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.12f) : new Color(0f, 0f, 0f, 0.12f));
        }

        private static string[] GetLabels(params string[] keys)
        {
            return keys.Select(MNNLocalization.Get).ToArray();
        }

        private static string FormatParameters(MNNModelInfo model)
        {
            if (model.parameterCount <= 0)
                return string.Empty;
            var unit = model.parameterCount >= 100_000_000L ? "B" : "M";
            var value = model.parameterCount / (unit == "B" ? 1_000_000_000d : 1_000_000d);
            return $"{value:0.##}{unit}" + (model.parameterCountInferred ? " " + MNNLocalization.Get("filter.inferred") : "");
        }

        private static string FormatShape(int[] shape)
        {
            return shape == null ? "-" : $"[{string.Join(", ", shape)}]";
        }

        private string GetCategoryDisplayName(MNNModelCategory category)
        {
            return MNNLocalization.Get($"category.{category.ToString().ToLower()}");
        }

        private string GetCategoryDisplayName(string category)
        {
            if (Enum.TryParse(category, true, out MNNModelCategory parsed))
                return GetCategoryDisplayName(parsed);
            return MNNLocalization.Get("category.other");
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
                "LargeLanguageModel" => MNNEditorUI.GetIcon("d_TextAsset Icon"),
                _ => MNNEditorUI.GetIcon("d_Prefab Icon")};
        }
    }
}
