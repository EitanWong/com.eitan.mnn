using System;
using System.Collections.Generic;
namespace MNN.Unity
{
    public enum MNNCloudSpeechTask { Recognition = 1, Synthesis = 2 }
    public enum MNNCloudSpeechProtocol { OpenAISpeech = 1, DashScopeRecognition, DashScopeSpeech, VolcengineRecognition, VolcengineSpeech, MiniMaxSpeech }
    /// <summary>Explicit protocol configuration. Service presets and credential storage belong to the application.</summary>
    public sealed class MNNCloudSpeechOptions
    {
        public string Endpoint, Model, Voice, Language, ApplicationId, ResourceId, DisplayName = "Cloud speech";
        public string[] SpeechLanguages = Array.Empty<string>();
        public int TimeoutSeconds = 60;
        public MNNCloudSpeechProtocol Protocol;
        public MNNCloudSpeechTask Task;
        internal MNNCloudSpeechOptions Snapshot()
        {
            Resolve("");
            bool recognition = Protocol == MNNCloudSpeechProtocol.DashScopeRecognition || Protocol == MNNCloudSpeechProtocol.VolcengineRecognition;
            if (!Enum.IsDefined(typeof(MNNCloudSpeechProtocol), Protocol) ||
                Task != (recognition ? MNNCloudSpeechTask.Recognition : MNNCloudSpeechTask.Synthesis))
                throw new ArgumentException("Select a speech protocol matching its task.");
            var copy = (MNNCloudSpeechOptions)MemberwiseClone();
            copy.SpeechLanguages = MNNRemoteProviderOptions.CopyLanguages(SpeechLanguages);
            return copy;
        }
        internal Uri Resolve(string path) => MNNRemoteProviderOptions.Resolve(Endpoint, Model, TimeoutSeconds, path);
    }
    internal static class MNNRemoteProviderOptions
    {
        internal static Uri Resolve(string endpoint, string model, int timeout, string path)
        {
            if (!Uri.TryCreate((endpoint ?? "").TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
                !(uri.Scheme == "https" || uri.Scheme == "http" && uri.IsLoopback) ||
                uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                throw new ArgumentException("Use an HTTPS API base URL without credentials/query, or HTTP loopback for development.");
            if (timeout < 1 || timeout > 300) throw new ArgumentOutOfRangeException(nameof(timeout));
            if (string.IsNullOrWhiteSpace(model) || model.Length > 1024) throw new ArgumentException("Select a bounded model identifier.");
            return new Uri(uri, path);
        }
        internal static string[] CopyLanguages(string[] languages)
        {
            if (languages == null) return Array.Empty<string>();
            if (languages.Length > 64) throw new ArgumentException("Choose at most 64 speech languages.");
            foreach (string code in languages)
            {
                if (string.IsNullOrWhiteSpace(code) || code.Length > 35) throw new ArgumentException("Use bounded speech language codes.");
                foreach (char c in code) if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-')) throw new ArgumentException("Use speech language codes such as en or zh-CN.");
            }
            return (string[])languages.Clone();
        }
    }
}
