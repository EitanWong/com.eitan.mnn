using System.Diagnostics;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Explicit setup action. Discovery never installs or changes a user's native backend configuration.</summary>
internal static class BackendInstaller
{
    internal static async Task InstallAsync(HarnessBackendDefinition backend)
    {
        var observed = BackendDiscovery.Scan().FirstOrDefault(item => item.Id == backend.Id);
        foreach (var component in backend.MissingComponents(observed))
        {
            // Recheck after each step. Never reinstall a CLI merely because its login or adapter is missing.
            observed = BackendDiscovery.Scan().FirstOrDefault(item => item.Id == backend.Id);
            if (!backend.MissingComponents(observed).Contains(component)) continue;
            Console.WriteLine("Installing missing " + (component.Adapter ? "adapter" : "backend") + ": " + backend.Name);
            if (component.Package != null) await NpmAsync(component.Package);
            else await ScriptAsync(component.ScriptUrl);
        }
        observed = BackendDiscovery.Scan().FirstOrDefault(item => item.Id == backend.Id);
        if (backend.Command != null && observed?.ProtocolCandidate != true)
            throw new InvalidOperationException("Installation finished, but a required executable or adapter is still missing. Refresh paths and check the backend setup.");
        Console.WriteLine(backend.ConfigurationHint);
    }
    private static Task NpmAsync(string package)
    {
        string npm = BackendDiscovery.Find("npm") ?? throw new FileNotFoundException("Install Node.js with npm, then retry.");
        if (OperatingSystem.IsWindows())
        {
            string node = BackendDiscovery.Find("node") ?? throw new FileNotFoundException("Install Node.js, then retry.");
            string cli = Path.Combine(Path.GetDirectoryName(npm), "node_modules", "npm", "bin", "npm-cli.js");
            if (!File.Exists(cli)) throw new FileNotFoundException("Cannot locate npm-cli.js beside npm. Repair the Node.js installation.");
            return RunAsync(node, [cli, "install", "-g", package]);
        }
        return RunAsync(npm, ["install", "-g", package]);
    }
    private static async Task ScriptAsync(string url)
    {
        bool windows = OperatingSystem.IsWindows();
        if (windows && url == "https://hermes-agent.nousresearch.com/install.sh")
            url = "https://hermes-agent.nousresearch.com/install.ps1";
        else if (windows) throw new PlatformNotSupportedException("Use the backend's official Windows installer, then rescan.");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        using var stream = await response.Content.ReadAsStreamAsync();
        using var buffer = new MemoryStream();
        var block = new byte[8192];
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        int count;
        while ((count = await stream.ReadAsync(block, budget.Token)) != 0)
        {
            if (buffer.Length + count > 1024 * 1024) throw new InvalidDataException("Installer exceeds its download budget.");
            buffer.Write(block, 0, count);
        }
        byte[] bytes = buffer.ToArray();
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MNN", "Harness", "Installers");
        HostServiceRegistration.MakePrivateDirectory(folder);
        string path = Path.Combine(folder, Guid.NewGuid().ToString("N") + (windows ? ".ps1" : ".sh"));
        try { await File.WriteAllBytesAsync(path, bytes); await RunAsync(windows ? "powershell.exe" : "/bin/bash", windows ? ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", path] : [path]); }
        finally { File.Delete(path); }
    }
    private static async Task RunAsync(string command, string[] arguments)
    {
        var start = new ProcessStartInfo(command) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Installer could not start.");
        // Drain both pipes; raw installer output may contain credentials, so return only controlled status.
        async Task DrainAsync(StreamReader reader) { var buffer = new char[4096]; while (await reader.ReadAsync(buffer) != 0) { } }
        Task output = DrainAsync(process.StandardOutput), error = DrainAsync(process.StandardError);
        using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(9));
        try { await process.WaitForExitAsync(budget.Token); }
        catch (OperationCanceledException) { try { process.Kill(true); } catch (InvalidOperationException) { } throw new TimeoutException("Backend installation timed out. Rescan before retrying."); }
        await Task.WhenAll(output, error);
        if (process.ExitCode != 0) throw new InvalidOperationException("Backend installer exited with status " + process.ExitCode + ". Use the official setup instructions, then rescan.");
    }
}
