using UnityEngine;
using MNN.Unity;

namespace MNN.Examples
{
    /// <summary>
    /// MNN 基础示例：检测 MNN 库是否正确加载
    /// </summary>
    public class MNNBasicExample : MonoBehaviour
    {
        private void Start()
        {
            CheckMNNStatus();
        }

        private void CheckMNNStatus()
        {
            Debug.Log("=== MNN Basic Example ===");
            // 检查 MNN 是否加载
            bool isLoaded = MNNVersion.IsLoaded();
            if (isLoaded)
            {
                Debug.Log("✓ MNN loaded successfully!");
                MNNVersion.LogInfo();
            }
            else
            {
                Debug.LogError("✗ MNN failed to load!");
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 400, 200));
            GUILayout.Label("MNN for Unity - Basic Example", new GUIStyle(GUI.skin.label)
            {fontSize = 20, fontStyle = FontStyle.Bold});
            if (MNNVersion.IsLoaded())
            {
                GUILayout.Label($"Status: ✓ Loaded", new GUIStyle(GUI.skin.label)
                {normal = {textColor = Color.green}});
                GUILayout.Label($"Version: {MNNVersion.GetVersion()}");
                GUILayout.Label($"Platform: {Application.platform}");
            }
            else
            {
                GUILayout.Label("Status: ✗ Failed", new GUIStyle(GUI.skin.label)
                {normal = {textColor = Color.red}});
            }

            GUILayout.EndArea();
        }
    }
}
