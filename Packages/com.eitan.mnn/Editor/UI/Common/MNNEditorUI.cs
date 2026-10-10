using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor.UI
{
    /// <summary>
    /// Shared IMGUI components for MNN editor tools.
    /// </summary>
    public enum MNNDownloadAction
    {
        None,
        Pause,
        Resume,
        Cancel,
        Dismiss
    }

    public static class MNNEditorUI
    {
        private static readonly Dictionary<string, Texture2D> TextureCache = new Dictionary<string, Texture2D>();
        private static readonly string[] FileSizeUnits = {"B", "KB", "MB", "GB", "TB"};
        private static GUIStyle _headerStyle;
        private static GUIStyle _cardStyle;
        private static GUIStyle _selectedCardStyle;
        private static GUIStyle _categoryButtonStyle;
        private static GUIStyle _selectedCategoryButtonStyle;
        private static GUIStyle _toolbarButtonStyle;
        private static GUIStyle _tagStyle;
        private static GUIStyle _sectionStyle;
        private static GUIStyle _wrappedValueStyle;
        private static GUIStyle _emptyStateStyle;
        public static GUIStyle HeaderStyle => _headerStyle ?? (_headerStyle = new GUIStyle(EditorStyles.boldLabel)
        {fontSize = 12, alignment = TextAnchor.MiddleLeft, margin = new RectOffset(8, 8, 0, 0)});
        public static GUIStyle CardStyle => _cardStyle ?? (_cardStyle = new GUIStyle(EditorStyles.helpBox)
        {margin = new RectOffset(4, 4, 3, 3), padding = new RectOffset(9, 9, 7, 7)});
        public static GUIStyle SelectedCardStyle => _selectedCardStyle ?? (_selectedCardStyle = new GUIStyle(CardStyle)
        {normal = {background = GetColorTexture(EditorGUIUtility.isProSkin ? new Color(0.20f, 0.38f, 0.55f, 0.42f) : new Color(0.20f, 0.48f, 0.78f, 0.16f))}, hover = {background = GetColorTexture(EditorGUIUtility.isProSkin ? new Color(0.20f, 0.38f, 0.55f, 0.48f) : new Color(0.20f, 0.48f, 0.78f, 0.22f))}});
        public static GUIStyle CategoryButtonStyle => _categoryButtonStyle ?? (_categoryButtonStyle = new GUIStyle(EditorStyles.miniButton)
        {fixedHeight = 26, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(9, 8, 0, 0), margin = new RectOffset(4, 4, 1, 1)});
        private static GUIStyle SelectedCategoryButtonStyle => _selectedCategoryButtonStyle ?? (_selectedCategoryButtonStyle = new GUIStyle(CategoryButtonStyle)
        {fontStyle = FontStyle.Bold});
        public static GUIStyle ToolbarButtonStyle => _toolbarButtonStyle ?? (_toolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
        {padding = new RectOffset(8, 8, 0, 0)});
        private static GUIStyle TagStyle => _tagStyle ?? (_tagStyle = new GUIStyle(EditorStyles.miniLabel)
        {normal = {background = GetColorTexture(EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0.06f))}, padding = new RectOffset(5, 5, 2, 2), margin = new RectOffset(0, 4, 2, 2)});
        private static GUIStyle SectionStyle => _sectionStyle ?? (_sectionStyle = new GUIStyle(EditorStyles.boldLabel)
        {margin = new RectOffset(0, 0, 7, 3)});
        private static GUIStyle WrappedValueStyle => _wrappedValueStyle ?? (_wrappedValueStyle = new GUIStyle(EditorStyles.label)
        {wordWrap = true, alignment = TextAnchor.UpperLeft});
        private static GUIStyle EmptyStateStyle => _emptyStateStyle ?? (_emptyStateStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel)
        {wordWrap = true, alignment = TextAnchor.MiddleCenter, padding = new RectOffset(12, 12, 4, 4)});
        public static void DrawToolbarTitle(string key, int width = -1)
        {
            if (width > 0)
                GUILayout.Label(MNNLocalization.Get(key), HeaderStyle, GUILayout.Width(width));
            else
                GUILayout.Label(MNNLocalization.Get(key), HeaderStyle);
        }

        public static bool DrawToolbarButton(string key, int width = -1)
        {
            return width > 0 ? GUILayout.Button(MNNLocalization.Get(key), ToolbarButtonStyle, GUILayout.Width(width)) : GUILayout.Button(MNNLocalization.Get(key), ToolbarButtonStyle);
        }

        public static string DrawSearchField(string value)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Space(5);
                var rect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
                value = value ?? string.Empty;
                var cancelRect = new Rect(rect.xMax - 18, rect.y, 18, rect.height);
                var fieldRect = new Rect(rect.x, rect.y, rect.width - 18, rect.height);
                value = EditorGUI.TextField(fieldRect, value, EditorStyles.toolbarSearchField);
                var cancelStyleName = string.IsNullOrEmpty(value) ? "ToolbarSearchFieldCancelButtonEmpty" : "ToolbarSearchFieldCancelButton";
                var cancelStyle = GUI.skin.FindStyle(cancelStyleName) ?? EditorStyles.toolbarButton;
                var cancelContent = cancelStyle == EditorStyles.toolbarButton ? new GUIContent("x") : GUIContent.none;
                if (GUI.Button(cancelRect, cancelContent, cancelStyle))
                {
                    value = string.Empty;
                    GUI.FocusControl(null);
                }

                GUILayout.Space(5);
            }

            return value;
        }

        public static void DrawPanelHeader(string titleKey, string trailingText = null)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(22)))
            {
                GUILayout.Label(MNNLocalization.Get(titleKey), HeaderStyle);
                if (!string.IsNullOrEmpty(trailingText))
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(trailingText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                    GUILayout.Space(7);
                }
            }
        }

        public static void DrawSectionHeader(string titleKey)
        {
            GUILayout.Label(MNNLocalization.Get(titleKey), SectionStyle);
        }

        public static void DrawCard(bool selected, Action content, Action clicked = null)
        {
            var rect = EditorGUILayout.BeginVertical(selected ? SelectedCardStyle : CardStyle);
            content?.Invoke();
            EditorGUILayout.EndVertical();
            if (clicked != null && Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                clicked();
                Event.current.Use();
            }
        }

        public static void DrawTag(string text)
        {
            GUILayout.Label(text, TagStyle, GUILayout.ExpandWidth(false));
        }

        public static bool DrawCategoryButton(string label, bool selected)
        {
            return GUILayout.Button(label, selected ? SelectedCategoryButtonStyle : CategoryButtonStyle);
        }

        public static int DrawLabeledPopup(string labelKey, int selectedIndex, string[] options, int labelWidth = 72)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(6);
                GUILayout.Label(MNNLocalization.Get(labelKey), EditorStyles.miniLabel, GUILayout.Width(labelWidth));
                selectedIndex = EditorGUILayout.Popup(selectedIndex, options, GUILayout.MinWidth(0));
                GUILayout.Space(6);
            }

            return selectedIndex;
        }

        public static void DrawInfoRow(string labelKey, string value, bool localize = true)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(localize ? MNNLocalization.Get(labelKey) : labelKey, EditorStyles.miniLabel, GUILayout.Width(90));
                EditorGUILayout.LabelField(value ?? string.Empty, WrappedValueStyle, GUILayout.MinWidth(0));
            }
        }

        public static bool DrawPrimaryButton(string labelKey, int height = 30, bool localize = true)
        {
            var content = localize ? MNNLocalization.Get(labelKey) : labelKey;
            return GUILayout.Button(content, GUILayout.Height(height), GUILayout.MinWidth(90));
        }

        public static bool DrawSecondaryButton(string labelKey, int height = 30, bool localize = true)
        {
            var content = localize ? MNNLocalization.Get(labelKey) : labelKey;
            return GUILayout.Button(content, GUILayout.Height(height), GUILayout.MinWidth(72));
        }

        public static void DrawSeparator(int height = 1)
        {
            GUILayout.Space(3);
            var rect = GUILayoutUtility.GetRect(1, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.12f) : new Color(0f, 0f, 0f, 0.12f));
            GUILayout.Space(3);
        }

        public static void DrawStatusBar(string leftText, string rightText = null)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar, GUILayout.Height(20)))
            {
                GUILayout.Space(7);
                GUILayout.Label(leftText, EditorStyles.miniLabel, GUILayout.MinWidth(0));
                if (!string.IsNullOrEmpty(rightText))
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(rightText, EditorStyles.miniLabel, GUILayout.ExpandWidth(false));
                    GUILayout.Space(7);
                }
            }
        }

        public static MNNDownloadAction DrawDownloadStatusBar(MNNModelDownloadJob job)
        {
            var action = MNNDownloadAction.None;
            var rect = GUILayoutUtility.GetRect(0, string.IsNullOrEmpty(job.Error) ? 88 : 110, GUILayout.ExpandWidth(true));
            var inset = new Rect(rect.x + 12, rect.y + 9, Mathf.Max(0, rect.width - 24), rect.height - 18);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), EditorGUIUtility.isProSkin ? new Color(1, 1, 1, 0.12f) : new Color(0, 0, 0, 0.12f));
            var title = new GUIStyle(EditorStyles.boldLabel)
            {clipping = TextClipping.Clip};
            var secondary = new GUIStyle(EditorStyles.miniLabel)
            {clipping = TextClipping.Clip};
            var trailing = new GUIStyle(secondary)
            {alignment = TextAnchor.MiddleRight};
            var state = MNNLocalization.Get("download." + job.State.ToString().ToLowerInvariant());
            GUI.Label(new Rect(inset.x, inset.y, inset.width - 240, 20), new GUIContent(job.DisplayName, job.DisplayName), title);
            var buttonX = inset.xMax - 170;
            if (job.State == MNNDownloadState.Downloading)
            {
                if (GUI.Button(new Rect(buttonX, inset.y, 80, 22), MNNLocalization.Get("download.pause")))
                    action = MNNDownloadAction.Pause;
            }
            else if (job.CanResume)
            {
                if (GUI.Button(new Rect(buttonX, inset.y, 80, 22), MNNLocalization.Get("download.resume")))
                    action = MNNDownloadAction.Resume;
            }

            if (job.IsRunning || job.CanResume)
            {
                using (new EditorGUI.DisabledScope(job.State == MNNDownloadState.Cancelling))
                    if (GUI.Button(new Rect(buttonX + 90, inset.y, 80, 22), MNNLocalization.Get("common.cancel")))
                        action = MNNDownloadAction.Cancel;
            }
            else if (GUI.Button(new Rect(buttonX + 90, inset.y, 80, 22), MNNLocalization.Get("download.dismiss")))
                action = MNNDownloadAction.Dismiss;
            var metrics = FormatFileSize(job.ReceivedBytes) + " / " + FormatFileSize(job.TotalBytes);
            if (job.State == MNNDownloadState.Downloading && job.BytesPerSecond > 0)
                metrics += "  ·  " + FormatFileSize((long)job.BytesPerSecond) + "/s";
            metrics += "  ·  " + job.Progress.ToString("P0");
            GUI.Label(new Rect(inset.x, inset.y + 27, inset.width * 0.40f, 18), new GUIContent(state + (string.IsNullOrEmpty(job.CurrentFile) ? "" : " · " + PathFileName(job.CurrentFile)), job.CurrentFile), secondary);
            GUI.Label(new Rect(inset.x + inset.width * 0.42f, inset.y + 27, inset.width * 0.58f, 18), metrics, trailing);
            var track = new Rect(inset.x, inset.y + 52, inset.width, 5);
            GUI.DrawTexture(track, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, EditorGUIUtility.isProSkin ? new Color(1, 1, 1, 0.10f) : new Color(0, 0, 0, 0.10f), 0, 3);
            if (job.Progress > 0)
                GUI.DrawTexture(new Rect(track.x, track.y, track.width * job.Progress, track.height), Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, job.State == MNNDownloadState.Failed ? new Color(0.95f, 0.37f, 0.31f) : job.State == MNNDownloadState.Paused ? new Color(0.65f, 0.65f, 0.68f) : new Color(0.10f, 0.48f, 0.98f), 0, 3);
            if (!string.IsNullOrEmpty(job.Error))
                GUI.Label(new Rect(inset.x, inset.y + 66, inset.width, 18), new GUIContent(job.Error, job.Error), secondary);
            return action;
        }

        private static string PathFileName(string path) => System.IO.Path.GetFileName(path);
        public static void DrawEmptyState(string messageKey, string iconName = null)
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.MinHeight(74), GUILayout.MaxHeight(92)))
            {
                if (!string.IsNullOrEmpty(iconName))
                {
                    var icon = GetIcon(iconName);
                    GUILayout.Label(icon, GUILayout.Height(28));
                }

                GUILayout.Label(MNNLocalization.Get(messageKey), EmptyStateStyle, GUILayout.MinHeight(24));
            }
        }

        public static bool ShowConfirmDialog(string titleKey, string message, string okKey = "common.ok", string cancelKey = "common.cancel")
        {
            return EditorUtility.DisplayDialog(MNNLocalization.Get(titleKey), message, MNNLocalization.Get(okKey), MNNLocalization.Get(cancelKey));
        }

        public static void ShowInfoDialog(string titleKey, string message, string okKey = "common.ok")
        {
            EditorUtility.DisplayDialog(MNNLocalization.Get(titleKey), message, MNNLocalization.Get(okKey));
        }

        public static void ShowErrorDialog(string message)
        {
            EditorUtility.DisplayDialog(MNNLocalization.Get("common.error"), message, MNNLocalization.Get("common.ok"));
        }

        public static Texture2D GetColorTexture(Color color)
        {
            var key = ColorUtility.ToHtmlStringRGBA(color);
            if (TextureCache.TryGetValue(key, out var cached))
                return cached;
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {hideFlags = HideFlags.HideAndDontSave, name = "MNN Editor UI"};
            texture.SetPixel(0, 0, color);
            texture.Apply();
            TextureCache.Add(key, texture);
            return texture;
        }

        public static string FormatFileSize(long bytes)
        {
            var value = Math.Max(0L, bytes);
            var size = (double)value;
            var unit = 0;
            while (size >= 1024 && unit < FileSizeUnits.Length - 1)
            {
                size /= 1024;
                unit++;
            }

            return $"{size:0.##} {FileSizeUnits[unit]}";
        }

        public static GUIContent GetIcon(string iconName)
        {
            return EditorGUIUtility.IconContent(iconName);
        }
    }
}
