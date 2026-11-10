using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal static class BackendSetupCli
{
    private const string TokenVariable = "MNN_LOCAL_HARNESS_TOKEN";
    internal static async Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            if (args.Length == 2 && args[0] == "--backend-install")
            {
                await BackendInstaller.InstallAsync(HarnessBackendCatalog.Get(args[1]));
                return 0;
            }
            if (args[0] != "--setup-local" || args.Length % 2 != 1) throw new ArgumentException("Invalid backend setup command.");
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            var allowed = new[] { "--backend", "--workspace", "--directory", "--runtime", "--port", "--endpoint", "--model" };
            for (int i = 1; i < args.Length; i += 2)
                if (!allowed.Contains(args[i]) || !options.TryAdd(args[i], args[i + 1])) throw new ArgumentException("Invalid or repeated setup option.");
            string Need(string key) => options.GetValueOrDefault(key) ?? throw new ArgumentException("Missing " + key);
            var definition = HarnessBackendCatalog.Get(Need("--backend"));
            string workspace = Path.GetFullPath(Need("--workspace"));
            if (!Directory.Exists(workspace)) throw new ArgumentException("Select an existing project folder.");
            string directory = Path.GetFullPath(Need("--directory"));
            if (!int.TryParse(Need("--port"), out int port) || port != 0 && (port < 1024 || port > 65535)) throw new ArgumentException("Choose automatic port 0 or a port from 1024 to 65535.");
            if (port == 0)
            {
                if (File.Exists(Path.Combine(directory, "registration.json"))) port = new Uri(HostServiceRegistration.Load(directory).Listen).Port;
                else
                {
                    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
                    listener.Start();
                    try { port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port; }
                    finally { listener.Stop(); }
                }
            }
            string setup = Path.GetFullPath(Path.Combine(directory, "..", "setup"));
            HostServiceRegistration.MakePrivateDirectory(setup);
            using var setupLock = HostServiceRegistration.AcquireLock(setup);
            string tokenPath = Path.Combine(setup, "client-token");
            string token = File.Exists(tokenPath) ? Encoding.UTF8.GetString(HostServiceRegistration.ReadPrivate(tokenPath)) : Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            if (token.Length != 64 || !token.All(Uri.IsHexDigit)) throw new InvalidDataException("Invalid local host credential.");
            Environment.SetEnvironmentVariable(TokenVariable, token);
            var harness = new HarnessConfiguration { Id = definition.Id, Backend = definition.Id, ReuseContext = definition.Integration == "acp" };
            if (definition.Id == "llm")
            {
                harness.Endpoint = Need("--endpoint"); harness.Model = Need("--model");
                harness.TokenEnvironmentVariable = "MNN_HARNESS_LLM_API_KEY";
            }
            var config = new HostConfiguration
            {
                Listen = "http://127.0.0.1:" + port, DataDirectory = Path.Combine(setup, "tasks"),
                Identities = [new HostIdentity { OwnerId = "local", TokenEnvironmentVariable = TokenVariable }],
                Workspaces = new() { ["project"] = workspace }, Harnesses = [harness]
            };
            string input = Path.Combine(setup, "generated-host.json");
            HostServiceRegistration.WritePrivate(tokenPath, Encoding.UTF8.GetBytes(token));
            HostServiceRegistration.WritePrivate(input, JsonSerializer.SerializeToUtf8Bytes(config, HostServiceRegistration.Json));
            HostServiceRegistration.Prepare("desktop", input, Need("--runtime"), directory, HostServiceRegistration.CurrentPlatform, []);
            Console.WriteLine(Encoding.UTF8.GetString(TaskHostJson.Write(new HarnessLocalSetup
            { Address = config.Listen, Backend = definition.Id, Workspace = "project", TokenPath = tokenPath })));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("Backend setup: " + error.Message); return 2; }
    }
}
