using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private void DrawRetrieval()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_toolScroll))
            {
                _toolScroll = scroll.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(28);
                    GUILayout.Label(_taskIndex == 1 ? "Compare semantic meaning" : "Find the most relevant answer", MNNStudioUI.Heading);
                    GUILayout.Space(8);
                    GUILayout.Label(_taskIndex == 1 ? "Measure how closely a document matches your query." : "Evaluate a document against your query.", MNNStudioUI.Caption);
                    GUILayout.Space(24);
                    using (new EditorGUILayout.VerticalScope(MNNStudioUI.Card))
                    {
                        GUILayout.Label("Query", MNNStudioUI.Heading);
                        GUILayout.Space(8);
                        _query = EditorGUILayout.TextArea(_query, MNNStudioUI.Input, GUILayout.Height(64));
                        GUILayout.Space(12);
                        MNNStudioUI.Separator();
                        GUILayout.Space(12);
                        GUILayout.Label("Document", MNNStudioUI.Heading);
                        GUILayout.Space(8);
                        _document = EditorGUILayout.TextArea(_document, MNNStudioUI.Input, GUILayout.Height(108));
                    }

                    GUILayout.Space(16);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (new EditorGUI.DisabledScope(Busy || string.IsNullOrWhiteSpace(_query) || string.IsNullOrWhiteSpace(_document)))
                            if (MNNStudioUI.Button(Busy ? "Computing…" : _taskIndex == 1 ? "Compare" : "Score relevance", 144, primary: true))
                                RunRetrieval();
                        GUILayout.FlexibleSpace();
                    }

                    if (_taskIndex == 1)
                    {
                        GUILayout.Space(12);
                        GUILayout.Label("Tip: use the model's recommended Instruct / Query prefix for retrieval queries.", MNNStudioUI.Caption);
                    }

                    if (_retrieval != null)
                    {
                        GUILayout.Space(24);
                        using (new EditorGUILayout.VerticalScope(MNNStudioUI.Card))
                        {
                            GUILayout.Label("Result", MNNStudioUI.Heading);
                            GUILayout.Space(16);
                            MNNStudioUI.ResultMetric(_taskIndex == 1 ? "Cosine similarity" : "Relevance", _retrieval.Score?.ToString("0.0000"));
                            MNNStudioUI.ResultMetric("Inference time", _retrieval.Seconds.ToString("0.00") + " s");
                            if (_retrieval.Vector != null)
                            {
                                MNNStudioUI.ResultMetric("Dimensions", _retrieval.Vector.Length.ToString());
                                if (MNNStudioUI.Button("Copy vector as JSON", 170))
                                    EditorGUIUtility.systemCopyBuffer = "[" + string.Join(",", _retrieval.Vector.Select(value => value.ToString("R", System.Globalization.CultureInfo.InvariantCulture))) + "]";
                            }
                        }
                    }

                    GUILayout.Space(24);
                }
            }
        }

        private void RunRetrieval()
        {
            Try(() =>
            {
                EnsureSession();
                _error = null;
                _retrieval = null;
                _session.Send(new MNNStudioRequest{Prompt = _query, Document = _document});
            });
        }
    }
}
