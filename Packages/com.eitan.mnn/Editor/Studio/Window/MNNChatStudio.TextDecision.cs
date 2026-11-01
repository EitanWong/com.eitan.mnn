using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MNN.Unity.Editor.UI;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        [SerializeField] private MNNDecisionKind _decisionKind;
        [SerializeField] private string _decisionInstructions = "Which department should handle this?";
        [SerializeField] private string _decisionOptions = "billing | invoices, payments, refunds\ntechnical | bugs, outages\nother | everything else";

        private MNNDecisionQuestion BuildDecisionQuestion() => ParseDecisionQuestion(_decisionKind, _decisionInstructions, _decisionOptions);

        internal static MNNDecisionQuestion ParseDecisionQuestion(MNNDecisionKind kind, string instructions, string text)
        {
            string[] lines = (text ?? "").Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            if (kind == MNNDecisionKind.Score) return MNNDecisionQuestion.Score(instructions, lines);
            if (kind == MNNDecisionKind.Boolean)
            {
                if (lines.Length != 0 && lines.Length != 2) throw new ArgumentException("Boolean criteria need two lines: false, then true. Leave empty to use defaults.");
                return MNNDecisionQuestion.Boolean(instructions, lines.ElementAtOrDefault(0), lines.ElementAtOrDefault(1));
            }
            if (kind != MNNDecisionKind.Choice) throw new ArgumentOutOfRangeException(nameof(kind));
            return MNNDecisionQuestion.Choice(instructions, lines.Select(line =>
            {
                int separator = line.IndexOf('|');
                return separator < 0 ? new MNNDecisionOption(line) : new MNNDecisionOption(line.Substring(0, separator).Trim(), line.Substring(separator + 1).Trim());
            }).ToArray());
        }

        private void DrawTextDecisionTool()
        {
            using (var scroll = new EditorGUILayout.ScrollViewScope(_toolScroll))
            {
                _toolScroll = scroll.scrollPosition;
                using (new CenteredContent(ContentWidth))
                {
                    GUILayout.Space(24);
                    GUILayout.Label("Structured text decisions", MNNStudioUI.Heading);
                    GUILayout.Label("Evaluate one English question against a situation. Choose a category, score ordered levels or estimate whether a statement holds.", MNNStudioUI.Body);
                    using (new EditorGUI.DisabledScope(Busy))
                    {
                        GUILayout.Label("Situation", MNNStudioUI.Heading);
                        _query = EditorGUILayout.TextArea(_query ?? "", MNNStudioUI.Input, GUILayout.Height(100));
                        if (MNNStudioUI.Button("Use example", 110, quiet: true))
                        {
                            GUI.FocusControl(null); GUIUtility.keyboardControl = 0; EditorGUIUtility.editingTextField = false;
                            _query = CurrentTask.Example;
                        }
                        var kind = (MNNDecisionKind)EditorGUILayout.EnumPopup("Question type", _decisionKind);
                        if (kind != _decisionKind)
                        {
                            _decisionKind = kind;
                            _decisionInstructions = kind == MNNDecisionKind.Choice ? "Which department should handle this?" : kind == MNNDecisionKind.Score ? "How urgent is this support request?" : "Is the customer asking for a refund?";
                            _decisionOptions = kind == MNNDecisionKind.Choice ? "billing | invoices, payments, refunds\ntechnical | bugs, outages\nother | everything else" : kind == MNNDecisionKind.Score ? "low priority, can wait\nnormal priority\nurgent, needs immediate action" : "";
                        }
                        GUILayout.Label("Question", MNNStudioUI.Heading);
                        _decisionInstructions = EditorGUILayout.TextArea(_decisionInstructions, MNNStudioUI.Input, GUILayout.Height(60));
                        GUILayout.Label(_decisionKind == MNNDecisionKind.Choice ? "One option per line: label | description" : _decisionKind == MNNDecisionKind.Score ? "One level description per line, lowest first (level 0)" : "Optional criteria: false on first line, true on second", MNNStudioUI.Caption);
                        _decisionOptions = EditorGUILayout.TextArea(_decisionOptions, MNNStudioUI.Input, GUILayout.Height(90));
                        string reason = GetTaskRunBlockedReason();
                        if (reason != null) EditorGUILayout.HelpBox(reason, MessageType.Info);
                        using (new EditorGUI.DisabledScope(reason != null))
                            if (MNNStudioUI.Button("Evaluate", 120, primary: true)) RunTask();
                    }
                    if (Busy && MNNStudioUI.Button("Stop", 68)) StopReply();
                    var result = _retrieval?.Decision;
                    if (result != null)
                    {
                        GUILayout.Space(16);
                        string answer = result.Kind == MNNDecisionKind.Choice ? result.Choice : result.Kind == MNNDecisionKind.Score ? "Expected level: " + result.ExpectedScore.Value.ToString("F3") : "True: " + result.TrueProbability.Value.ToString("P2");
                        GUILayout.Label(answer, MNNStudioUI.Heading);
                        GUILayout.Label(_retrieval.Seconds.ToString("F2") + " s · " + _retrieval.BackendDescription + " · " + result.InputTokens + " tokens", MNNStudioUI.Caption);
                        for (int i = 0; i < result.Labels.Count; ++i) GUILayout.Label(result.Labels[i] + "   " + result.Probabilities[i].ToString("P3"), MNNStudioUI.Body);
                        GUILayout.Label("Confidence: " + result.Confidence.ToString("P2") + " · upstream act probability: " + result.ActProbability.ToString("P2"), MNNStudioUI.Caption);
                        if (result.StateTruncated || result.QuestionTruncated) EditorGUILayout.HelpBox("Input was truncated to the model token budget. Shorten the situation or question to retain more context.", MessageType.Warning);
                        if (MNNStudioUI.Button("Copy probabilities", 150)) EditorGUIUtility.systemCopyBuffer = _retrieval.Text ?? "";
                    }
                    GUILayout.Space(12);
                    GUILayout.Label("Probabilities depend on the checkpoint and question wording. Validate thresholds on your own examples before using them to trigger actions.", MNNStudioUI.Caption);
                    GUILayout.Space(24);
                }
            }
        }
    }
}
