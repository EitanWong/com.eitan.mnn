using System.Text.Json;

namespace SiriAI.HarnessHost;

internal static class HostServiceCli
{
    internal static string ActiveLabel { get; private set; }
    internal static async Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            var options = Parse(args);
            string command = args[1];
            string directory = Required(options, "--directory");
            if (command == "prepare")
            {
                var registration = HostServiceRegistration.Prepare(Required(options, "--id"), Required(options, "--config"),
                    Required(options, "--runtime"), directory, Get(options, "--platform") ?? HostServiceRegistration.CurrentPlatform,
                    options.TryGetValue("--environment", out var names) ? names.ToArray() : []);
                Console.WriteLine(JsonSerializer.Serialize(new { registration.Label, registration.Platform, registration.Directory, Definition = registration.DefinitionPath }, HostServiceRegistration.Json));
                return 0;
            }
            using var operation = HostServiceRegistration.AcquireLock(directory);
            var current = HostServiceRegistration.Load(directory);
            var manager = new HostServiceManager(current);
            ServiceStatus result = command switch
            {
                "install" => await manager.StartAsync(true), "start" => await manager.StartAsync(false),
                "stop" => await manager.StopAsync(false), "uninstall" => await manager.StopAsync(true),
                "status" => await manager.StatusAsync(),
                _ => throw new ArgumentException("Service command must be prepare, install, start, stop, status, uninstall or run.")
            };
            Console.WriteLine(JsonSerializer.Serialize(result, HostServiceRegistration.Json));
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine("Service error: " + error.Message); return 2; }
    }
    internal static string[] RunArguments(string[] args)
    {
        var options = Parse(args);
        var registration = HostServiceRegistration.Load(Required(options, "--directory"));
        if (registration.Platform != HostServiceRegistration.CurrentPlatform) throw new PlatformNotSupportedException("The registration targets another platform.");
        HostConfiguration.ApplyEnvironment(registration);
        ActiveLabel = registration.Label;
        return ["--config", registration.ConfigPath];
    }
    private static Dictionary<string, List<string>> Parse(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("Specify a service command.");
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var allowed = args[1] == "prepare" ? new[] { "--id", "--config", "--runtime", "--directory", "--platform", "--environment" } : new[] { "--directory" };
        for (int i = 2; i < args.Length; i += 2)
        {
            string key = args[i];
            if (!allowed.Contains(key) || i + 1 >= args.Length) throw new ArgumentException("Invalid service option: " + key);
            if (!values.TryGetValue(key, out var entries)) values[key] = entries = [];
            if (entries.Count > 0 && key != "--environment") throw new ArgumentException("Duplicate service option: " + key);
            entries.Add(args[i + 1]);
        }
        return values;
    }
    private static string Get(Dictionary<string, List<string>> values, string key) => values.TryGetValue(key, out var entries) ? entries.Single() : null;
    private static string Required(Dictionary<string, List<string>> values, string key) => Get(values, key) ?? throw new ArgumentException("Missing service option: " + key);
}
