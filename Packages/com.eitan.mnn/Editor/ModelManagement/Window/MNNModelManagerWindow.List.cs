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
        private float _modelListViewportHeight = 500f;
        private const float ModelCardHeight = 78f;
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
                float contentBottom;
                {
                    if (_filteredModels.Count == 0)
                    {
                        DrawEmptyModelList();
                    }
                    else
                    {
                        int start = Mathf.Clamp(Mathf.FloorToInt(_modelListScrollPos.y / ModelCardHeight) - 2, 0, _filteredModels.Count);
                        int end = Mathf.Min(_filteredModels.Count, start + Mathf.CeilToInt(_modelListViewportHeight / ModelCardHeight) + 5);
                        GUILayout.Space(start * ModelCardHeight);
                        for (int i = start; i < end; ++i) DrawVirtualModelCard(_filteredModels[i]);
                        GUILayout.Space((_filteredModels.Count - end) * ModelCardHeight);
                    }
                    contentBottom = GUILayoutUtility.GetLastRect().yMax;
                }

                EditorGUILayout.EndScrollView();
                var viewport = GUILayoutUtility.GetLastRect();
                if (Event.current.type == EventType.Repaint && viewport.height > 0) _modelListViewportHeight = viewport.height;
                if (Event.current.type == EventType.Repaint && HasMoreVisibleRepository && !_loadMorePending &&
                    _modelListScrollPos.y + viewport.height >= contentBottom - 100f)
                    EditorApplication.delayCall += LoadMoreRepository;
            }
        }

        private void DrawVirtualModelCard(MNNModelInfo model)
        {
            Rect rect = GUILayoutUtility.GetRect(1, ModelCardHeight, GUILayout.ExpandWidth(true));
            var card = new Rect(rect.x, rect.y, rect.width, rect.height - 2);
            GUI.Box(card, GUIContent.none, _selectedModel == model ? MNNEditorUI.SelectedCardStyle : MNNEditorUI.CardStyle);
            float x = card.x + 10, width = Mathf.Max(0, card.width - 20);
            GUI.Label(new Rect(x, card.y + 7, width, 20), new GUIContent(model.displayName, model.repositoryId), EditorStyles.boldLabel);
            GUI.Label(new Rect(x, card.y + 28, width, 18), GetCategoryDisplayName(model.category), EditorStyles.miniLabel);
            string summary = MNNEditorUI.FormatFileSize(model.fileSize);
            string parameters = FormatParameters(model);
            if (!string.IsNullOrEmpty(parameters)) summary += "  ·  " + parameters;
            if (model.isInstalled) summary += "  ·  Installed";
            GUI.Label(new Rect(x, card.y + 48, width, 18), summary, EditorStyles.miniLabel);
            if (Event.current.type == EventType.MouseDown && card.Contains(Event.current.mousePosition))
            {
                _selectedModel = model;
                Repaint();
                Event.current.Use();
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

    }
}
