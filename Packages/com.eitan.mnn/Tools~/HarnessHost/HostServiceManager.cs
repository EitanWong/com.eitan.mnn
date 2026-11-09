using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Xml.Linq;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

internal sealed class HostServiceManager
{
    private readonly HostServiceRegistration _registration;
    internal HostServiceManager(HostServiceRegistration registration)
    {
        _registration = registration;
        if (registration.Platform != HostServiceRegistration.CurrentPlatform) throw new PlatformNotSupportedException("This definition targets another desktop platform.");
    }
    private string Domain => "gui/" + UserId();
    // launchctl's documented user domain takes the numeric uid; id is a system CLI, not native bridge code.
    private static string UserId()
    {
        var result = Command("/usr/bin/id", ["-u"]);
        if (result.Code != 0 || !uint.TryParse(result.Output.Trim(), out _)) throw new IOException("Could not resolve the login uid.");
        return result.Output.Trim();
    }
    private static string Systemctl => File.Exists("/usr/bin/systemctl") ? "/usr/bin/systemctl" : "/bin/systemctl";
    private static string Scheduler => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "schtasks.exe");
    internal bool IsLoaded() => _registration.Platform switch
    {
        "macos" => Command("/bin/launchctl", ["print", Domain + "/" + _registration.Label]).Code == 0,
        "linux" => Command(Systemctl, ["--user", "show", _registration.Label + ".service", "--property=LoadState", "--value"]).Output.Trim() == "loaded",
        _ => Command(Scheduler, ["/Query", "/TN", _registration.Label, "/XML"]).Code == 0
    };
    internal async Task<ServiceStatus> StatusAsync()
    {
        bool loaded = IsLoaded();
        var probe = await ProbeAsync();
        return new ServiceStatus { Label = _registration.Label, Platform = _registration.Platform, ManagerLoaded = loaded,
            EndpointState = probe.State, Health = probe.Health };
    }
    internal async Task<ServiceStatus> StartAsync(bool install)
    {
        var probe = await ProbeAsync();
        if (probe.State == "Conflict") throw new InvalidOperationException("The configured endpoint is owned by another host or credential. No process was stopped.");
        // Validate the saved environment/config before any service manager mutation.
        _ = HostConfiguration.LoadWithEnvironment(_registration);
        bool loaded = IsLoaded();
        if (install) InstallDefinition();
        if (probe.State != "Ready")
        {
            switch (_registration.Platform)
            {
                case "macos":
                    if (!loaded) Require(Command("/bin/launchctl", ["bootstrap", Domain, install ? InstalledDefinition : _registration.DefinitionPath]));
                    else Require(Command("/bin/launchctl", ["kickstart", Domain + "/" + _registration.Label]));
                    break;
                case "linux":
                    if (install) Require(Command(Systemctl, ["--user", "start", _registration.Label + ".service"]));
                    else if (!loaded)
                    {
                        string executable = File.Exists("/usr/bin/systemd-run") ? "/usr/bin/systemd-run" : "/bin/systemd-run";
                        Require(Command(executable, new[] { "--user", "--unit=" + _registration.Label, "--collect", "--property=Restart=on-failure", "--property=RestartSec=5", "--property=UMask=0077", "--property=WorkingDirectory=" + _registration.Directory, "--", _registration.Runtime }.Concat(_registration.RunArguments).ToArray()));
                    }
                    else Require(Command(Systemctl, ["--user", "start", _registration.Label + ".service"]));
                    break;
                default:
                    if (!loaded && !install) RegisterWindowsTask(false);
                    Require(Command(Scheduler, ["/Run", "/TN", _registration.Label]));
                    break;
            }
        }
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var status = await StatusAsync();
            if (status.EndpointState == "Ready") return status;
            if (status.EndpointState == "Conflict") throw new InvalidOperationException("The endpoint identity changed during startup.");
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("The service has not confirmed authenticated readiness. Inspect status and host logs before retrying.");
            await Task.Delay(100);
        }
    }
    private string InstalledDefinition => _registration.Platform switch
    {
        "macos" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "LaunchAgents", _registration.Label + ".plist"),
        "linux" => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "systemd", "user", _registration.Label + ".service"),
        _ => _registration.DefinitionPath
    };
    private void InstallDefinition()
    {
        if (_registration.Platform == "windows") { RegisterWindowsTask(true); return; }
        string destination = InstalledDefinition;
        if (File.Exists(destination) && !File.ReadAllBytes(destination).SequenceEqual(File.ReadAllBytes(_registration.DefinitionPath)))
            throw new IOException("The login service file contains other edits. Stop and review it before replacing it.");
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        HostServiceRegistration.WritePrivate(destination, File.ReadAllBytes(_registration.DefinitionPath));
        if (_registration.Platform == "linux")
        {
            Require(Command(Systemctl, ["--user", "daemon-reload"]));
            Require(Command(Systemctl, ["--user", "enable", _registration.Label + ".service"]));
        }
    }
    private void RegisterWindowsTask(bool login)
    {
        string path = _registration.DefinitionPath;
        if (!login)
        {
            var definition = XDocument.Parse(_registration.Render());
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            definition.Root.Element(ns + "Triggers").RemoveNodes();
            path = Path.Combine(_registration.Directory, "manual-start.xml");
            HostServiceRegistration.WritePrivate(path, System.Text.Encoding.UTF8.GetBytes(definition.ToString()));
        }
        Require(Command(Scheduler, ["/Create", "/TN", _registration.Label, "/XML", path, "/F"]));
    }
    internal async Task<ServiceStatus> StopAsync(bool uninstall)
    {
        if (IsLoaded())
        {
            switch (_registration.Platform)
            {
                case "macos": Require(Command("/bin/launchctl", ["bootout", Domain + "/" + _registration.Label])); break;
                case "linux": Require(Command(Systemctl, ["--user", "stop", _registration.Label + ".service"])); break;
                default:
                    // Task Scheduler may already have a stopped registration. Readiness determines whether End is needed.
                    if ((await ProbeAsync()).State == "Ready") Require(Command(Scheduler, ["/End", "/TN", _registration.Label]));
                    break;
            }
        }
        if (uninstall)
        {
            if (_registration.Platform == "windows")
            { if (IsLoaded()) Require(Command(Scheduler, ["/Delete", "/TN", _registration.Label, "/F"])); }
            else if (File.Exists(InstalledDefinition))
            {
                if (!File.ReadAllBytes(InstalledDefinition).SequenceEqual(File.ReadAllBytes(_registration.DefinitionPath)))
                    throw new IOException("The login service file contains later edits; it was preserved.");
                if (_registration.Platform == "linux") Require(Command(Systemctl, ["--user", "disable", _registration.Label + ".service"]));
                File.Delete(InstalledDefinition);
                if (_registration.Platform == "linux") Require(Command(Systemctl, ["--user", "daemon-reload"]));
            }
        }
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var status = await StatusAsync();
            if (status.EndpointState != "Ready") return status;
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("The service has not confirmed shutdown; no other process was killed.");
            await Task.Delay(100);
        }
    }
    private async Task<(string State, HarnessHostHealth Health)> ProbeAsync()
    {
        var credentials = _registration.ReadCredentials();
        if (!credentials.TryGetValue(_registration.ProbeVariable, out var token)) throw new InvalidDataException("The readiness credential is missing.");
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(2) };
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(_registration.Listen), "/v1/health"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode) return ("Conflict", null);
            using var input = await response.Content.ReadAsStreamAsync(deadline.Token);
            using var output = new MemoryStream();
            var buffer = new byte[4096]; int count;
            while ((count = await input.ReadAsync(buffer, deadline.Token)) > 0)
            {
                if (output.Length + count > 8192) return ("Conflict", null);
                output.Write(buffer, 0, count);
            }
            var data = output.ToArray();
            HarnessHostHealth health;
            try { health = TaskHostJson.Read<HarnessHostHealth>(data); }
            catch (Exception) { return ("Conflict", null); }
            return health != null && health.ProtocolVersion == 1 && health.HostId == _registration.HostId && health.ServiceLabel == _registration.Label
                ? ("Ready", health) : ("Conflict", health);
        }
        catch (HttpRequestException) { return ("Stopped", null); }
        catch (TaskCanceledException) { return ("Unreachable", null); }
    }
    private sealed record CommandResult(int Code, string Output, string Error);
    private static CommandResult Command(string executable, string[] arguments)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new IOException("Could not start the system service manager.");
        var output = ReadBoundedAsync(process.StandardOutput); var error = ReadBoundedAsync(process.StandardError);
        if (!process.WaitForExit(15000)) { process.Kill(true); throw new TimeoutException("System service manager did not return within 15 seconds."); }
        return new CommandResult(process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
    private static async Task<string> ReadBoundedAsync(StreamReader reader)
    {
        var text = new System.Text.StringBuilder(); var buffer = new char[4096]; int count;
        while ((count = await reader.ReadAsync(buffer)) > 0) if (text.Length < 32768) text.Append(buffer, 0, Math.Min(count, 32768 - text.Length));
        return text.ToString();
    }
    private static void Require(CommandResult result)
    { if (result.Code != 0) throw new IOException("System service manager returned " + result.Code + ": " + result.Error.Trim()); }
}

internal sealed class ServiceStatus
{
    public string Label { get; set; }
    public string Platform { get; set; }
    public bool ManagerLoaded { get; set; }
    public string EndpointState { get; set; }
    public HarnessHostHealth Health { get; set; }
}
