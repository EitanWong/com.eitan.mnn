namespace SiriAI.HarnessHost;

internal static class BackendProcessEnvironment
{
    internal static readonly string[] SystemNames =
    [
        "APPDATA", "COLORTERM", "COMSPEC", "ComSpec", "HOME", "HOMEDRIVE", "HOMEPATH", "HTTP_PROXY", "HTTPS_PROXY", "LANG",
        "LC_ALL", "LOCALAPPDATA", "LOGNAME", "NODE_EXTRA_CA_CERTS", "NO_BROWSER", "NO_PROXY", "PATH", "PATHEXT", "PWD", "SHELL",
        "SSL_CERT_DIR", "SSL_CERT_FILE", "SYSTEMDRIVE", "SYSTEMROOT", "SystemRoot", "TEMP", "TERM", "TMP", "TMPDIR", "USER",
        "USERDOMAIN", "USERNAME", "USERPROFILE", "WINDIR", "XDG_CACHE_HOME", "XDG_CONFIG_HOME", "XDG_DATA_HOME", "XDG_RUNTIME_DIR", "XDG_STATE_HOME",
        "http_proxy", "https_proxy", "no_proxy"
    ];
    internal static string[] AvailableSystemNames() => Environment.GetEnvironmentVariables().Keys.Cast<string>()
        .Where(name => SystemNames.Contains(name) || name.StartsWith("LC_", StringComparison.Ordinal) || name.StartsWith("npm_config_", StringComparison.Ordinal) || name.StartsWith("NPM_CONFIG_", StringComparison.Ordinal))
        .Concat(SystemNames).Distinct(StringComparer.Ordinal).ToArray();
}
