using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor.UI
{
    /// <summary>Shared desktop layout, semantic colors, typography and controls. Dimensions
    /// are GUI points; Unity applies the display scale. Cached styles follow the editor theme.</summary>
    internal static class MNNStudioUI
    {
        internal const float ControlHeight = 28, PageInset = 24, MaximumContentWidth = 760;
        private static bool _dark;
        private static readonly List<Texture2D> Textures = new List<Texture2D>();
        private static GUIStyle _card, _user, _body, _userBody, _userCaption, _caption, _title, _heading, _input;
        private static GUIStyle _code, _markdownHeading;
        private static Font _codeFont;
        private static GUIStyle _button, _primary, _quiet, _icon, _segment, _selectedSegment, _chip, _placeholder, _navigation, _selectedNavigation, _model;
        internal static Color Background => EditorGUIUtility.isProSkin ? new Color(.13f, .13f, .13f) : Color.white;
        internal static Color Sidebar => EditorGUIUtility.isProSkin ? new Color(.095f, .095f, .095f) : new Color(.965f, .965f, .965f);
        internal static Color Surface => SurfaceColor(EditorGUIUtility.isProSkin);
        internal static Color SurfaceColor(bool dark) => dark ? new Color(.19f, .19f, .19f) : new Color(.955f, .955f, .955f);
        internal static Color Text => TextColor(EditorGUIUtility.isProSkin);
        internal static Color TextColor(bool dark) => dark ? new Color(.94f, .95f, .97f) : new Color(.12f, .14f, .17f);
        internal static Color SecondaryText => SecondaryColor(EditorGUIUtility.isProSkin);
        internal static Color SecondaryColor(bool dark) => dark ? new Color(.72f, .74f, .78f) : new Color(.38f, .40f, .44f);
        internal static Color Accent => new Color(.0f, .36f, .76f);
        internal static Color ActionColor(bool dark) => dark ? new Color(.94f, .94f, .94f) : new Color(.12f, .12f, .12f);
        internal static Color ActionTextColor(bool dark) => dark ? new Color(.12f, .12f, .12f) : Color.white;
        internal static GUIStyle Card
        {
            get
            {
                Ensure();
                return _card;
            }
        }

        internal static GUIStyle User
        {
            get
            {
                Ensure();
                return _user;
            }
        }

        internal static GUIStyle Body
        {
            get
            {
                Ensure();
                return _body;
            }
        }

        internal static GUIStyle UserBody
        {
            get
            {
                Ensure();
                return _userBody;
            }
        }

        internal static GUIStyle UserCaption
        {
            get
            {
                Ensure();
                return _userCaption;
            }
        }

        internal static GUIStyle Caption
        {
            get
            {
                Ensure();
                return _caption;
            }
        }

        internal static GUIStyle Title
        {
            get
            {
                Ensure();
                return _title;
            }
        }

        internal static GUIStyle Heading
        {
            get
            {
                Ensure();
                return _heading;
            }
        }

        internal static GUIStyle Input
        {
            get
            {
                Ensure();
                return _input;
            }
        }

        internal static GUIStyle Placeholder
        {
            get
            {
                Ensure();
                return _placeholder;
            }
        }

        internal static GUIStyle Code
        {
            get
            {
                Ensure();
                return _code;
            }
        }

        internal static GUIStyle MarkdownHeading
        {
            get
            {
                Ensure();
                return _markdownHeading;
            }
        }

        internal static float ContentWidth(float windowWidth) => Mathf.Max(160, Mathf.Min(MaximumContentWidth, windowWidth - 2 * PageInset - 16));
        static MNNStudioUI()
        {
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseResources;
        }

        private static void ReleaseResources()
        {
            ClearTextures();
            if (_codeFont != null)
                UnityEngine.Object.DestroyImmediate(_codeFont);
            _codeFont = null;
        }

        private static void ClearTextures()
        {
            foreach (var texture in Textures)
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            Textures.Clear();
        }

        private static void Ensure()
        {
            if (_card != null && _dark == EditorGUIUtility.isProSkin)
                return;
            _dark = EditorGUIUtility.isProSkin;
            ClearTextures();
            _card = Container(Surface, 16);
            _user = Container(Surface, 14);
            _body = new GUIStyle(EditorStyles.wordWrappedLabel)
            {fontSize = 13, richText = false, padding = new RectOffset(), margin = new RectOffset(), normal = {textColor = Text}};
            _userBody = new GUIStyle(_body);
            _userCaption = new GUIStyle(_userBody)
            {fontSize = 11, normal = {textColor = SecondaryText}};
            _caption = new GUIStyle(_body)
            {fontSize = 11, normal = {textColor = SecondaryText}};
            _title = new GUIStyle(_body)
            {fontSize = 26, fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter};
            _heading = new GUIStyle(_body)
            {fontSize = 14, fontStyle = FontStyle.Bold};
            if (_codeFont == null)
            {
                _codeFont = Font.CreateDynamicFontFromOSFont(new[]{"Menlo", "Consolas", "DejaVu Sans Mono"}, 12);
                if (_codeFont != null)
                    _codeFont.hideFlags = HideFlags.HideAndDontSave;
            }

            _code = new GUIStyle(_body)
            {font = _codeFont ?? _body.font, fontSize = 12};
            _markdownHeading = new GUIStyle(_body)
            {fontSize = 18, fontStyle = FontStyle.Bold};
            _input = new GUIStyle{font = EditorStyles.textArea.font, fontSize = 13, wordWrap = true, padding = new RectOffset(2, 2, 4, 4), margin = new RectOffset(), border = new RectOffset(), normal = {background = null, textColor = Text}, focused = {background = null, textColor = Text}, hover = {background = null, textColor = Text}, active = {background = null, textColor = Text}};
            _placeholder = new GUIStyle(_input)
            {normal = {textColor = SecondaryText}};
            Color hover = _dark ? new Color(.25f, .25f, .25f) : new Color(.89f, .89f, .89f);
            _button = ButtonStyle(Surface, hover, Text);
            _primary = ButtonStyle(ActionColor(_dark), _dark ? Color.white : new Color(.28f, .28f, .28f), ActionTextColor(_dark));
            _quiet = new GUIStyle(_button)
            {normal = {background = null, textColor = SecondaryText}};
            _icon = new GUIStyle(_quiet)
            {padding = new RectOffset()};
            _navigation = new GUIStyle(_quiet)
            {alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 0, 0), clipping = TextClipping.Clip, normal = {textColor = Text}};
            _selectedNavigation = new GUIStyle(_button)
            {alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 0, 0), clipping = TextClipping.Clip};
            _model = new GUIStyle(_navigation)
            {fontSize = 14, fontStyle = FontStyle.Bold};
            _segment = new GUIStyle(_quiet)
            {normal = {textColor = SecondaryText}, padding = new RectOffset(12, 12, 0, 0)};
            _selectedSegment = ButtonStyle(_dark ? new Color(.3f, .32f, .35f) : Color.white, Surface, Text);
            _selectedSegment.fontStyle = FontStyle.Bold;
            _selectedSegment.onNormal.background = _selectedSegment.normal.background;
            _selectedSegment.onNormal.textColor = Text;
            _selectedSegment.onHover.background = _selectedSegment.hover.background;
            _selectedSegment.onHover.textColor = Text;
            _selectedSegment.onActive.background = _selectedSegment.active.background;
            _selectedSegment.onActive.textColor = Text;
            _selectedSegment.onFocused.background = _selectedSegment.focused.background;
            _selectedSegment.onFocused.textColor = Text;
            foreach (var state in new[]{_input.onNormal, _input.onHover, _input.onActive, _input.onFocused})
                state.background = null;
            foreach (var style in new[]{_input, _placeholder, _button, _primary, _quiet, _icon, _segment, _selectedSegment, _navigation, _selectedNavigation, _model})
                foreach (var state in new[]{style.normal, style.hover, style.active, style.focused, style.onNormal, style.onHover, style.onActive, style.onFocused})
                    state.scaledBackgrounds = Array.Empty<Texture2D>();
            _chip = Container(_dark ? new Color(.24f, .25f, .28f) : new Color(.92f, .94f, .97f), 8);
        }

        private static GUIStyle Container(Color color, int padding) => new GUIStyle{normal = {background = Rounded(color)}, border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(padding, padding, padding, padding), margin = new RectOffset()};
        private static GUIStyle ButtonStyle(Color normal, Color hover, Color text) => new GUIStyle{normal = {background = Rounded(normal), textColor = text}, hover = {background = Rounded(hover), textColor = text}, active = {background = Rounded(hover), textColor = text}, focused = {background = Rounded(hover), textColor = text}, border = new RectOffset(10, 10, 10, 10), padding = new RectOffset(10, 10, 0, 0), margin = new RectOffset(), font = EditorStyles.label.font, fontSize = 12, alignment = TextAnchor.MiddleCenter, fixedHeight = ControlHeight};
        private static Texture2D Rounded(Color color)
        {
            const int size = 24, radius = 10;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {hideFlags = HideFlags.HideAndDontSave};
            var pixels = new Color[size * size];
            for (int y = 0; y < size; ++y)
                for (int x = 0; x < size; ++x)
                {
                    float dx = Mathf.Max(0, Mathf.Max(radius - x - .5f, x + .5f - (size - radius)));
                    float dy = Mathf.Max(0, Mathf.Max(radius - y - .5f, y + .5f - (size - radius)));
                    var pixel = color;
                    pixel.a *= Mathf.Clamp01(radius + .5f - Mathf.Sqrt(dx * dx + dy * dy));
                    pixels[y * size + x] = pixel;
                }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Textures.Add(texture);
            return texture;
        }

        internal static bool Button(string label, float width, bool primary = false, bool quiet = false, string tooltip = null)
        {
            Ensure();
            return GUILayout.Button(new GUIContent(label, tooltip), primary ? _primary : quiet ? (width <= ControlHeight ? _icon : _quiet) : _button, GUILayout.Width(width), GUILayout.Height(ControlHeight));
        }

        internal static bool ButtonAt(Rect rect, string label, bool primary = false, bool quiet = false, string tooltip = null)
        {
            Ensure();
            return GUI.Button(rect, new GUIContent(label, tooltip), primary ? _primary : quiet ? (rect.width <= ControlHeight ? _icon : _quiet) : _button);
        }

        internal static bool IconButton(string icon, string tooltip, float size = ControlHeight)
        {
            Ensure();
            return GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent(icon).image, tooltip), _icon, GUILayout.Width(size), GUILayout.Height(size));
        }

        internal static bool IconButtonAt(Rect rect, string icon, string tooltip)
        {
            Ensure();
            return GUI.Button(rect, new GUIContent(EditorGUIUtility.IconContent(icon).image, tooltip), _icon);
        }

        internal static bool Navigation(string label, bool selected, float width, string tooltip = null, Action<Rect> beforeButton = null)
        {
            Ensure();
            var rect = GUILayoutUtility.GetRect(width, 34, GUILayout.ExpandWidth(false));
            beforeButton?.Invoke(rect);
            return GUI.Button(rect, new GUIContent(label, tooltip), selected ? _selectedNavigation : _navigation);
        }

        internal static bool ModelButton(Rect rect, string label, string tooltip)
        {
            Ensure();
            return GUI.Button(rect, new GUIContent(label, tooltip), _model);
        }

        internal static bool SendButton(bool enabled)
        {
            Ensure();
            using (new EditorGUI.DisabledScope(!enabled))
            {
                var rect = GUILayoutUtility.GetRect(32, 32, GUILayout.ExpandWidth(false));
                bool clicked = GUI.Button(rect, new GUIContent("", "Send message (Enter)"), _primary);
                if (Event.current.type == EventType.Repaint)
                {
                    Color color = GUI.enabled ? ActionTextColor(_dark) : SecondaryText;
                    float x = rect.center.x, y = rect.center.y;
                    EditorGUI.DrawRect(new Rect(x - 1, y - 6, 2, 13), color);
                    for (int i = 0; i < 6; ++i)
                    {
                        EditorGUI.DrawRect(new Rect(x - i - 1, y - 6 + i, 2, 2), color);
                        EditorGUI.DrawRect(new Rect(x + i - 1, y - 6 + i, 2, 2), color);
                    }
                }

                return clicked;
            }
        }

        internal static int Segments(int selected, string[] labels)
        {
            Ensure();
            using (new EditorGUILayout.HorizontalScope(_chip, GUILayout.ExpandWidth(false)))
                for (int i = 0; i < labels.Length; ++i)
                {
                    if (i > 0)
                        GUILayout.Space(4);
                    if (GUILayout.Button(labels[i], i == selected ? _selectedSegment : _segment, GUILayout.Width(i == 0 ? 112 : 100)))
                        selected = i;
                }

            return selected;
        }

        internal static bool Toggle(string label, bool value, float width, string tooltip = null)
        {
            Ensure();
            return GUILayout.Toggle(value, new GUIContent(label, tooltip), value ? _selectedSegment : _quiet, GUILayout.Width(width), GUILayout.Height(ControlHeight));
        }

        internal static bool Attachment(string kind, string path, Texture2D preview, float width)
        {
            Ensure();
            using (new EditorGUILayout.HorizontalScope(_chip, GUILayout.Width(width), GUILayout.Height(48)))
            {
                var rect = GUILayoutUtility.GetRect(32, 32, GUILayout.ExpandWidth(false));
                if (preview != null)
                    GUI.DrawTexture(rect, preview, ScaleMode.ScaleToFit);
                else
                    GUI.Label(rect, kind == "Image" ? "IMG" : "WAV", Caption);
                GUILayout.Space(8);
                using (new EditorGUILayout.VerticalScope(GUILayout.MinWidth(0)))
                {
                    GUILayout.Label(kind, Caption);
                    var filename = new GUIStyle(Body)
                    {wordWrap = false, clipping = TextClipping.Clip};
                    GUILayout.Label(new GUIContent(System.IO.Path.GetFileName(path), path), filename, GUILayout.MinWidth(0));
                }

                return Button("×", 28, quiet: true, tooltip: "Remove " + kind.ToLowerInvariant() + " attachment");
            }
        }

        internal static void EmptyState(string title, string detail)
        {
            GUILayout.Space(32);
            GUILayout.Label(title, Title, GUILayout.Height(36));
            GUILayout.Space(8);
            var style = new GUIStyle(Caption)
            {alignment = TextAnchor.MiddleCenter};
            GUILayout.Label(detail, style, GUILayout.Height(44));
            GUILayout.Space(20);
        }

        internal static void Separator()
        {
            var rect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, _dark ? new Color(1, 1, 1, .07f) : new Color(0, 0, 0, .08f));
        }

        internal static void ResultMetric(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, Caption, GUILayout.Width(140));
                GUILayout.Label(value, Heading, GUILayout.MinWidth(0));
            }

            GUILayout.Space(8);
        }
    }

    /// <summary>Reusable anchored settings surface; native popup dismissal and Escape behavior.</summary>
    internal sealed class MNNStudioPopover : PopupWindowContent
    {
        private readonly Action _draw;
        private readonly Vector2 _size;
        internal MNNStudioPopover(Vector2 size, Action draw)
        {
            _size = size;
            _draw = draw;
        }

        public override Vector2 GetWindowSize() => _size;
        public override void OnGUI(Rect rect)
        {
            EditorGUI.DrawRect(rect, MNNStudioUI.Background);
            using (new EditorGUILayout.VerticalScope(MNNStudioUI.Card))
                _draw();
        }
    }
}
