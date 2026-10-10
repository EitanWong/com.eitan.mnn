using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // 滚动位置
        private Vector2 _categoryScrollPos;
        private Vector2 _modelListScrollPos;
        private Vector2 _detailScrollPos;
        // 下载状态
        private bool _isLoadingModels;
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
            _repository = MNNModelRepository.GetDefaultRepository();
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
                DrawMainContent();
                DrawStatusBarSection();
            }
        }

#region Layout Sections
        private void DrawToolbarSection()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                MNNEditorUI.DrawToolbarTitle("modelmanager.title");
                GUILayout.FlexibleSpace();
                if (MNNEditorUI.DrawToolbarButton("common.refresh"))
                {
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

#endregion
#region Panel Components
        private void DrawCategoryPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(CATEGORY_WIDTH), GUILayout.ExpandHeight(true)))
            {
                MNNEditorUI.DrawPanelHeader("modelmanager.categories");
                // 分类列表
                _categoryScrollPos = EditorGUILayout.BeginScrollView(_categoryScrollPos);
                {
                    foreach (var category in _categories)
                    {
                        DrawCategoryButton(category);
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawCategoryButton(MNNModelCategory category)
        {
            var count = category == MNNModelCategory.All ? _repository.models.Count : _repository.GetModelsByCategory(category).Count;
            var displayName = GetCategoryDisplayName(category);
            var label = $"{displayName} ({count})";
            var isSelected = _selectedCategory == category;
            if (MNNEditorUI.DrawCategoryButton(label, isSelected))
            {
                _selectedCategory = category;
                ResetDependentFilters();
                RebuildFamilyOptions();
                RefreshModelList();
                _selectedModel = null; // 清除选中的模型
            }
        }

        private void DrawCompactCategoryPicker()
        {
            var selectedIndex = 0;
            for (var i = 0; i < _categories.Length; i++)
            {
                if (_categories[i] == _selectedCategory)
                    selectedIndex = i;
            }

            EditorGUI.BeginChangeCheck();
            selectedIndex = MNNEditorUI.DrawLabeledPopup("modelmanager.type", selectedIndex, _categoryLabels);
            if (EditorGUI.EndChangeCheck())
            {
                _selectedCategory = _categories[selectedIndex];
                ResetDependentFilters();
                RebuildFamilyOptions();
                RefreshModelList();
                _selectedModel = null;
            }
        }

        private void DrawModelListPanel(bool showCategoryPanel)
        {
            var width = showCategoryPanel ? Mathf.Max(MODEL_LIST_MIN_WIDTH, position.width * 0.28f) : Mathf.Max(MODEL_LIST_MIN_WIDTH, position.width * 0.38f);
            using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(MODEL_LIST_MIN_WIDTH), GUILayout.Width(width), GUILayout.ExpandHeight(true)))
            {
                MNNEditorUI.DrawPanelHeader("modelmanager.models", _filteredModels.Count.ToString());
                if (!showCategoryPanel)
                    DrawCompactCategoryPicker();
                if (_familyLabels.Length > 2)
                    DrawFamilyPicker();
                if (_generationLabels.Length > 2)
                    DrawGenerationPicker();
                DrawAdvancedFilters();
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
            if (!string.IsNullOrEmpty(_repositoryError))
            {
                EditorGUILayout.HelpBox(_repositoryError, MessageType.Error);
                return;
            }

            MNNEditorUI.DrawEmptyState(_isLoadingModels ? "modelmanager.loading" : "modelmanager.nomodels", "d_console.infoicon");
        }

        private void DrawFamilyPicker()
        {
            var index = Array.IndexOf(_familyValues, _selectedFamily);
            if (index < 0)
                index = 0;
            EditorGUI.BeginChangeCheck();
            index = MNNEditorUI.DrawLabeledPopup("modelmanager.series", index, _familyLabels);
            if (EditorGUI.EndChangeCheck())
            {
                _selectedFamily = _familyValues[index];
                _selectedGeneration = string.Empty;
                RebuildGenerationOptions();
                RefreshModelList();
                _selectedModel = null;
            }
        }

        private void DrawGenerationPicker()
        {
            var index = Array.IndexOf(_generationValues, _selectedGeneration);
            if (index < 0)
                index = 0;
            EditorGUI.BeginChangeCheck();
            index = MNNEditorUI.DrawLabeledPopup("modelmanager.generation", index, _generationLabels);
            if (EditorGUI.EndChangeCheck())
            {
                _selectedGeneration = _generationValues[index];
                RefreshModelList();
                _selectedModel = null;
            }
        }

        private void DrawAdvancedFilters()
        {
            EditorGUI.BeginChangeCheck();
            _sortMode = MNNEditorUI.DrawLabeledPopup("modelmanager.sort", _sortMode, _sortLabels);
            _showAdvancedFilters = EditorGUILayout.Foldout(_showAdvancedFilters, MNNLocalization.Get(_sizeFilter != 0 || _parameterFilter != 0 ? "filter.advancedactive" : "filter.advanced"), true);
            if (_showAdvancedFilters)
            {
                _sizeFilter = MNNEditorUI.DrawLabeledPopup("modelmanager.size", _sizeFilter, _sizeFilterLabels);
                _parameterFilter = MNNEditorUI.DrawLabeledPopup("modelmanager.parameters", _parameterFilter, _parameterFilterLabels);
            }

            if (EditorGUI.EndChangeCheck())
                RefreshModelList();
        }

        private void DrawModelCard(MNNModelInfo model)
        {
            MNNEditorUI.DrawCard(_selectedModel == model, () =>
            {
                DrawModelCardHeader(model);
                DrawModelCardBody(model);
                DrawModelCardFooter(model);
            }, () =>
            {
                _selectedModel = model;
                Repaint();
            });
        }

        private void DrawModelCardHeader(MNNModelInfo model)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                // 图标
                var icon = GetCategoryIcon(model.category);
                GUILayout.Label(icon, GUILayout.Width(30), GUILayout.Height(30));
                // 信息
                using (new EditorGUILayout.VerticalScope())
                {
                    EditorGUILayout.LabelField(model.displayName, EditorStyles.boldLabel, GUILayout.MinWidth(0));
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.LabelField($"v{model.version}", EditorStyles.miniLabel, GUILayout.Width(42));
                        EditorGUILayout.LabelField(GetCategoryDisplayName(model.category), EditorStyles.miniLabel, GUILayout.MinWidth(0));
                    }
                }

                GUILayout.FlexibleSpace();
                // 安装状态
                if (model.isInstalled)
                {
                    GUILayout.Label(MNNEditorUI.GetIcon("TestPassed"), GUILayout.Width(18), GUILayout.Height(18));
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
                        MNNEditorUI.DrawTag(tag);
                    }
                }
            }
        }

        private void DrawModelCardFooter(MNNModelInfo model)
        {
            var parameters = FormatParameters(model);
            var summary = $"{MNNLocalization.Get("status.size")} {MNNEditorUI.FormatFileSize(model.fileSize)}";
            if (!string.IsNullOrEmpty(parameters))
                summary += "  ·  " + parameters;
            EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);
        }

        private void DrawDetailPanel()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true)))
            {
                MNNEditorUI.DrawPanelHeader("modelmanager.details");
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
            _detailScrollPos = EditorGUILayout.BeginScrollView(_detailScrollPos, GUILayout.ExpandHeight(true));
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

            EditorGUILayout.EndScrollView();
            DrawActionButtons();
        }

        private void DrawBasicInfo()
        {
            MNNEditorUI.DrawSectionHeader("detail.name");
            EditorGUILayout.LabelField(_selectedModel.displayName, EditorStyles.boldLabel, GUILayout.MinWidth(0));
            GUILayout.Space(3);
            MNNEditorUI.DrawInfoRow("detail.category", GetCategoryDisplayName(_selectedModel.category));
            if (!string.IsNullOrEmpty(_selectedModel.family))
                MNNEditorUI.DrawInfoRow("modelmanager.series", _selectedModel.family);
            if (!string.IsNullOrEmpty(_selectedModel.generation))
                MNNEditorUI.DrawInfoRow("modelmanager.generation", _selectedModel.generation);
            var parameters = FormatParameters(_selectedModel);
            if (!string.IsNullOrEmpty(parameters))
                MNNEditorUI.DrawInfoRow("modelmanager.parameters", parameters);
            MNNEditorUI.DrawInfoRow("detail.version", _selectedModel.version);
            MNNEditorUI.DrawInfoRow("detail.author", _selectedModel.author);
        }

        private void DrawDescription()
        {
            MNNEditorUI.DrawSectionHeader("detail.description");
            EditorGUILayout.LabelField(_selectedModel.description ?? string.Empty, EditorStyles.wordWrappedLabel);
        }

        private void DrawInputSpec()
        {
            if (_selectedModel.inputSpec == null)
                return;
            MNNEditorUI.DrawSectionHeader("detail.inputspec");
            MNNEditorUI.DrawInfoRow("detail.shape", FormatShape(_selectedModel.inputSpec.shape));
            MNNEditorUI.DrawInfoRow("detail.format", _selectedModel.inputSpec.format);
            MNNEditorUI.DrawInfoRow("detail.type", _selectedModel.inputSpec.dataType);
        }

        private void DrawOutputSpec()
        {
            if (_selectedModel.outputSpec == null)
                return;
            MNNEditorUI.DrawSectionHeader("detail.outputspec");
            MNNEditorUI.DrawInfoRow("detail.shape", FormatShape(_selectedModel.outputSpec.shape));
            MNNEditorUI.DrawInfoRow("detail.type", _selectedModel.outputSpec.dataType);
            if (_selectedModel.outputSpec.classCount > 0)
            {
                MNNEditorUI.DrawInfoRow("detail.classes", _selectedModel.outputSpec.classCount.ToString());
            }
        }

        private void DrawFileInfo()
        {
            MNNEditorUI.DrawInfoRow("detail.filesize", MNNEditorUI.FormatFileSize(_selectedModel.fileSize));
            var parameters = FormatParameters(_selectedModel);
            if (!string.IsNullOrEmpty(parameters))
                MNNEditorUI.DrawInfoRow("modelmanager.parameters", parameters);
            MNNEditorUI.DrawInfoRow("detail.downloads", _selectedModel.downloadCount.ToString("N0"));
            if (_selectedModel.isInstalled)
            {
                MNNEditorUI.DrawSectionHeader("detail.localpath");
                EditorGUILayout.SelectableLabel(_selectedModel.localPath ?? string.Empty, EditorStyles.miniLabel, GUILayout.MinHeight(EditorGUIUtility.singleLineHeight * 2));
            }
        }

        private void DrawActionButtons()
        {
            if (_selectedModel.isInstalled && _workspaceTask != null && GUILayout.Button("Open in Studio", GUILayout.Height(30)))
                MNNChatStudio.OpenModelForTask(_selectedModel.localPath, _workspaceTask.Task);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4);
                using (new EditorGUI.DisabledScope(MNNModelDownloadTasks.instance.HasJobFor(_selectedModel.modelScopeId)))
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

                GUILayout.Space(4);
            }
        }

        private void DrawEmptyDetails()
        {
            MNNEditorUI.DrawEmptyState("modelmanager.selectmodel", "d_UnityEditor.InspectorWindow");
        }

        private void DrawVerticalSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, GUILayout.Width(SEPARATOR_WIDTH), GUILayout.ExpandHeight(true));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.12f) : new Color(0f, 0f, 0f, 0.12f));
        }

#endregion
#region Helper Methods
        private void RefreshModelList()
        {
            IEnumerable<MNNModelInfo> results = string.IsNullOrWhiteSpace(_searchQuery) ? _repository.GetModelsByCategory(_selectedCategory) : _repository.SearchModels(_searchQuery).Where(m => _selectedCategory == MNNModelCategory.All || m.category == _selectedCategory.ToString());
            if (_workspaceTask != null)
                results = results.Where(_workspaceTask.MatchesCatalog);
            if (!string.IsNullOrEmpty(_selectedFamily))
                results = results.Where(m => m.family == _selectedFamily);
            if (!string.IsNullOrEmpty(_selectedGeneration))
                results = results.Where(m => m.generation == _selectedGeneration);
            results = results.Where(MatchesSizeFilter).Where(MatchesParameterFilter);
            switch (_sortMode)
            {
                case 1:
                    results = results.OrderBy(m => m.displayName, StringComparer.OrdinalIgnoreCase);
                    break;
                case 2:
                    results = results.OrderBy(m => m.fileSize == 0).ThenBy(m => m.fileSize);
                    break;
                case 3:
                    results = results.OrderBy(m => m.fileSize == 0).ThenByDescending(m => m.fileSize);
                    break;
                case 4:
                    results = results.OrderBy(m => m.parameterCount == 0).ThenBy(m => m.parameterCount);
                    break;
                case 5:
                    results = results.OrderBy(m => m.parameterCount == 0).ThenByDescending(m => m.parameterCount);
                    break;
                case 6:
                    results = results.OrderByDescending(m => m.downloadCount);
                    break;
            }

            _filteredModels = results.ToList();
            if (_selectedModel != null && !_filteredModels.Contains(_selectedModel))
                _selectedModel = null;
        }

        private bool MatchesSizeFilter(MNNModelInfo model)
        {
            const long mb = 1024L * 1024L;
            var size = model.fileSize;
            switch (_sizeFilter)
            {
                case 1:
                    return size > 0 && size < 100 * mb;
                case 2:
                    return size >= 100 * mb && size < 1024 * mb;
                case 3:
                    return size >= 1024 * mb && size < 5 * 1024 * mb;
                case 4:
                    return size >= 5 * 1024 * mb;
                default:
                    return true;
            }
        }

        private bool MatchesParameterFilter(MNNModelInfo model)
        {
            const long billion = 1_000_000_000L;
            switch (_parameterFilter)
            {
                case 1:
                    return model.parameterCount > 0 && model.parameterCount < billion;
                case 2:
                    return model.parameterCount >= billion && model.parameterCount < 3 * billion;
                case 3:
                    return model.parameterCount >= 3 * billion && model.parameterCount < 7 * billion;
                case 4:
                    return model.parameterCount >= 7 * billion;
                default:
                    return true;
            }
        }

        private void ResetDependentFilters()
        {
            _selectedFamily = string.Empty;
            _selectedGeneration = string.Empty;
        }

        private void RebuildFamilyOptions()
        {
            var families = _repository.GetModelsByCategory(_selectedCategory).Where(model => _workspaceTask == null || _workspaceTask.MatchesCatalog(model)).Where(model => !string.IsNullOrEmpty(model.family)).GroupBy(model => model.family, StringComparer.OrdinalIgnoreCase).Where(group => group.Count() > 1).Select(group => group.Key).OrderBy(family => family, StringComparer.OrdinalIgnoreCase).ToArray();
            _familyValues = new[]{string.Empty}.Concat(families).ToArray();
            _familyLabels = new[]{MNNLocalization.Get("filter.allseries")}.Concat(families).ToArray();
            if (!_familyValues.Contains(_selectedFamily, StringComparer.OrdinalIgnoreCase))
                _selectedFamily = string.Empty;
            if (_familyValues.Length <= 2)
                _selectedFamily = string.Empty;
            RebuildGenerationOptions();
        }

        private void RebuildGenerationOptions()
        {
            var generations = string.IsNullOrEmpty(_selectedFamily) ? Array.Empty<string>() : _repository.GetModelsByCategory(_selectedCategory).Where(model => model.family == _selectedFamily).Select(model => model.generation).Where(generation => !string.IsNullOrEmpty(generation)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(generation => generation, StringComparer.OrdinalIgnoreCase).ToArray();
            _generationValues = new[]{string.Empty}.Concat(generations).ToArray();
            _generationLabels = new[]{MNNLocalization.Get("filter.allgenerations")}.Concat(generations).ToArray();
            if (_generationValues.Length <= 2)
                _selectedGeneration = string.Empty;
            if (!_generationValues.Contains(_selectedGeneration, StringComparer.OrdinalIgnoreCase))
                _selectedGeneration = string.Empty;
        }

        private void RebuildCategoryOptions()
        {
            var available = _repository.models.Select(model => Enum.TryParse(model.category, true, out MNNModelCategory category) ? category : MNNModelCategory.All).Where(category => category != MNNModelCategory.All).Distinct().OrderBy(GetCategoryDisplayName, StringComparer.OrdinalIgnoreCase).ToArray();
            _categories = new[]{MNNModelCategory.All}.Concat(available).ToArray();
            _categoryLabels = _categories.Select(GetCategoryDisplayName).ToArray();
            if (!_categories.Contains(_selectedCategory))
                _selectedCategory = MNNModelCategory.All;
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

        private void CheckInstalledModels()
        {
            foreach (var model in _repository.models)
            {
                var path = GetInstalledPath(model);
                model.isInstalled = model.isRepository ? Directory.Exists(path) && File.Exists(Path.Combine(path, INSTALL_MARKER)) : File.Exists(path);
                if (model.isInstalled)
                {
                    model.localPath = path;
                }
                else
                    model.localPath = null;
            }
        }

        private void DownloadModel(MNNModelInfo model)
        {
            try
            {
                var job = MNNModelDownloadTasks.instance.StartDownload(model.modelScopeId, model.displayName, Path.Combine(DOWNLOAD_STAGING_FOLDER, SanitizeFolderName(model.name)), GetInstalledPath(model));
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

        private static string GetInstalledPath(MNNModelInfo model)
        {
            return Path.Combine(MODELS_FOLDER, SanitizeFolderName(model.name));
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

        private void RefreshRepository()
        {
            LoadRepository();
        }

        private async void LoadRepository()
        {
            if (_isLoadingModels)
                return;
            _isLoadingModels = true;
            _repositoryError = null;
            Repaint();
            try
            {
                var repository = MNNModelRepository.GetDefaultRepository();
                await repository.LoadModelScopeModelsAsync();
                if (_isClosing)
                    return;
                _repository = repository;
                RebuildCategoryOptions();
                RebuildFamilyOptions();
                CheckInstalledModels();
                RefreshModelList();
            }
            catch (Exception exception)
            {
                if (_isClosing)
                    return;
                _repositoryError = exception.Message;
                Debug.LogError("Failed to load MNN models from ModelScope: " + exception);
                MNNEditorUI.ShowErrorDialog("Failed to load models from ModelScope:\n" + exception.Message);
            }
            finally
            {
                _isLoadingModels = false;
                if (!_isClosing)
                    Repaint();
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
#endregion
    }
}
