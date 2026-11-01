using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>Uses Unity's public 2021.3+ build API, including scripted BuildPipeline.BuildPlayer calls.</summary>
    public sealed class MNNModelBuildProcessor : BuildPlayerProcessor, IPostprocessBuildWithReport
    {
        public override int callbackOrder => int.MinValue + 100;
        internal static string ModelsRoot => Path.Combine(Application.streamingAssetsPath, MNNModelDeliveryManifest.ModelsPath);
        private static MNNModelBuildStaging Staging => new MNNModelBuildStaging(ModelsRoot,
            Path.GetFullPath("TestArtifacts~/MNNBuildStaging"));

        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            var staging = Staging;
            if (staging.HasTransaction) throw new BuildFailedException("Recover the previous MNN model staging transaction before building. Open Project Settings > MNN.");
            var existingManifest = Path.Combine(Application.streamingAssetsPath, MNNModelDeliveryManifest.StreamingPath);
            if (File.Exists(existingManifest)) throw new BuildFailedException("Reserved MNN build manifest path already exists: " + existingManifest);
            var settings = MNNProjectSettings.instance;
            try
            {
                var plan = MNNModelBuildPlan.Create(ModelsRoot, settings.packagingMode, settings.includedPaths);
                var manifest = plan.CreateManifest(ModelsRoot, settings);
                staging.Begin(manifest);
                foreach (var path in plan.IncludedFiles)
                    buildPlayerContext.AddAdditionalPathToStreamingAssets(staging.SourceFile(path), MNNModelDeliveryManifest.ModelsPath + "/" + path);
                buildPlayerContext.AddAdditionalPathToStreamingAssets(staging.ManifestPath, MNNModelDeliveryManifest.StreamingPath);
                Debug.Log($"[MNN] Model packaging: {settings.packagingMode}, {plan.IncludedFiles.Length} files, {plan.IncludedBytes} bytes. Runtime manifest: {manifest.files.Length} files.");
            }
            catch
            {
                staging.Restore();
                throw;
            }
        }

        public void OnPostprocessBuild(BuildReport report) => Staging.Restore();

        [InitializeOnLoadMethod]
        private static void RegisterRecovery()
        {
            EditorApplication.update -= RecoverAfterBuild;
            EditorApplication.update += RecoverAfterBuild;
            EditorApplication.quitting -= RestoreBeforeQuit;
            EditorApplication.quitting += RestoreBeforeQuit;
        }

        private static bool _reportedRecoveryFailure;
        internal static bool NeedsRecovery => Staging.HasTransaction;
        internal static void Recover()
        {
            if (BuildPipeline.isBuildingPlayer) throw new InvalidOperationException("Wait for the current Player build to finish.");
            Staging.Restore();
            _reportedRecoveryFailure = false;
        }

        private static void RecoverAfterBuild()
        {
            // Postprocess callbacks are not guaranteed after cancellation or failures in another build hook.
            if (BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling || _reportedRecoveryFailure || !NeedsRecovery) return;
            try { Recover(); }
            catch (Exception error)
            {
                _reportedRecoveryFailure = true;
                Debug.LogError("[MNN] Model build recovery requires attention in Project Settings > MNN. " + error.Message);
            }
        }

        private static void RestoreBeforeQuit()
        {
            if (!BuildPipeline.isBuildingPlayer && NeedsRecovery) RecoverAfterBuild();
        }
    }
}
