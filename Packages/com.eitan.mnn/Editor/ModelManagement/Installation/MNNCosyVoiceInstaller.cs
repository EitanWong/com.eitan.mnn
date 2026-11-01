using System;
using System.Collections.Generic;
using System.Threading;
namespace MNN.Unity.Editor
{
    internal static class MNNCosyVoiceInstaller
    {
        internal static void Install(string path, CancellationToken token) => MNNCosyVoiceAssets.Install(path, token);
        internal static void InstallArchive(string path, string hash, IDictionary<string, long> required, CancellationToken token = default, Action<long> onExtracted = null) =>
            MNNCosyVoiceAssets.InstallArchive(path, hash, required, token, onExtracted);
        internal static string Hash(string path, CancellationToken token = default) => MNNCosyVoiceAssets.Hash(path, token);
    }
}
