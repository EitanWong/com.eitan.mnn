using System.Text.Json;
using DotCraft.Configuration;
using DotCraft.Harness;
using DotCraft.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Independent test-only administrator: grant the SDK's accepted fingerprint via its public API.
var config = new AppConfig { GlobalConfigPath = args[1] };
config.Plugins.PluginRoots = [args[0]];
config.Plugins.DisableDefaultPluginRegistry = true;
Directory.CreateDirectory(args[2]);
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [], DisableDefaults = true });
builder.Logging.ClearProviders();
builder.Services.AddDotCraftHarness(config, options =>
{
    options.WorkspacePath = args[2]; options.DataPath = ".fixture-grant"; options.UserDataPath = null;
});
using var host = builder.Build();
await host.StartAsync();
var coordinator = host.Services.GetRequiredService<IPluginDotnetRuntimeCoordinator>();
var result = await coordinator.TrustAsync("siriai.fixture");
Console.WriteLine(JsonSerializer.Serialize(new { result, coordinator.Snapshot }));
await host.StopAsync();
return result.Outcome == PluginRuntimeMutationOutcome.NotApplied ? 1 : 0;
