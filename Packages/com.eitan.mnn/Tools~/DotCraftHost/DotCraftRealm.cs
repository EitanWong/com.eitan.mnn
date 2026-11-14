using DotCraft.Configuration;
using DotCraft.Harness;
using DotCraft.Sessions;

namespace SiriAI.HarnessHost;

/// <summary>One native runtime owns one owner/workspace's mutable agent state.</summary>
internal sealed class DotCraftRealm : IAsyncDisposable
{
    private readonly IHost _host;
    internal ISessionService Sessions => _host.Services.GetRequiredService<ISessionService>();
    private DotCraftRealm(IHost host) => _host = host;

    internal static async Task<DotCraftRealm> StartAsync(HarnessConfiguration config, string workspace, string dataName, string key, CancellationToken stopping)
    {
        const string provider = "siriai-model";
        var app = new AppConfig { ProviderId = provider };
        // The authority is read by the official trust store; no workspace/global config is merged.
        app.GlobalConfigPath = config.DotCraftPluginAuthorityConfig;
        app.Plugins.PluginRoots = config.DotCraftPluginRoots.ToList();
        app.Plugins.DisabledPlugins = config.DotCraftDisabledPlugins.ToList();
        app.Plugins.DisableDefaultPluginRegistry = true;
        DotCraftPluginConfiguration.RequireUnchanged(config);
        app.Providers[provider] = new AppConfig.ModelProviderConfig
        {
            DisplayName = "SiriAI configured model", Protocol = config.DotCraftModelProtocol,
            ApiKey = key, EndPoint = config.Endpoint, StreamMaxRetries = 0
        };
        app.ProviderPreferences[provider] = new ModelPreference { Model = config.Model };
        app.EnabledTools = config.DotCraftEnabledTools.ToList();
        app.McpServers = DotCraftMcpConfiguration.Servers(config, workspace);
        app.Tools.CodeMode.Mode = config.DotCraftCodeMode switch
        {
            "on" => AppConfig.CodeModeSetting.On,
            "only" => AppConfig.CodeModeSetting.Only,
            _ => AppConfig.CodeModeSetting.Off
        };
        app.Tools.DeferredLoading.Strategy = AppConfig.DeferredLoadingStrategy.Off;
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = [], DisableDefaults = true });
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IHostLifetime, EmbeddedLifetime>();
        builder.Services.AddSingleton<DotCraft.Processes.IManagedChildProcessFactory, DotCraftWorkerProcessFactory>();
        builder.Services.AddDotCraftHarness(app, options =>
        {
            options.WorkspacePath = workspace;
            options.DataPath = dataName;
            options.UserDataPath = null;
            if (config.DotCraftCodeMode != "off") options.CodeModeWorkerHost = "SiriAI.DotCraftHost";
        });
        var host = builder.Build();
        try
        {
            await host.StartAsync(stopping);
            DotCraftPluginConfiguration.RequireReady(host.Services, config);
            await DotCraftMcpConfiguration.RequireReadyAsync(host.Services, app.McpServers, stopping);
            return new DotCraftRealm(host);
        }
        catch { await ShutdownHostAsync(host); throw; }
    }

    public ValueTask DisposeAsync() => ShutdownHostAsync(_host);
    private static async ValueTask ShutdownHostAsync(IHost host)
    {
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await host.StopAsync(stopping.Token); }
        finally
        {
            if (host is IAsyncDisposable disposable) await disposable.DisposeAsync();
            else host.Dispose();
        }
    }
    private sealed class EmbeddedLifetime : IHostLifetime
    {
        public Task WaitForStartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
