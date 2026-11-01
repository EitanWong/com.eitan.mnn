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
    }
}
