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
            if (!string.IsNullOrEmpty(_selectedModel.supportSummary))
                EditorGUILayout.HelpBox(_selectedModel.supportSummary, MessageType.Info);
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
            if (_selectedModel.isInstalled && _selectedModel.runtimeAvailable && _workspaceTask != null && GUILayout.Button("Open in Studio", GUILayout.Height(30)))
                MNNChatStudio.OpenModelForTask(_selectedModel.localPath, _workspaceTask.Task);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(4);
                using (new EditorGUI.DisabledScope(MNNModelDownloadTasks.instance.HasJobFor(_selectedModel)))
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
    }
}
