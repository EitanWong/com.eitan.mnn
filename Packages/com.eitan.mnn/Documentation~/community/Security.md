# Security policy

Security fixes target the current reviewed code on `main`; older development
snapshots do not have a separate maintenance commitment. Native inference crosses
an unmanaged boundary. Load models from trusted sources and review their licenses
and upstream security updates.

## Reporting

Do not disclose credentials, private model content or exploitable details in a
public issue. If GitHub's **Report a vulnerability** option is enabled on this
repository's Security page, use it for a private report. Otherwise, open an issue
requesting a private contact channel without including vulnerability details.
Private vulnerability reporting must be enabled in GitHub settings before that
option becomes available.

Include the affected commit, platform/compiler, minimal reproduction, impact and
any proposed fix. Keep sample inputs small and redistributable. Do not attach
model weights or full memory dumps. Maintainers will coordinate triage and disclosure;
no fixed response or remediation deadline is promised.
