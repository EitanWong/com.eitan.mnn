using System;
using System.Collections.Generic;
using System.Text;
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
            internal string Text, Language;
            internal int Heading;
            internal bool Code;
        }

        internal readonly List<Block> Blocks;
        internal MNNStudioMarkdown(string text)
        {
            Blocks = Parse(text);
        }

        internal static List<Block> Parse(string text)
        {
            var blocks = new List<Block>();
            var paragraph = new StringBuilder();
            bool code = false;
            string language = null, fence = null;
            Action flush = () =>
            {
                if (paragraph.Length == 0)
                    return;
                blocks.Add(new Block{Text = paragraph.ToString().TrimEnd('\n'), Code = code, Language = language});
                paragraph.Clear();
            };
            foreach (string line in (text ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                string trimmed = line.TrimStart();
                string marker = trimmed.StartsWith("```") ? "```" : trimmed.StartsWith("~~~") ? "~~~" : null;
                if ((!code && marker != null) || (code && marker == fence && trimmed.Substring(3).Trim().Length == 0))
                {
                    flush();
                    code = !code;
                    if (code)
                    {
                        fence = marker;
                        language = trimmed.Substring(3).Trim();
                    }
                    else
                    {
                        language = fence = null;
                    }

                    continue;
                }

                int heading = 0;
                if (!code)
                    while (heading < trimmed.Length && heading < 6 && trimmed[heading] == '#')
                        ++heading;
                if (!code && heading > 0 && trimmed.Length > heading && trimmed[heading] == ' ')
                {
                    flush();
                    blocks.Add(new Block{Text = trimmed.Substring(heading + 1), Heading = heading});
                    continue;
                }

                if (!code && line.Length == 0)
                {
                    flush();
                    continue;
                }

                paragraph.Append(line).Append('\n');
            }

            flush();
            return blocks;
        }

        internal void Draw(float width)
        {
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
                        EditorGUILayout.SelectableLabel(block.Text, MNNStudioUI.Code, GUILayout.Height(Mathf.Max(20, MNNStudioUI.Code.CalcHeight(new GUIContent(block.Text), width - 32) + 2)));
                    }
                }
                else
                {
                    var style = block.Heading > 0 ? MNNStudioUI.MarkdownHeading : MNNStudioUI.Body;
                    EditorGUILayout.SelectableLabel(block.Text, style, GUILayout.Height(Mathf.Max(20, style.CalcHeight(new GUIContent(block.Text), width) + 2)));
                }

                GUILayout.Space(block.Heading > 0 ? 8 : 12);
            }
        }
    }
}
