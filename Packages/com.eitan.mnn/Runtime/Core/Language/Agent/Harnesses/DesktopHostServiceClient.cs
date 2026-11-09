using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Desktop-only CLI control. The OS owns the host; disposing a Unity client never stops it.</summary>
    public sealed class DesktopHostServiceClient
    {
        private readonly string _runtime, _assembly, _directory;
        public DesktopHostServiceClient(string runtime, string assembly, string directory, bool mobile)
        {
            if (mobile) throw new PlatformNotSupportedException("Mobile clients connect to a remote task host.");
            _runtime = Path.GetFullPath(runtime);
            _assembly = Path.GetFullPath(assembly);
            _directory = Path.GetFullPath(directory);
        }
        public Task<string> PrepareAsync(string id, string config, IDictionary<string, string> environment = null) =>
            RunAsync(new[] { "--service", "prepare", "--directory", _directory, "--id", id, "--config", Path.GetFullPath(config), "--runtime", _runtime }, environment);
        public async Task<HarnessInstallation[]> DiscoverAsync() => TaskHostJson.Read<HarnessInstallation[]>(
            Encoding.UTF8.GetBytes(await RunAsync(new[] { "--discover" }, null).ConfigureAwait(false)));
        public Task<string> ExecuteAsync(string command)
        {
            if (!new[] { "status", "start", "stop", "install", "uninstall" }.Contains(command))
                throw new ArgumentException("Choose a desktop service lifecycle command.", nameof(command));
            return RunAsync(new[] { "--service", command, "--directory", _directory }, null);
        }
        private async Task<string> RunAsync(string[] arguments, IDictionary<string, string> environment)
        {
            if (!File.Exists(_runtime) || !File.Exists(_assembly)) throw new FileNotFoundException("Select an existing dotnet runtime and SiriAI.HarnessHost.dll.");
            var start = new ProcessStartInfo(_runtime, string.Join(" ", new[] { _assembly }.Concat(arguments).Select(Quote)))
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(_assembly)
            };
            if (environment != null)
                foreach (var pair in environment) start.EnvironmentVariables[pair.Key] = pair.Value;
            using (var process = new Process { StartInfo = start })
            {
                if (!process.Start()) throw new IOException("The desktop service command did not start.");
                var output = ReadBoundedAsync(process.StandardOutput);
                var error = ReadBoundedAsync(process.StandardError);
                var exit = Task.Run(() => process.WaitForExit(45000));
                if (!await exit.ConfigureAwait(false))
                {
                    // Only this short-lived CLI is ours. Never kill the independent host or its backend processes.
                    try { process.Kill(); } catch (InvalidOperationException) { }
                    throw new TimeoutException("The service command timed out. Check service status before retrying.");
                }
                string stdout = await output.ConfigureAwait(false), stderr = await error.ConfigureAwait(false);
                if (process.ExitCode != 0) throw new InvalidOperationException(string.IsNullOrWhiteSpace(stderr) ? "The desktop service command failed." : stderr.Trim());
                return stdout.Trim();
            }
        }
        private static async Task<string> ReadBoundedAsync(StreamReader input)
        {
            var result = new StringBuilder(); var buffer = new char[2048]; int count;
            while ((count = await input.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
                if (result.Length < 32768) result.Append(buffer, 0, Math.Min(count, 32768 - result.Length));
            return result.ToString();
        }
        private static string Quote(string value)
        {
            var text = new StringBuilder("\""); int slashes = 0;
            foreach (char character in value)
            {
                if (character == '\\') { slashes++; continue; }
                text.Append('\\', character == '"' ? slashes * 2 + 1 : slashes).Append(character);
                slashes = 0;
            }
            return text.Append('\\', slashes * 2).Append('"').ToString();
        }
    }
}
