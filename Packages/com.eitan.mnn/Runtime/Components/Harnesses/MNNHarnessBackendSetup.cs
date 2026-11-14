using System;
using System.IO;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Reusable desktop backend setup. Discovery is read-only; installation and host startup are explicit.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MNN/Agents/Harness Backend Setup")]
    public sealed class MNNHarnessBackendSetup : MonoBehaviour
    {
        public string RuntimePath { get; private set; }
        public string HostAssemblyPath { get; private set; }
        public string ServiceDirectory { get; private set; }
        public bool IsBusy { get; private set; }
        public bool AutomaticDetection { get; set; } = true;
        public string RecommendedBackend => HarnessBackendCatalog.ChooseLocal(Installations);
        public bool DetectionCompleted { get; private set; }
        private float _nextEnvironmentScan;
        private void OnEnable() { if (!Application.isMobilePlatform) RefreshEnvironment(); }
        private void Update()
        {
            if (!AutomaticDetection || Application.isMobilePlatform || IsBusy || Time.unscaledTime < _nextEnvironmentScan) return;
            RefreshEnvironment();
        }
        public void RefreshEnvironment()
        {
            var detected = HarnessLocalDiscovery.Scan();
            bool changed = !DetectionCompleted || !System.Linq.Enumerable.SequenceEqual(
                TaskHostJson.Write(Installations), TaskHostJson.Write(detected));
            Installations = detected;
            DetectionCompleted = true; _nextEnvironmentScan = Time.unscaledTime + 15f;
            if (changed) Changed?.Invoke();
        }
        private string _activeDirectory;
        public HarnessInstallation[] Installations { get; private set; } = Array.Empty<HarnessInstallation>();
        public event Action Changed;
        public void Configure(string runtime = null, string assembly = null, string serviceDirectory = null)
        {
            if (IsBusy) throw new InvalidOperationException("Backend setup is already running.");
            _activeDirectory = null;
            RuntimePath = string.IsNullOrWhiteSpace(runtime) ? HarnessDesktopPaths.FindRuntime() : runtime;
            HostAssemblyPath = string.IsNullOrWhiteSpace(assembly) ? HarnessDesktopPaths.FindHost(new[]
            { Application.streamingAssetsPath, AppDomain.CurrentDomain.BaseDirectory, Path.GetFullPath(Path.Combine(Application.dataPath, "..")) }) : assembly;
            ServiceDirectory = string.IsNullOrWhiteSpace(serviceDirectory) ? Path.Combine(Application.persistentDataPath, "MNN", "Harness", "Local", "registration") : serviceDirectory;
        }
        private DesktopHostServiceClient Client(string directory = null)
        {
            if (ServiceDirectory == null) Configure();
            if (string.IsNullOrWhiteSpace(RuntimePath) || string.IsNullOrWhiteSpace(HostAssemblyPath))
                throw new FileNotFoundException("Local task host was not found. Install .NET 8 and select the published HarnessHost application in advanced settings.");
            return new DesktopHostServiceClient(RuntimePath, HostAssemblyPath, directory ?? _activeDirectory ?? ServiceDirectory, Application.isMobilePlatform);
        }
        private async Task<T> RunAsync<T>(Func<DesktopHostServiceClient, Task<T>> action)
        {
            if (IsBusy) throw new InvalidOperationException("Wait for backend setup to finish.");
            var client = Client();
            IsBusy = true; Changed?.Invoke();
            try { return await action(client); }
            finally { IsBusy = false; Changed?.Invoke(); }
        }
        public Task<HarnessInstallation[]> ScanAsync()
        {
            RefreshEnvironment();
            return Task.FromResult(Installations);
        }
        public Task<string> InstallAsync(string backend) => RunAsync(client => client.InstallBackendAsync(backend));
        public Task<HarnessLocalSetup> PrepareAsync(string backend, string workspace, int port = 0,
            string endpoint = null, string model = null, string apiKey = null)
        {
            if (IsBusy) throw new InvalidOperationException("Wait for backend setup to finish.");
            if (ServiceDirectory == null) Configure();
            string scope = HarnessLocalSetup.ScopeFor(backend, workspace);
            _activeDirectory = ScopeDirectory(scope);
            return RunAsync(async client =>
            {
                var result = await client.PrepareBackendAsync(backend, workspace, port, endpoint, model,
                    string.IsNullOrEmpty(apiKey) ? null : new System.Collections.Generic.Dictionary<string, string>
                    { [backend == "deepseek" ? "DEEPSEEK_API_KEY" : "MNN_HARNESS_LLM_API_KEY"] = apiKey });
                result.ScopeId = scope;
                return result;
            });
        }
        private string ScopeDirectory(string scope)
        {
            if (scope == null || scope.Length != 64 || !System.Linq.Enumerable.All(scope, Uri.IsHexDigit))
                throw new ArgumentException("Invalid local host scope.");
            return Path.Combine(ServiceDirectory, "instances", scope, "registration");
        }
        public string ReadSavedAccessToken(string scope)
        {
            if (ServiceDirectory == null) Configure();
            string directory = ScopeDirectory(scope);
            _activeDirectory = directory;
            return Client(directory).ReadLocalAccessToken(new HarnessLocalSetup
            { TokenPath = Path.GetFullPath(Path.Combine(directory, "..", "setup", "client-token")) });
        }
        public Task<string> StartHostAsync() => RunAsync(client => client.ExecuteAsync("start"));
        public string ReadAccessToken(HarnessLocalSetup setup) => Client().ReadLocalAccessToken(setup);
    }
}
