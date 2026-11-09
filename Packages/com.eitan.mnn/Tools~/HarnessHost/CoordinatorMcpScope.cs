using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SiriAI.HarnessHost;

[JsonConverter(typeof(JsonStringEnumConverter<CoordinatorConversationBinding>))]
internal enum CoordinatorConversationBinding { Fixed, ParentTask }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class CoordinatorMcpScope
{
    public string Id { get; set; }
    public string OwnerId { get; set; }
    public string ConversationId { get; set; }
    public CoordinatorConversationBinding ConversationBinding { get; set; }
    public string WorkspaceKey { get; set; }
    public string HarnessId { get; set; }
    public string TokenEnvironmentVariable { get; set; }
    internal string Workspace { get; private set; }
    internal string Prefix { get; private set; }
    internal byte[] Digest { get; private set; }
    internal string CoordinatorHarnessId { get; set; }
    internal SemaphoreSlim Tools { get; } = new(1, 1);
    internal bool Authenticate(string token) => CryptographicOperations.FixedTimeEquals(Digest, SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    internal bool BindsParent => ConversationBinding == CoordinatorConversationBinding.ParentTask;
    internal string PrefixFor(string conversation) => BindsParent ? Prefix + MspWire.Hash(new { ConversationId = conversation }) + ":" : Prefix;
    private void ValidateConversationBinding()
    {
        if (!Enum.IsDefined(ConversationBinding)) throw new ArgumentException("Unknown coordinator conversation binding.");
        if (BindsParent)
        {
            if (ConversationId != null) throw new ArgumentException("ParentTask binding must not also configure a fixed ConversationId.");
        }
        else if (string.IsNullOrWhiteSpace(ConversationId) || ConversationId.Length > 128 || ConversationId.Any(char.IsControl))
            throw new ArgumentException("Fixed coordinator binding requires a valid ConversationId.");
    }
    internal static void Resolve(HostConfiguration configuration)
    {
        var scopes = configuration.CoordinatorMcpScopes;
        if (scopes == null || scopes.Length > 64 || scopes.Any(s => s == null)) throw new ArgumentException("Invalid coordinator MCP scope list.");
        if (scopes.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != scopes.Length) throw new ArgumentException("Coordinator MCP scope ids must be unique.");
        var tokens = configuration.Identities.Select(i => i.Secret).ToHashSet(StringComparer.Ordinal);
        foreach (var scope in scopes)
        {
            foreach (var value in new[] { scope.Id, scope.OwnerId, scope.WorkspaceKey, scope.HarnessId })
                if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(char.IsControl)) throw new ArgumentException("Invalid coordinator MCP scope identity.");
            scope.ValidateConversationBinding();
            if (!configuration.Identities.Any(i => i.OwnerId == scope.OwnerId) || !configuration.Harnesses.Any(h => h.Id == scope.HarnessId) ||
                !configuration.Workspaces.TryGetValue(scope.WorkspaceKey, out string workspace)) throw new ArgumentException("Coordinator MCP scope requires configured owner, harness and workspace.");
            if (scope.TokenEnvironmentVariable == null || !Regex.IsMatch(scope.TokenEnvironmentVariable, @"\A[A-Za-z_][A-Za-z0-9_]{0,127}\z"))
                throw new ArgumentException("Invalid coordinator MCP credential reference.");
            string token = Environment.GetEnvironmentVariable(scope.TokenEnvironmentVariable);
            if (token == null || token.Length < 32 || token.Length > 4096 || !tokens.Add(token)) throw new ArgumentException("Coordinator MCP scopes need distinct credentials separate from Host authentication.");
            scope.Workspace = workspace;
            scope.Digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            // Preserve fixed-scope durable identities from earlier Host versions exactly.
            scope.Prefix = "mcp:" + (scope.BindsParent
                ? MspWire.Hash(new { scope.Id, scope.OwnerId, scope.ConversationBinding, Workspace = workspace, scope.HarnessId })
                : MspWire.Hash(new { scope.Id, scope.OwnerId, scope.ConversationId, Workspace = workspace, scope.HarnessId })) + ":";
        }
    }
}
