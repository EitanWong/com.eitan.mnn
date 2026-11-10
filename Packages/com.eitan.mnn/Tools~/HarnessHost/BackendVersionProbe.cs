using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Bounded official --version query. Inventory reads never run this probe or install anything.</summary>
internal static class BackendVersionProbe
{
    internal static async Task<HarnessBackendVersion> CheckAsync(HarnessConfiguration config, string directory, CancellationToken cancellation)
    {
        var profile = string.IsNullOrEmpty(config.Backend) ? null : BackendProfiles.Get(config.Backend);
        if (profile?.MinimumVersion == null) return null;
        var result = new HarnessBackendVersion { Minimum = profile.MinimumVersion, State = "unconfirmed" };
        string executable = config.NativeCommand ?? config.Command;
        string output = await ReadAsync(executable, config, directory, cancellation);
        string version = Parse(output);
        if (version == null) return result;
        result.Version = version;
        result.State = Version.Parse(version) >= Version.Parse(result.Minimum) ? "compatible" : "too_old";
        return result;
    }
    private static string Parse(string output)
    {
        if (output == null) return null;
        var line = output.Trim().Split('\n')[0].Trim();
        var match = Regex.Match(line, @"(\d+)\.(\d+)\.(\d+)", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50));
        if (!match.Success) return null;
        var parts = new int[3];
        for (int i = 0; i < parts.Length; ++i)
            if (!int.TryParse(match.Groups[i + 1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out parts[i])) return null;
        return string.Join(".", parts);
    }
    private static ProcessStartInfo StartInfo(string executable, HarnessConfiguration config, string directory)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = directory, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (OperatingSystem.IsWindows() && Path.GetExtension(executable).ToLowerInvariant() is ".cmd" or ".bat")
        {
            // cmd.exe expands these characters even when the path is quoted. Fail unconfirmed for such a path.
            if (executable.IndexOfAny("\"%&|<>^!\r\n".ToCharArray()) >= 0) throw new IOException("Unsupported batch executable path.");
            start.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            start.Arguments = "/d /s /c \"\"" + executable + "\" --version\"";
        }
        else start.ArgumentList.Add("--version");
        start.Environment.Clear();
        foreach (string name in BackendProcessEnvironment.AvailableSystemNames().Concat(config.ForwardEnvironment).Distinct(StringComparer.Ordinal))
            if (Environment.GetEnvironmentVariable(name) is string value) start.Environment[name] = value;
        foreach (var pair in config.ProcessEnvironment) start.Environment[pair.Key] = pair.Value;
        start.Environment["PWD"] = directory;
        return start;
    }
    private static async Task<string> ReadAsync(string executable, HarnessConfiguration config, string directory, CancellationToken cancellation)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        budget.CancelAfter(TimeSpan.FromSeconds(5));
        Process process = null;
        try
        {
            process = Process.Start(StartInfo(executable, config, directory)) ?? throw new IOException("Version process did not start.");
            process.StandardInput.Close();
            var output = ReadLimitedAsync(process.StandardOutput.BaseStream, budget.Token);
            var error = ReadLimitedAsync(process.StandardError.BaseStream, budget.Token);
            await Task.WhenAll(output, error, process.WaitForExitAsync(budget.Token));
            if (process.ExitCode != 0) return null;
            return string.IsNullOrWhiteSpace(output.Result) ? error.Result : output.Result;
        }
        catch (Exception error) when (error is IOException or DecoderFallbackException or System.ComponentModel.Win32Exception or OperationCanceledException)
        { return null; }
        finally
        {
            if (process != null)
            {
                try
                {
                    if (!process.HasExited) process.Kill(true);
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await process.WaitForExitAsync(cleanup.Token);
                }
                catch (Exception error) when (error is InvalidOperationException or OperationCanceledException or System.ComponentModel.Win32Exception) { }
                process.Dispose();
            }
        }
    }
    private static async Task<string> ReadLimitedAsync(Stream stream, CancellationToken cancellation)
    {
        using var output = new MemoryStream();
        var buffer = new byte[1024];
        int count;
        while ((count = await stream.ReadAsync(buffer, cancellation)) != 0)
        {
            if (output.Length + count > 4096) throw new IOException("Version output exceeds 4 KiB.");
            output.Write(buffer, 0, count);
        }
        return new UTF8Encoding(false, true).GetString(output.ToArray());
    }
}
