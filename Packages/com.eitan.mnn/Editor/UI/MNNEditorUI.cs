using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor.UI
{
    /// <summary>
    /// MNN编辑器UI组件库 - 可复用的UI组件
    /// </summary>
    public static class MNNEditorUI
    {
        #region Styles Cache

        private static GUIStyle _headerStyle;
        private static GUIStyle _subHeaderStyle;
        private static GUIStyle _cardStyle;
        private static GUIStyle _selectedCardStyle;
        private static GUIStyle _categoryButtonStyle;
        private static GUIStyle _toolbarButtonStyle;
        private static Dictionary<string, Texture2D> _textureCache;

        static MNNEditorUI()
        {
            _textureCache = new Dictionary<string, Texture2D>();
        }

        /// <summary>
        /// 头部标题样式
        /// </summary>
        public static GUIStyle HeaderStyle
        {
            get
            {
                if (_headerStyle == null)
                {
                    _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 18,
                        margin = new RectOffset(10, 10, 10, 10),
                        fontStyle = FontStyle.Bold
                    };
                }
                return _headerStyle;
            }
        }

        /// <summary>
        /// 副标题样式
        /// </summary>
        public static GUIStyle SubHeaderStyle
        {
            get
            {
                if (_subHeaderStyle == null)
                {
                    _subHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 14,
                        margin = new RectOffset(5, 5, 5, 5),
                        fontStyle = FontStyle.Bold
                    };
                }
                return _subHeaderStyle;
            }
        }

        /// <summary>
        /// 卡片样式
        /// </summary>
        public static GUIStyle CardStyle
        {
            get
            {
                if (_cardStyle == null)
                {
                    _cardStyle = new GUIStyle(EditorStyles.helpBox)
                    {
                        margin = new RectOffset(5, 5, 5, 5),
                        padding = new RectOffset(10, 10, 10, 10)
                    };
                }
                return _cardStyle;
            }
        }

        /// <summary>
        /// 选中卡片样式
        /// </summary>
        public static GUIStyle SelectedCardStyle
        {
            get
            {
                if (_selectedCardStyle == null)
                {
                    _selectedCardStyle = new GUIStyle(CardStyle)
                    {
                        normal = { background = GetColorTexture(new Color(0.3f, 0.5f, 0.8f, 0.3f)) }
                    };
                }
                return _selectedCardStyle;
            }
        }

        /// <summary>
        /// 分类按钮样式
        /// </summary>
        public static GUIStyle CategoryButtonStyle
        {
            get
            {
                if (_categoryButtonStyle == null)
                {
                    _categoryButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
                    {
                        fixedHeight = 30,
                        fontSize = 12,
                        alignment = TextAnchor.MiddleLeft,
                        padding = new RectOffset(10, 10, 0, 0)
                    };
                }
                return _categoryButtonStyle;
            }
        }

        /// <summary>
        /// 工具栏按钮样式
        /// </summary>
        public static GUIStyle ToolbarButtonStyle
        {
            get
            {
                if (_toolbarButtonStyle == null)
                {
                    _toolbarButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
                    {
                        fontSize = 11
                    };
                }
                return _toolbarButtonStyle;
            }
        }

        #endregion

        #region Toolbar Components

        /// <summary>
        /// 绘制工具栏
        /// </summary>
        public static void DrawToolbar(Action content)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            content?.Invoke();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制工具栏标题
        /// </summary>
        public static void DrawToolbarTitle(string title, int width = -1)
        {
            var options = width > 0 ? new[] { GUILayout.Width(width) } : Array.Empty<GUILayoutOption>();
            GUILayout.Label(MNNLocalization.Get(title), HeaderStyle, options);
        }

        /// <summary>
        /// 绘制工具栏按钮
        /// </summary>
        public static bool DrawToolbarButton(string label, int width = -1)
        {
            var localizedLabel = MNNLocalization.Get(label);
            var options = width > 0
                ? new[] { GUILayout.Width(width) }
                : Array.Empty<GUILayoutOption>();

            return GUILayout.Button(localizedLabel, ToolbarButtonStyle, options);
        }

        /// <summary>
        /// 绘制搜索栏
        /// </summary>
        public static string DrawSearchField(string searchQuery)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Label(MNNLocalization.Get("common.search") + ":", GUILayout.Width(50));

                searchQuery = EditorGUILayout.TextField(searchQuery, EditorStyles.toolbarSearchField);

                if (GUILayout.Button("", EditorStyles.toolbarButton, GUILayout.Width(20)))
                {
                    searchQuery = "";
                    GUI.FocusControl(null);
                }
            }
            EditorGUILayout.EndHorizontal();

            return searchQuery;
        }

        #endregion

        #region Panel Components

        /// <summary>
        /// 绘制侧边栏面板
        /// </summary>
        public static void DrawSidePanel(float width, Action content, ref Vector2 scrollPosition)
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(width));
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Width(width));
                content?.Invoke();
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制主内容面板
        /// </summary>
        public static void DrawContentPanel(Action content, ref Vector2 scrollPosition)
        {
            EditorGUILayout.BeginVertical();
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
                content?.Invoke();
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制分割线
        /// </summary>
        public static void DrawSeparator(int height = 1)
        {
            var rect = GUILayoutUtility.GetRect(1, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        }

        #endregion

        #region Card Components

        /// <summary>
        /// 绘制可点击卡片
        /// </summary>
        /// <param name="isSelected">是否选中</param>
        /// <param name="content">卡片内容</param>
        /// <param name="onClicked">点击回调</param>
        public static void DrawCard(bool isSelected, Action content, Action onClicked = null)
        {
            var style = isSelected ? SelectedCardStyle : CardStyle;

            EditorGUILayout.BeginVertical(style);
            content?.Invoke();
            EditorGUILayout.EndVertical();

            // 检测点击
            if (onClicked != null && Event.current.type == EventType.MouseDown &&
                GUILayoutUtility.GetLastRect().Contains(Event.current.mousePosition))
            {
                onClicked.Invoke();
                Event.current.Use();
            }
        }

        /// <summary>
        /// 绘制带图标的卡片头部
        /// </summary>
        public static void DrawCardHeader(GUIContent icon, string title, string subtitle = null, Action rightContent = null)
        {
            EditorGUILayout.BeginHorizontal();
            {
                // 图标
                if (icon != null)
                {
                    GUILayout.Label(icon, GUILayout.Width(40), GUILayout.Height(40));
                }

                // 标题和副标题
                EditorGUILayout.BeginVertical();
                {
                    EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
                    if (!string.IsNullOrEmpty(subtitle))
                    {
                        EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);
                    }
                }
                EditorGUILayout.EndVertical();

                GUILayout.FlexibleSpace();

                // 右侧内容
                rightContent?.Invoke();
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制信息行
        /// </summary>
        public static void DrawInfoRow(string label, string value, bool localize = true)
        {
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField(
                    localize ? MNNLocalization.Get(label) : label,
                    GUILayout.Width(120)
                );
                EditorGUILayout.LabelField(value, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Button Components

        /// <summary>
        /// 绘制主要按钮
        /// </summary>
        public static bool DrawPrimaryButton(string label, int height = 30, bool localize = true)
        {
            var localizedLabel = localize ? MNNLocalization.Get(label) : label;
            var style = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };

            return GUILayout.Button(localizedLabel, style, GUILayout.Height(height));
        }

        /// <summary>
        /// 绘制次要按钮
        /// </summary>
        public static bool DrawSecondaryButton(string label, int height = 30, bool localize = true)
        {
            var localizedLabel = localize ? MNNLocalization.Get(label) : label;
            return GUILayout.Button(localizedLabel, GUILayout.Height(height));
        }

        /// <summary>
        /// 绘制图标按钮
        /// </summary>
        public static bool DrawIconButton(GUIContent icon, int size = 30)
        {
            return GUILayout.Button(icon, GUILayout.Width(size), GUILayout.Height(size));
        }

        #endregion

        #region Category List

        /// <summary>
        /// 绘制分类列表
        /// </summary>
        /// <typeparam name="T">枚举类型</typeparam>
        /// <param name="selected">当前选中</param>
        /// <param name="getCategoryName">获取分类显示名称</param>
        /// <param name="getCategoryCount">获取分类数量</param>
        /// <param name="onCategorySelected">选中回调</param>
        public static T DrawCategoryList<T>(
            T selected,
            Func<T, string> getCategoryName,
            Func<T, int> getCategoryCount,
            Action<T> onCategorySelected) where T : Enum
        {
            foreach (T category in Enum.GetValues(typeof(T)))
            {
                var count = getCategoryCount?.Invoke(category) ?? 0;
                var name = getCategoryName?.Invoke(category) ?? category.ToString();
                var label = $"{name} ({count})";
                var isSelected = EqualityComparer<T>.Default.Equals(selected, category);

                var style = isSelected
                    ? new GUIStyle(CategoryButtonStyle) { fontStyle = FontStyle.Bold }
                    : CategoryButtonStyle;

                if (GUILayout.Button(label, style))
                {
                    onCategorySelected?.Invoke(category);
                    selected = category;
                }
            }

            return selected;
        }

        #endregion

        #region Progress & Status

        /// <summary>
        /// 绘制进度条
        /// </summary>
        public static void DrawProgressBar(float progress, string label)
        {
            var rect = GUILayoutUtility.GetRect(200, 16);
            EditorGUI.ProgressBar(rect, progress, label);
        }

        /// <summary>
        /// 绘制状态栏
        /// </summary>
        public static void DrawStatusBar(string leftText, string rightText = null)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(20));
            {
                GUILayout.Label(leftText, EditorStyles.miniLabel);

                if (!string.IsNullOrEmpty(rightText))
                {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(rightText, EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制下载状态栏
        /// </summary>
        public static void DrawDownloadStatusBar(bool isDownloading, float progress, string status)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(20));
            {
                if (isDownloading)
                {
                    GUILayout.Label(status, EditorStyles.miniLabel);
                    GUILayout.FlexibleSpace();

                    var rect = GUILayoutUtility.GetRect(200, 16);
                    EditorGUI.ProgressBar(rect, progress, $"{(progress * 100):F1}%");
                }
                else
                {
                    GUILayout.Label(status, EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Empty State

        /// <summary>
        /// 绘制空状态提示
        /// </summary>
        public static void DrawEmptyState(string message, string iconName = null)
        {
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical();
            {
                GUILayout.FlexibleSpace();

                // 图标
                if (!string.IsNullOrEmpty(iconName))
                {
                    var icon = EditorGUIUtility.IconContent(iconName);
                    GUILayout.Label(icon, GUILayout.Height(64));
                }

                // 消息
                GUILayout.Label(MNNLocalization.Get(message), EditorStyles.centeredGreyMiniLabel);

                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();
        }

        #endregion

        #region Dialog Helpers

        /// <summary>
        /// 显示确认对话框
        /// </summary>
        public static bool ShowConfirmDialog(string title, string message, string ok = "common.ok", string cancel = "common.cancel")
        {
            return EditorUtility.DisplayDialog(
                MNNLocalization.Get(title),
                MNNLocalization.Get(message),
                MNNLocalization.Get(ok),
                MNNLocalization.Get(cancel)
            );
        }

        /// <summary>
        /// 显示信息对话框
        /// </summary>
        public static void ShowInfoDialog(string title, string message, string ok = "common.ok")
        {
            EditorUtility.DisplayDialog(
                MNNLocalization.Get(title),
                MNNLocalization.Get(message),
                MNNLocalization.Get(ok)
            );
        }

        /// <summary>
        /// 显示错误对话框
        /// </summary>
        public static void ShowErrorDialog(string message)
        {
            EditorUtility.DisplayDialog(
                MNNLocalization.Get("common.error"),
                message,
                MNNLocalization.Get("common.ok")
            );
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 获取纯色纹理
        /// </summary>
        public static Texture2D GetColorTexture(Color color)
        {
            var key = $"color_{color.r}_{color.g}_{color.b}_{color.a}";

            if (_textureCache.TryGetValue(key, out var texture))
                return texture;

            texture = new Texture2D(2, 2);
            var pixels = new Color[4];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;

            texture.SetPixels(pixels);
            texture.Apply();

            _textureCache[key] = texture;
            return texture;
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        public static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }

        /// <summary>
        /// 获取Unity内置图标
        /// </summary>
        public static GUIContent GetIcon(string iconName)
        {
            return EditorGUIUtility.IconContent(iconName);
        }

        /// <summary>
        /// 绘制水平布局
        /// </summary>
        public static void BeginHorizontal(Action content, GUIStyle style = null)
        {
            if (style != null)
                EditorGUILayout.BeginHorizontal(style);
            else
                EditorGUILayout.BeginHorizontal();

            content?.Invoke();
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制垂直布局
        /// </summary>
        public static void BeginVertical(Action content, GUIStyle style = null)
        {
            if (style != null)
                EditorGUILayout.BeginVertical(style);
            else
                EditorGUILayout.BeginVertical();

            content?.Invoke();
            EditorGUILayout.EndVertical();
        }

        #endregion
    }
}
