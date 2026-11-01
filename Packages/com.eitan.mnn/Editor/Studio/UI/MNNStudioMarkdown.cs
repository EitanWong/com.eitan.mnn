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
        private readonly MNNMarkdownDocument _document;
        private MNNMarkdownTheme _theme;

        internal MNNStudioMarkdown(string text)
        {
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
            foreach (var block in Blocks)
            {
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
                            GUILayout.Height(Mathf.Max(20, MNNStudioUI.MarkdownCode.CalcHeight(new GUIContent(block.RichText), width - 32) + 2)));
                    }
                }
                else
                {
                    var richStyle = block.Heading > 0 ? MNNStudioUI.MarkdownHeading : MNNStudioUI.MarkdownBody;
                    EditorGUILayout.SelectableLabel(block.Text, richStyle,
                        GUILayout.Height(Mathf.Max(20, richStyle.CalcHeight(new GUIContent(block.Text), width) + 2)));
                }

                GUILayout.Space(block.Heading > 0 ? 8 : 12);
            }
        }
    }
}
