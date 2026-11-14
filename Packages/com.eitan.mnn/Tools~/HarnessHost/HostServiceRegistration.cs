using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SiriAI.HarnessHost;

internal sealed class HostServiceRegistration
{
    public int Schema { get; set; } = 1;
    public string Id { get; set; }
    public string Label { get; set; }
    public string Platform { get; set; }
    public string Directory { get; set; }
    public string Runtime { get; set; }
    public string Assembly { get; set; }
    public string Listen { get; set; }
    public string HostId { get; set; }
    public string ProbeVariable { get; set; }
    public string ConfigHash { get; set; }
    public string Principal { get; set; }
    internal string ConfigPath => Path.Combine(Directory, "host.json");
    internal string CredentialsPath => Path.Combine(Directory, "credentials.json");
    internal string DefinitionPath => Path.Combine(Directory, Platform == "macos" ? "host.plist" : Platform == "linux" ? Label + ".service" : "host.xml");
    internal string ManifestPath => Path.Combine(Directory, "registration.json");
    internal string[] RunArguments => [Assembly, "--service", "run", "--directory", Directory];
    internal static string CurrentPlatform => OperatingSystem.IsMacOS() ? "macos" : OperatingSystem.IsLinux() ? "linux" : OperatingSystem.IsWindows() ? "windows" : throw new PlatformNotSupportedException("Desktop services require macOS, Linux or Windows.");
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    internal static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static string ServiceLabel(string id, string directory)
    {
        if (id == null || !Regex.IsMatch(id, "^[a-z][a-z0-9-]{0,40}$")) throw new ArgumentException("Service id must be a lowercase name of at most 41 characters.");
        return "com.eitan.siriai.harness." + id + "." + Hash(Encoding.UTF8.GetBytes(directory))[..12];
    }
    internal static HostServiceRegistration Prepare(string id, string configPath, string runtime, string directory, string platform, string[] extraEnvironment)
    {
        directory = Path.GetFullPath(directory);
        if (directory.Any(char.IsControl) || runtime.Any(char.IsControl)) throw new ArgumentException("Service paths cannot contain control characters.");
        runtime = Path.GetFullPath(runtime);
        if (!File.Exists(runtime)) throw new ArgumentException("The selected dotnet runtime does not exist.");
        var config = HostConfiguration.Load(Path.GetFullPath(configPath));
        if (config.Harnesses.Any(h => h == null || string.IsNullOrWhiteSpace(h.Id) || !SupportedServiceProtocol(h.Protocol)) ||
            config.Harnesses.Select(h => h.Id).Distinct(StringComparer.Ordinal).Count() != config.Harnesses.Length)
            throw new ArgumentException("Service harnesses need unique ids and implemented protocols.");
        foreach (var harness in config.Harnesses.Where(h => h.Protocol is "acp" or "msp")) harness.Command = ResolveExecutable(harness.Command);
        var names = config.Identities.Select(i => i.TokenEnvironmentVariable)
            .Concat(config.CoordinatorMcpScopes.Select(s => s.TokenEnvironmentVariable))
            .Concat(config.Harnesses.Select(h => h.TokenEnvironmentVariable))
            .Concat(config.Harnesses.SelectMany(h => h.ForwardEnvironment ?? []))
            .Concat(config.Skills?.ForwardEnvironment ?? [])
            .Concat(config.Harnesses.SelectMany(AcpMcpConfiguration.EnvironmentNames))
            .Concat(extraEnvironment).Concat(BackendProcessEnvironment.AvailableSystemNames()).Where(n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var credentials = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string name in names)
        {
            if (!Regex.IsMatch(name, "^[A-Za-z_][A-Za-z0-9_]{0,127}$")) throw new ArgumentException("Invalid service environment variable name.");
            string value = Environment.GetEnvironmentVariable(name);
            if (value != null) credentials.Add(name, value);
        }
        byte[] configBytes = JsonSerializer.SerializeToUtf8Bytes(config, Json);
        var registration = new HostServiceRegistration
        {
            Id = id, Label = ServiceLabel(id, directory), Directory = directory, Runtime = runtime,
            Assembly = Path.GetFullPath(System.Reflection.Assembly.GetExecutingAssembly().Location), Platform = platform,
            Listen = config.Listen, HostId = config.HostId, ProbeVariable = config.Identities[0].TokenEnvironmentVariable,
            ConfigHash = Hash(configBytes), Principal = OperatingSystem.IsWindows() ? WindowsIdentity.GetCurrent().User.Value : Environment.UserName
        };
        if (!new[] { "macos", "linux", "windows" }.Contains(platform)) throw new ArgumentException("Unknown desktop service platform.");
        var files = new Dictionary<string, byte[]>
        {
            [registration.ConfigPath] = configBytes,
            [registration.CredentialsPath] = JsonSerializer.SerializeToUtf8Bytes(credentials, Json),
            [registration.DefinitionPath] = Encoding.UTF8.GetBytes(registration.Render()),
            [registration.ManifestPath] = JsonSerializer.SerializeToUtf8Bytes(registration, Json)
        };
        bool IsPreparationFile(string path) => Path.GetFileName(path) == "prepare.lock" || Regex.IsMatch(Path.GetFileName(path), "^registration\\.json\\.[a-f0-9]{32}\\.tmp$");
        if (System.IO.Directory.Exists(directory) && !File.Exists(registration.ManifestPath) &&
            System.IO.Directory.EnumerateFileSystemEntries(directory).Any(path => !IsPreparationFile(path)))
            throw new IOException("Choose an empty directory or this service's existing directory.");
        MakePrivateDirectory(directory);
        using var preparation = AcquireLock(directory);
        if (File.Exists(registration.ManifestPath))
        {
            // A stopped registration can repair an interrupted multi-file write using its saved identity.
            var previous = Load(directory, false);
            if (previous.Id != id || previous.Platform != platform) throw new IOException("This directory belongs to another service registration.");
            bool changed = files.Any(pair => !File.Exists(pair.Key) || !File.ReadAllBytes(pair.Key).SequenceEqual(pair.Value));
            if (changed && platform == CurrentPlatform && new HostServiceManager(previous).IsLoaded())
                throw new InvalidOperationException("Stop the service before preparing a changed configuration.");
        }
        // Persist identity first; each subsequent file uses flush + atomic replacement.
        WritePrivate(registration.ManifestPath, files[registration.ManifestPath]);
        foreach (var pair in files.Where(pair => pair.Key != registration.ManifestPath)) WritePrivate(pair.Key, pair.Value);
        return registration;
    }

    private static bool SupportedServiceProtocol(string protocol) => protocol is "acp" or "msp" or "a2a" or "http" or "llm"
#if DOTCRAFT
        or "dotcraft"
#endif
        ;
    internal static HostServiceRegistration Load(string directory, bool verifyFiles = true)
    {
        directory = Path.GetFullPath(directory);
        var file = Path.Combine(directory, "registration.json");
        var registration = JsonSerializer.Deserialize<HostServiceRegistration>(ReadPrivate(file)) ?? throw new InvalidDataException("Missing service registration.");
        if (registration.Schema != 1 || registration.Directory != directory || registration.Label != ServiceLabel(registration.Id, directory) ||
            !new[] { "macos", "linux", "windows" }.Contains(registration.Platform) ||
            !Path.IsPathFullyQualified(registration.Runtime ?? "") || !Path.IsPathFullyQualified(registration.Assembly ?? "") ||
            !Uri.TryCreate(registration.Listen, UriKind.Absolute, out var address) || (address.Scheme != "http" && address.Scheme != "https") ||
            string.IsNullOrEmpty(registration.ProbeVariable)) throw new InvalidDataException("Invalid service registration.");
        if (!verifyFiles) return registration;
        if (Hash(ReadPrivate(registration.ConfigPath)) != registration.ConfigHash) throw new InvalidDataException("The service config changed; prepare the registration again.");
        if (!File.ReadAllBytes(registration.DefinitionPath).SequenceEqual(Encoding.UTF8.GetBytes(registration.Render())))
            throw new InvalidDataException("The service definition changed; prepare the registration again.");
        return registration;
    }
    internal static FileStream AcquireLock(string directory)
    {
        string path = Path.Combine(Path.GetFullPath(directory), "prepare.lock");
        if (new FileInfo(path).LinkTarget != null) throw new IOException("Service lock must not be a symlink.");
        var options = new FileStreamOptions { Mode = FileMode.OpenOrCreate, Access = FileAccess.ReadWrite, Share = FileShare.None };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }
    internal Dictionary<string, string> ReadCredentials()
    {
        byte[] bytes = ReadPrivate(CredentialsPath);
        if (bytes.Length > 256 * 1024) throw new InvalidDataException("Service environment exceeds its budget.");
        var values = JsonSerializer.Deserialize<Dictionary<string, string>>(bytes) ?? throw new InvalidDataException("Missing service environment.");
        foreach (var pair in values)
            if (!Regex.IsMatch(pair.Key, "^[A-Za-z_][A-Za-z0-9_]{0,127}$") || pair.Value == null || pair.Value.Length > 32768)
                throw new InvalidDataException("Invalid service environment.");
        return values;
    }
    internal string Render()
    {
        if (Platform == "macos")
        {
            var dict = new XElement("dict", new XElement("key", "Label"), new XElement("string", Label),
                new XElement("key", "ProgramArguments"), new XElement("array", new[] { Runtime }.Concat(RunArguments).Select(a => new XElement("string", a))),
                new XElement("key", "WorkingDirectory"), new XElement("string", Directory),
                new XElement("key", "RunAtLoad"), new XElement("true"),
                new XElement("key", "KeepAlive"), new XElement("true"),
                new XElement("key", "ThrottleInterval"), new XElement("integer", 10),
                new XElement("key", "ExitTimeOut"), new XElement("integer", 30),
                new XElement("key", "StandardOutPath"), new XElement("string", Path.Combine(Directory, "host.log")),
                new XElement("key", "StandardErrorPath"), new XElement("string", Path.Combine(Directory, "host-error.log")));
            return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + new XDocument(new XDeclaration("1.0", "UTF-8", null), new XDocumentType("plist", "-//Apple//DTD PLIST 1.0//EN", "http://www.apple.com/DTDs/PropertyList-1.0.dtd", null), new XElement("plist", new XAttribute("version", "1.0"), dict)).ToString() + "\n";
        }
        if (Platform == "linux") return "[Unit]\nDescription=SiriAI Harness Host\nAfter=network-online.target\n\n[Service]\nType=simple\nWorkingDirectory=" + UnitSettingQuote(Directory) +
            "\nExecStart=" + string.Join(" ", new[] { Runtime }.Concat(RunArguments).Select(UnitQuote)) +
            "\nRestart=on-failure\nRestartSec=5\nTimeoutStopSec=30\nUMask=0077\n\n[Install]\nWantedBy=default.target\n";
        XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
        XElement E(string name, object content) => new(ns + name, content);
        return "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" + new XDocument(new XDeclaration("1.0", "UTF-8", null), new XElement(ns + "Task", new XAttribute("version", "1.4"),
            E("Triggers", new XElement(ns + "LogonTrigger", E("Enabled", "true"), E("UserId", Principal))),
            E("Principals", new XElement(ns + "Principal", new XAttribute("id", "Author"), E("UserId", Principal), E("LogonType", "InteractiveToken"), E("RunLevel", "LeastPrivilege"))),
            E("Settings", new object[] { E("MultipleInstancesPolicy", "IgnoreNew"), E("DisallowStartIfOnBatteries", "false"), E("StopIfGoingOnBatteries", "false"), E("ExecutionTimeLimit", "PT0S"),
                E("RestartOnFailure", new object[] { E("Interval", "PT1M"), E("Count", 999) }) }),
            new XElement(ns + "Actions", new XAttribute("Context", "Author"), E("Exec", new object[] { E("Command", Runtime), E("Arguments", string.Join(" ", RunArguments.Select(WindowsQuote))), E("WorkingDirectory", Directory) })))).ToString() + "\n";
    }
    internal static string UnitSettingQuote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("%", "%%") + "\"";
    internal static string UnitQuote(string value) => UnitSettingQuote(value).Replace("$", "$$");
    internal static string WindowsQuote(string value)
    {
        var text = new StringBuilder("\""); int slashes = 0;
        foreach (char character in value)
        {
            if (character == '\\') { slashes++; continue; }
            if (character == '\"') text.Append('\\', slashes * 2 + 1).Append(character);
            else text.Append('\\', slashes).Append(character);
            slashes = 0;
        }
        return text.Append('\\', slashes * 2).Append('\"').ToString();
    }
    private static string ResolveExecutable(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) throw new ArgumentException("ACP services require a configured executable.");
        if (Path.IsPathFullyQualified(command)) return File.Exists(command) ? command : throw new FileNotFoundException("Configured ACP executable does not exist.");
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            foreach (string extension in OperatingSystem.IsWindows() ? new[] { "", ".exe", ".cmd", ".bat" } : new[] { "" })
            {
                string path = Path.Combine(folder, command + extension);
                if (File.Exists(path)) return Path.GetFullPath(path);
            }
        throw new FileNotFoundException("Configured ACP executable was not found on PATH.");
    }
    internal static void MakePrivateDirectory(string path)
    {
        System.IO.Directory.CreateDirectory(path);
        if (new DirectoryInfo(path).LinkTarget != null) throw new IOException("Service directory must not be a symlink.");
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        else
        {
            var security = new DirectorySecurity(); security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            new DirectoryInfo(path).SetAccessControl(security);
        }
    }
    internal static void WritePrivate(string path, byte[] bytes)
    {
        if (File.Exists(path) && new FileInfo(path).LinkTarget != null) throw new IOException("Service files must not be symlinks.");
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var output = new FileStream(temporary, options))
            { output.Write(bytes); output.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    internal static byte[] ReadPrivate(string path)
    {
        if (new FileInfo(path).LinkTarget != null) throw new IOException("Service files must not be symlinks.");
        if (new FileInfo(path).Length > 1024 * 1024) throw new InvalidDataException("Service file exceeds its budget.");
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute)) != 0)
            throw new UnauthorizedAccessException("Service files must be private to this user (mode 600).");
        return File.ReadAllBytes(path);
    }
}
