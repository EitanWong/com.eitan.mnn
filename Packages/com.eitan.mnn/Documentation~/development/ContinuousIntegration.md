# CI and validation

## Automatic checks

`Repository checks` runs on every pull request targeting `dev` or `main`, every
push to those branches, and manual dispatch. It has read-only repository access,
bounded runtimes and concurrency cancellation. It requires no Unity license or model downloads.

| Check | What it verifies |
| --- | --- |
| Package and repository | Python tool tests; package layout, assembly ownership and meta/GUIDs; tracked-asset exclusions; manifest dependencies; tracked local Markdown links; commit whitespace |
| Markdown | README, changelog, GitHub guidance and all `Documentation~` pages |
| Workflows | GitHub Actions syntax, expressions and embedded shell/Python snippets with actionlint |

The workflow uses Python 3.11, actionlint 1.7.12 and the pinned markdownlint-cli2
GitHub Action. Local `markdownlint-cli2` uses `.markdownlint-cli2.jsonc`; line length
is unrestricted to accommodate Chinese text, tables and URLs. Separate sections
may reuse heading names. Existing inline HTML is permitted.

Third-party action references use commit SHAs. Dependabot opens weekly action
updates against `dev`. Review the action diff and runtime requirements before merging.

## Unity native validation

`Unity native validation` is manual and accepts trusted `dev` or `main` commits.
It runs on a dedicated self-hosted macOS ARM64 runner with labels `macOS`, `ARM64`
and `mnn-unity`. Configure an activated Unity 2021.3.45f2 installation, Python 3.11+
and the `MNN_UNITY_EXECUTABLE` Actions variable pointing to the Unity executable.
Use the `unity-validation` environment to restrict who can start privileged runs.
Do not attach this runner to jobs executing untrusted fork pull requests.

The workflow creates an isolated project under `TestArtifacts~/CI`, embeds the
committed package and enables the package's tests. Unity may resolve its declared
UPM dependencies; no model weights are downloaded. Run `contract` for platform
policy checks or `affine` for policy checks plus the committed deterministic graph.
Missing or skipped required affine tests fail the existing runner.

Logs, NUnit XML and the scope summary are uploaded even when the test command
fails, with 14-day retention. A green static workflow is not native inference
coverage, and a successful affine run does not validate external models or all devices.

## Maintainer settings

Protect `main` with pull-request review and require these checks after they have
run at least once: `Package and repository`, `Markdown`, and `Workflows`. Restrict
force pushes and configure reviewers for the `release` and `unity-validation`
environments. These are GitHub repository settings; adding workflow files alone
does not enable branch protection or environment approval.

## Known clean-build blocker

A local run of the new isolated project against the existing dev snapshot failed
before tests: OmniVoice references `MNNReferenceAudio` and `MNNBytePairTokenizer`,
which are absent from the committed tree. It also expects graph APIs from the
uncommitted runtime work. This infrastructure change does not publish that separate
runtime work or certify the package as buildable.

Resolve the dependency closure and rerun the affine workflow before marking the
dev-to-main promotion ready to merge. Keep the PR in draft while this blocker remains.
