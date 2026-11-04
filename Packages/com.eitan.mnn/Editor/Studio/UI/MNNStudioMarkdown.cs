using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor.UI
{
    /// <summary>Small, literal Markdown renderer for model output. Supports fenced code and
    /// headings without executing links, rich-text tags or generated editor commands.</summary>
    internal sealed class MNNStudioMarkdown
    {
        internal sealed class Block
        {
            internal string Text, Language, RichText;
            internal int Heading;
            internal bool Code;
        }

        internal readonly List<Block> Blocks;
        internal readonly string Source;
        private readonly MNNMarkdownDocument _document;
        private MNNMarkdownTheme _theme;
        private float _measuredWidth = float.NaN;
        private float _measuredScale;
        private GUILayoutOption[][] _heightOptions;

        internal MNNStudioMarkdown(string text)
        {
            Source = text;
            _document = MNNMarkdownDocument.Parse(text);
            _theme = EditorGUIUtility.isProSkin ? MNNMarkdownTheme.Dark : MNNMarkdownTheme.Light;
            Blocks = RenderBlocks(_document, _theme);
        }

        internal static List<Block> Parse(string text) => RenderBlocks(MNNMarkdownDocument.Parse(text),
            EditorGUIUtility.isProSkin ? MNNMarkdownTheme.Dark : MNNMarkdownTheme.Light);

        internal void SetTheme(MNNMarkdownTheme theme)
        {
            if (_theme == theme) return;
            List<Block> blocks = RenderBlocks(_document, theme);
            Blocks.Clear();
            Blocks.AddRange(blocks);
            _theme = theme;
            _measuredWidth = float.NaN;
        }

        private static List<Block> RenderBlocks(MNNMarkdownDocument document, MNNMarkdownTheme theme)
        {
            var blocks = new List<Block>();
            foreach (var source in document.Blocks)
            {
                if (source.Type == MNNMarkdownBlockType.Code)
                {
                    blocks.Add(new Block { Text = source.Text, Code = true, Language = source.Language,
                        RichText = MNNMarkdownRenderer.ToCodeRichText(source.Text, source.Language, theme) });
                    continue;
                }
                if (source.Type == MNNMarkdownBlockType.Heading)
                {
                    blocks.Add(new Block { Text = MNNMarkdownRenderer.ToRichText(source, theme), Heading = source.HeadingLevel });
                    continue;
                }
                blocks.Add(new Block { Text = MNNMarkdownRenderer.ToRichText(source, theme) });
            }
            return blocks;
        }

        internal void Draw(float width)
        {
            SetTheme(EditorGUIUtility.isProSkin ? MNNMarkdownTheme.Dark : MNNMarkdownTheme.Light);
            MeasureHeights(width);
            for (int index = 0; index < Blocks.Count; ++index)
            {
                var block = Blocks[index];
                if (block.Code)
                {
                    using (new EditorGUILayout.VerticalScope(MNNStudioUI.Card, GUILayout.Width(width)))
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            GUILayout.Label(string.IsNullOrEmpty(block.Language) ? "Code" : block.Language, MNNStudioUI.Caption);
                            GUILayout.FlexibleSpace();
                            if (MNNStudioUI.Button("Copy code", 82, quiet: true))
                                EditorGUIUtility.systemCopyBuffer = block.Text;
                        }

                        GUILayout.Space(8);
                        EditorGUILayout.SelectableLabel(block.RichText, MNNStudioUI.MarkdownCode,
                            _heightOptions[index]);
                    }
                }
                else
                {
                    var richStyle = block.Heading > 0 ? MNNStudioUI.MarkdownHeading : MNNStudioUI.MarkdownBody;
                    EditorGUILayout.SelectableLabel(block.Text, richStyle,
                        _heightOptions[index]);
                }

                GUILayout.Space(block.Heading > 0 ? 8 : 12);
            }
        }

        private void MeasureHeights(float width)
        {
            float scale = EditorGUIUtility.pixelsPerPoint;
            if (_measuredWidth == width && _measuredScale == scale)
                return;
            _heightOptions = new GUILayoutOption[Blocks.Count][];
            var content = new GUIContent();
            for (int index = 0; index < Blocks.Count; ++index)
            {
                var block = Blocks[index];
                var style = block.Code ? MNNStudioUI.MarkdownCode : block.Heading > 0 ? MNNStudioUI.MarkdownHeading : MNNStudioUI.MarkdownBody;
                content.text = block.Code ? block.RichText : block.Text;
                float height = Mathf.Max(20, style.CalcHeight(content, block.Code ? width - 32 : width) + 2);
                _heightOptions[index] = new[] { GUILayout.Height(height) };
            }
            _measuredWidth = width;
            _measuredScale = scale;
        }
    }
}
