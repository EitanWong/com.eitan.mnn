using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    public sealed partial class DesktopHostServiceClient
    {
        public Task<string> InstallBackendAsync(string backend)
        {
            HarnessBackendCatalog.Get(backend);
            return RunAsync(new[] { "--backend-install", backend }, null, 620000);
        }
        public async Task<HarnessLocalSetup> PrepareBackendAsync(string backend, string workspace, int port = 8765,
            string endpoint = null, string model = null, IDictionary<string, string> environment = null)
        {
            HarnessBackendCatalog.Get(backend);
            var args = new List<string> { "--setup-local", "--backend", backend, "--workspace", Path.GetFullPath(workspace),
                "--directory", _directory, "--runtime", _runtime, "--port", port.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            if (!string.IsNullOrWhiteSpace(endpoint)) { args.Add("--endpoint"); args.Add(endpoint); }
            if (!string.IsNullOrWhiteSpace(model)) { args.Add("--model"); args.Add(model); }
            return TaskHostJson.Read<HarnessLocalSetup>(Encoding.UTF8.GetBytes(await RunAsync(args.ToArray(), environment).ConfigureAwait(false)));
        }
        public string ReadLocalAccessToken(HarnessLocalSetup setup)
        {
            string expected = Path.GetFullPath(Path.Combine(_directory, "..", "setup", "client-token"));
            if (setup == null || Path.GetFullPath(setup.TokenPath) != expected) throw new ArgumentException("Unexpected local credential path.");
            var file = new FileInfo(expected);
            if (file.Length > 4096) throw new InvalidDataException("Local credential exceeds its budget.");
            return File.ReadAllText(expected).Trim();
        }
    }
}
