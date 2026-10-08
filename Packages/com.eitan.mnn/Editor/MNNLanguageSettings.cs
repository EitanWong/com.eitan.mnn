using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>
    /// MNN语言设置窗口
    /// </summary>
    public class MNNLanguageSettings : EditorWindow
    {
        private SystemLanguage _selectedLanguage;

        [MenuItem("Window/MNN/Language Settings")]
        public static void ShowWindow()
        {
            var window = GetWindow<MNNLanguageSettings>("MNN Language");
            window.minSize = new Vector2(300, 150);
            window.Show();
        }

        private void OnEnable()
        {
            _selectedLanguage = MNNLocalization.CurrentLanguage;
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("MNN Editor Language Settings", EditorStyles.boldLabel);
            GUILayout.Space(10);

            EditorGUILayout.HelpBox(
                "Select the language for MNN Editor tools. " +
                "This setting will be saved and persist across Unity sessions.",
                MessageType.Info
            );

            GUILayout.Space(10);

            EditorGUI.BeginChangeCheck();
            _selectedLanguage = (SystemLanguage)EditorGUILayout.EnumPopup("Language:", _selectedLanguage);

            if (EditorGUI.EndChangeCheck())
            {
                // 保存到EditorPrefs
                EditorPrefs.SetString("MNN.EditorLanguage", _selectedLanguage.ToString());

                // 强制刷新本地化系统
                MNNLocalization.RefreshLanguage();

                EditorUtility.DisplayDialog(
                    "Language Changed",
                    $"Language has been changed to {_selectedLanguage}.\n" +
                    "Please reopen MNN windows to see the changes.",
                    "OK"
                );
            }

            GUILayout.Space(10);
            GUILayout.Label($"Current Language: {MNNLocalization.CurrentLanguage}", EditorStyles.miniLabel);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Reset to System Language", GUILayout.Height(30)))
            {
                EditorPrefs.DeleteKey("MNN.EditorLanguage");
                MNNLocalization.RefreshLanguage();
                _selectedLanguage = MNNLocalization.CurrentLanguage;

                EditorUtility.DisplayDialog(
                    "Language Reset",
                    "Language has been reset to system default.",
                    "OK"
                );
            }
        }
    }
}
