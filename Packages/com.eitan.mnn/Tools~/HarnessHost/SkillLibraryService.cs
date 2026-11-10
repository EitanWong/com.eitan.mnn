using System.Text;
using System.Runtime.Serialization;
using MNN.Samples.SiriAI.Harnesses;

namespace SiriAI.HarnessHost;

/// <summary>Administrative work reuses durable task admission/recovery, in a separate one-writer runtime.</summary>
internal sealed partial class SkillLibraryService : IDisposable
{
    private readonly HostConfiguration _configuration;
    private readonly FileTaskStore _store;
    private readonly object _admission = new();
    private readonly PersistentTaskRuntime _runtime;
    private readonly CancellationTokenSource _processStopping = new();
    private int _disposed;
    internal SkillLibraryService(HostConfiguration configuration)
    {
        _configuration = configuration;
        if (configuration.Skills == null) return;
        _store = new FileTaskStore(Path.Combine(configuration.DataDirectory, "skills-library"));
        try { _runtime = new PersistentTaskRuntime(_store, new[] { new SkillsHarnessAdapter(configuration, _processStopping.Token) }, 1); }
        catch { _store.Dispose(); _processStopping.Dispose(); throw; }
    }
    private bool Allowed(string owner) => _configuration.Skills?.AllowedOwners.Contains(owner, StringComparer.Ordinal) == true;
    private void RequireOwner(string owner)
    {
        if (_runtime == null) throw new NotSupportedException("Skill management is not enabled on this host.");
        if (!Allowed(owner)) throw new UnauthorizedAccessException("Skill management is not authorized for this owner.");
    }
    internal HarnessSkillCatalog Catalog(string owner) => new()
    {
        Enabled = Allowed(owner), Package = Allowed(owner) ? _configuration.Skills.Package : null,
        Targets = Targets(), DefaultTargets = Allowed(owner) ? DefaultTargets(_configuration) : []
    };
    internal static HarnessSkillTarget[] Targets() => BackendProfiles.All.Where(p => p.SkillsInstaller != null)
        .Select(p => new HarnessSkillTarget { Backend = p.Id, Installer = p.SkillsInstaller }).ToArray();
    internal static string[] DefaultTargets(HostConfiguration configuration)
    {
        var present = BackendDiscovery.Scan().Where(item => item.Installed).Select(item => item.Backend)
            .Concat(configuration.Harnesses.Select(item => item.Backend)).ToHashSet(StringComparer.Ordinal);
        var selected = Targets().Where(p => present.Contains(p.Backend)).Select(p => p.Installer).Distinct().ToArray();
        return selected.Length > 0 ? selected : Targets().Select(p => p.Installer).ToArray();
    }
    internal static string[] ExplicitTargets(HarnessSkillRequest request)
    {
        var targets = Targets().ToDictionary(p => p.Backend, p => p.Installer, StringComparer.Ordinal);
        return request.Backends.Select(backend => targets.TryGetValue(backend, out var installer) ? installer :
            throw new NotSupportedException("This backend has no skills.sh installer: " + backend)).Distinct(StringComparer.Ordinal).ToArray();
    }
    internal HarnessTask Submit(string owner, HarnessSkillRequest request) => SubmitCore(owner, request, null);
    private HarnessTask SubmitCore(string owner, HarnessSkillRequest request, SkillStartupIntent startup)
    {
        RequireOwner(owner); request.Validate(); ExplicitTargets(request);
        lock (_admission)
        {
            var prior = _runtime.List(owner).FirstOrDefault(t => t.SubmissionKey == request.SubmissionKey);
            if (prior != null)
            {
                var accepted = TaskHostJson.Read<SkillLibraryPlan>(Encoding.UTF8.GetBytes(prior.Objective));
                if ((accepted.Startup == null) != (startup == null) || !TaskHostJson.Write(accepted.Request).SequenceEqual(TaskHostJson.Write(request)))
                    throw new ArgumentException("An invocation identity cannot be reused for a different skill operation.");
                return prior;
            }
            var plan = new SkillLibraryPlan { Request = request, Startup = startup, Fingerprint = SkillLibraryPlan.Identity(_configuration.Skills),
                Installers = request.Operation == "install" ? request.Backends.Length > 0 ? ExplicitTargets(request) : DefaultTargets(_configuration) : [] };
            return _runtime.Submit(new TaskSubmission(owner, "skill-library", request.SubmissionKey, "skills", "host",
                Encoding.UTF8.GetString(TaskHostJson.Write(plan))));
        }
    }
    internal HarnessTask[] List(string owner) { RequireOwner(owner); return _runtime.List(owner); }
    internal HarnessTask Get(string owner, string id) { RequireOwner(owner); return _runtime.Get(id, owner); }
    internal async Task<HarnessTask> CancelAsync(string owner, string id, CancellationToken cancellation)
    { RequireOwner(owner); await _runtime.CancelAsync(id, owner, cancellation); return _runtime.Get(id, owner); }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _processStopping.Cancel(); _runtime?.Dispose(); _store?.Dispose(); _processStopping.Dispose();
    }
}

[DataContract]
internal sealed class SkillLibraryPlan
{
    [DataMember] public HarnessSkillRequest Request;
    [DataMember] public string Fingerprint;
    [DataMember] public string[] Installers;
    [DataMember] public SkillStartupIntent Startup;
    internal static string Identity(SkillsConfiguration c) => MspWire.Hash(new
    {
        c.Command, c.Arguments, c.Package, c.WorkingDirectory, c.TimeoutMilliseconds,
        Owners = c.AllowedOwners.OrderBy(x => x, StringComparer.Ordinal).ToArray(),
        Environment = c.ForwardEnvironment.OrderBy(x => x, StringComparer.Ordinal)
            .Select(name => new { Name = name, Value = Environment.GetEnvironmentVariable(name) }).ToArray()
    });
}
