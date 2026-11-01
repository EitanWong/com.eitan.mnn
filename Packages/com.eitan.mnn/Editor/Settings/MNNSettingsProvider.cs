using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor
{
    internal sealed class MNNSettingsProvider : SettingsProvider
    {
        private SerializedObject _settings;
        private string[] _files = Array.Empty<string>();
        private readonly Dictionary<string, long> _sizes = new Dictionary<string, long>();
        private string _error;
        private Vector2 _scroll;
        private Vector2 _modelsScroll;
        private bool _pathsExpanded;
        private bool _previewExpanded;
        private MNNModelBuildPlan _plan;
        private bool Chinese => MNNLocalization.CurrentLanguage == SystemLanguage.Chinese ||
            MNNLocalization.CurrentLanguage == SystemLanguage.ChineseTraditional;
        private string L(string en, string zh) => Chinese ? zh : en;

        private MNNSettingsProvider() : base("Project/MNN", SettingsScope.Project)
        {
            label = "MNN";
            keywords = new HashSet<string> { "MNN", "Models", "StreamingAssets", "Build", "Download", "Android", "iOS", "模型", "打包", "下载" };
        }

        [SettingsProvider]
        private static SettingsProvider CreateProvider() => new MNNSettingsProvider();

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            _settings = new SerializedObject(MNNProjectSettings.instance);
            Refresh();
            Undo.undoRedoPerformed += Refresh;
        }

        public override void OnDeactivate() => Undo.undoRedoPerformed -= Refresh;

        private void Refresh()
        {
            _sizes.Clear();
            try
            {
                var root = MNNModelBuildProcessor.ModelsRoot;
                _files = Directory.Exists(root) ? MNNModelBuildPlan.EnumerateFiles(root).ToArray() : Array.Empty<string>();
                foreach (var file in _files)
                    _sizes[file] = new FileInfo(Path.Combine(root, file)).Length;
                UpdatePlan();
            }
            catch (Exception error) { _error = error.Message; _plan = null; }
            Repaint();
        }

        private void UpdatePlan()
        {
            try
            {
                var settings = MNNProjectSettings.instance;
                if (settings.remoteManifest != null) MNNModelBuildPlan.ReadRemoteManifest(settings.remoteManifest.text);
                _plan = MNNModelBuildPlan.Create(MNNModelBuildProcessor.ModelsRoot, settings.packagingMode, settings.includedPaths);
                _error = null;
            }
            catch (Exception error) { _plan = null; _error = error.Message; }
        }

        private void PickPath(SerializedProperty selection, bool folder)
        {
            var root = Path.GetFullPath(MNNModelBuildProcessor.ModelsRoot);
            var picked = folder ? EditorUtility.OpenFolderPanel(L("Include model folder", "加入模型目录"), root, "") :
                EditorUtility.OpenFilePanel(L("Include model file", "加入模型文件"), root, "");
            if (string.IsNullOrEmpty(picked)) return;
            var prefix = root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(picked).StartsWith(prefix, StringComparison.Ordinal))
            {
                EditorUtility.DisplayDialog("MNN", L("Choose a file or subfolder inside StreamingAssets/MNN/Models.",
                    "请选择 StreamingAssets/MNN/Models 内的文件或子目录。"), "OK");
                return;
            }
            var relative = Path.GetFullPath(picked).Substring(prefix.Length).Replace('\\', '/');
            if (Enumerable.Range(0, selection.arraySize).Any(i => selection.GetArrayElementAtIndex(i).stringValue == relative)) return;
            var index = selection.arraySize++;
            selection.GetArrayElementAtIndex(index).stringValue = relative;
        }

        public override void OnGUI(string searchContext)
        {
            if (_settings == null) return;
            _settings.Update();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(L("Model delivery", "模型发布"), EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(L("Player builds exclude MNN models by default. Bundle essential models and download other declared models when needed.",
                "默认构建不携带 MNN 模型。可随包携带必要模型，其余已配置的模型在使用时下载。"), MessageType.Info);

            using (new EditorGUI.DisabledScope(BuildPipeline.isBuildingPlayer))
            {
                if (MNNModelBuildProcessor.NeedsRecovery)
                {
                    EditorGUILayout.HelpBox(L("A previous build staging transaction needs recovery. Model files are preserved under TestArtifacts~/MNNBuildStaging.",
                        "上次构建暂存需要恢复。模型文件保存在 TestArtifacts~/MNNBuildStaging。"), MessageType.Warning);
                    if (GUILayout.Button(L("Restore model directory", "恢复模型目录")))
                    {
                        try { MNNModelBuildProcessor.Recover(); Refresh(); }
                        catch (Exception error) { _error = error.Message; }
                    }
                }
                EditorGUILayout.LabelField(L("Build packaging", "构建打包"), EditorStyles.boldLabel);
                var mode = _settings.FindProperty("packagingMode");
                mode.enumValueIndex = EditorGUILayout.Popup(L("Bundled models", "随包模型"), mode.enumValueIndex,
                    new[] { L("None (default)", "不携带（默认）"), L("Selected files / folders", "指定文件 / 目录"), L("All models", "全部模型") });
                EditorGUILayout.LabelField("StreamingAssets/MNN/Models", EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(L("Refresh files", "刷新文件"), GUILayout.Width(100))) Refresh();
                    if (GUILayout.Button(L("Model Manager", "模型管理器"), GUILayout.Width(120)))
                        EditorApplication.ExecuteMenuItem("Window/MNN/Model Manager");
                }
                var selection = _settings.FindProperty("includedPaths");
                if (_files.Length == 0)
                    EditorGUILayout.HelpBox(L("No local models found. You can still configure a remote manifest for a model-free build.",
                        "未发现本地模型。仍可通过远程模型清单构建不含模型的安装包。"), MessageType.None);
                using (new EditorGUI.DisabledScope(mode.enumValueIndex != (int)MNNModelPackagingMode.Selected))
                {
                    _modelsScroll = EditorGUILayout.BeginScrollView(_modelsScroll, GUILayout.Height(Math.Min(220, Math.Max(24, _files.Select(path => path.Split('/')[0]).Distinct().Count() * 20 + 4))));
                    foreach (var group in _files.GroupBy(path => path.Split('/')[0]).OrderBy(group => group.Key, StringComparer.Ordinal))
                    {
                        var key = group.Key;
                        bool selected = Enumerable.Range(0, selection.arraySize).Any(i => selection.GetArrayElementAtIndex(i).stringValue == key);
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            var effective = mode.enumValueIndex == (int)MNNModelPackagingMode.All ||
                                mode.enumValueIndex == (int)MNNModelPackagingMode.Selected && selected;
                            var next = EditorGUILayout.ToggleLeft(new GUIContent(key, key), effective, GUILayout.MinWidth(120));
                            EditorGUILayout.LabelField(EditorUtility.FormatBytes(group.Sum(file => _sizes[file])), GUILayout.Width(90));
                            if (mode.enumValueIndex == (int)MNNModelPackagingMode.Selected && next != selected)
                            {
                                if (next)
                                {
                                    var index = selection.arraySize++;
                                    selection.GetArrayElementAtIndex(index).stringValue = key;
                                }
                                else
                                    for (var i = selection.arraySize - 1; i >= 0; i--)
                                        if (selection.GetArrayElementAtIndex(i).stringValue == key) selection.DeleteArrayElementAtIndex(i);
                            }
                        }
                    }
                    EditorGUILayout.EndScrollView();
                    _pathsExpanded = EditorGUILayout.Foldout(_pathsExpanded, L("Specific relative files / subfolders", "指定相对文件 / 子目录"), true);
                    if (_pathsExpanded)
                    {
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            if (GUILayout.Button(L("Add file…", "添加文件…"))) PickPath(selection, false);
                            if (GUILayout.Button(L("Add folder…", "添加目录…"))) PickPath(selection, true);
                        }
                        EditorGUILayout.PropertyField(selection, new GUIContent(L("Include paths", "包含路径")), true);
                        EditorGUILayout.HelpBox(L("Paths are relative to MNN/Models. Selecting a directory includes its dependencies recursively. Individual files do not automatically include dependencies.",
                            "路径相对 MNN/Models。选择目录会递归包含依赖文件；选择单个文件时需要自行加入其依赖。"), MessageType.None);
                    }
                }
                EditorGUILayout.Space(8);
                EditorGUILayout.LabelField(L("Runtime storage & downloads", "运行时存储与下载"), EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_settings.FindProperty("bundledStorage"), new GUIContent(L("Bundled file storage", "随包文件存储"),
                    L("Automatic uses readable bundle files directly; Android APK files are extracted. PersistentData always copies before loading.",
                        "自动：可读文件直接加载，Android APK 内文件先解包。PersistentData：始终复制到持久目录。")));
                EditorGUILayout.PropertyField(_settings.FindProperty("allowDownloads"), new GUIContent(L("Allow runtime downloads", "允许运行时下载")));
                EditorGUILayout.PropertyField(_settings.FindProperty("remoteManifest"), new GUIContent(L("Remote file manifest (JSON)", "远程文件清单（JSON）"),
                    L("Optional TextAsset with files: path, HTTPS url, bytes, sha256. The build pins this snapshot; it never downloads model weights.",
                        "可选 JSON TextAsset，files 包含 path、HTTPS url、bytes、sha256。构建固定清单，不会下载权重。")));
                var timeout = _settings.FindProperty("requestTimeout");
                timeout.intValue = Math.Max(1, EditorGUILayout.IntField(L("Transfer timeout (seconds)", "传输超时（秒）"), timeout.intValue));
                EditorGUILayout.HelpBox(L("Models are prepared on demand through MNNModelDelivery or Model Loader. Downloaded / extracted files use persistentDataPath/MNN/Models. Custom loaders must call the preparation API first.",
                    "通过 MNNModelDelivery 或 Model Loader 按需准备模型。下载 / 解包文件写入 persistentDataPath/MNN/Models。自定义加载器需先调用模型准备 API。"), MessageType.None);
            }
            if (_settings.ApplyModifiedProperties())
            {
                MNNProjectSettings.instance.SaveSettings();
                UpdatePlan();
            }
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(L("Build preview", "构建预览"), EditorStyles.boldLabel);
            if (_error != null) EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (_plan != null)
            {
                EditorGUILayout.LabelField(L("Included model payload", "随包模型体积"), EditorUtility.FormatBytes(_plan.IncludedBytes), EditorStyles.boldLabel);
                EditorGUILayout.LabelField(L("Included files", "随包文件数"), _plan.IncludedFiles.Length.ToString());
                _previewExpanded = EditorGUILayout.Foldout(_previewExpanded, L("Included file paths", "随包文件路径"), true);
                if (_previewExpanded)
                    foreach (var file in _plan.IncludedFiles) EditorGUILayout.LabelField(file, EditorStyles.miniLabel);
            }
            EditorGUILayout.HelpBox(L("Applies to Unity 2021.3+ Player builds, including BuildPipeline.BuildPlayer. Only StreamingAssets/MNN/Models is filtered; other assets use Unity's normal rules. Payload size excludes compression and native libraries.",
                "适用于 Unity 2021.3+ Player 构建及 BuildPipeline.BuildPlayer。仅过滤 StreamingAssets/MNN/Models；其他资源遵循 Unity 原有规则。预览体积不含压缩与原生库。"), MessageType.None);
            EditorGUILayout.EndScrollView();
        }
    }
}
