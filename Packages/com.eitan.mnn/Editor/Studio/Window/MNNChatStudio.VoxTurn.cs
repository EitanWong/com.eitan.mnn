using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private int _voxTurnLanguage, _voxTurnSpeaker;
        private int _voxTurnQuestions = 1;
        private static readonly string[] VoxTurnQuestionLabels =
        {
            "End of turn", "Completion state (four options)", "Completion state (three options)",
            "Interruption", "Invalid input", "Resume timing"
        };

        private void DrawVoxTurnSettings()
        {
            _voxTurnLanguage = EditorGUILayout.Popup("Question language", _voxTurnLanguage, new[] { "English", "Chinese" });
            _voxTurnSpeaker = EditorGUILayout.Popup("Current speaker", _voxTurnSpeaker, new[] { "User", "Agent" });
            _voxTurnQuestions = EditorGUILayout.MaskField("Decisions", _voxTurnQuestions, VoxTurnQuestionLabels);
            EditorGUILayout.HelpBox("Enter the current transcript, attach a WAV, or supply both. VoxTurn evaluates the last eight seconds. The transcript is optional when audio is attached.", MessageType.Info);
        }

        private string[] BuildVoxTurnQuestions()
        {
            return MNNVoxTurn.SupportedTasks.Where((task, index) => (_voxTurnQuestions & (1 << index)) != 0).ToArray();
        }

        private string VoxTurnValidation()
        {
            if (BuildVoxTurnQuestions().Length == 0) return "Choose at least one turn decision.";
            if (!string.IsNullOrWhiteSpace(_audioPath) && !File.Exists(_audioPath)) return "Choose an existing WAV or clear the audio attachment.";
            if (string.IsNullOrWhiteSpace(_query) && !File.Exists(_audioPath)) return "Enter a transcript or choose audio.";
            return null;
        }
    }
}
