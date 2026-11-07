# Shared package regression and cloud acceptance

`run_shared_managed_tests.py` creates a private package-only Unity project and runs
the complete offline scope of `MNN.Unity.Tests`: every local Editor test,
including its native inference tests, and all local explicit tests. Its seven
external network tests have a separate scope. There is no user case/category
filter option. SiriAI, EasyMic and APM are absent. The separate native/model integration
assembly and Player inference assembly remain the responsibility of
`run_platform_tests.py` and the model runners; this gate does not claim native
inference or Player coverage.
For a complete large-model Player scope, `run_platform_tests.py
--run-timeout-seconds 7200` sets an explicit total build/run deadline. Its default
remains 1800 seconds; this is separate from `--player-heartbeat-timeout`.
Choose the total budget before launch. Extending it does not relax model
assertions or turn elapsed time into a performance pass.
The complete Editor assembly also contains texture and Studio inference tests.
Run it with `--graphics` on a supported graphics host, using the required existing
native binaries and model/reference assets. The workflow enables graphics.

```sh
python3 -B Packages/com.eitan.mnn/Tools~/Testing/run_shared_managed_tests.py \
  --unity /path/to/licensed/Unity \
  --graphics --scope offline \
  --output TestArtifacts~/SharedManagedTests \
  --models-root /path/to/existing/Assets/StreamingAssets/MNN/Models \
  --reference-environment /path/to/nonsecret-reference-environment.json
```

The model link reuses existing deployed assets. It never copies or downloads
weights. Use a fresh output directory to change its target. The reference file is
a JSON object containing only nonsecret `MNN_TEST_*` model/reference settings.
Those values supplement the inherited process environment. Several existing
packaging/reference tests in this assembly need deployed models, even though
they do not perform native inference. Missing assets remain visible failures.

The result gate rejects nonzero exits, missing XML, inconsistent counters, empty
runs, unexpected assemblies, incomplete scope discovery counts, failed cases,
unexpected skips and inconclusive cases. It requires the migration's main
fixtures to execute. Unity retains the seven unselected network methods as XML
leaves. The complete assembly discovery count must equal all reported leaves;
each of those seven names must be present with result `Skipped`, label `Explicit`
and runstate `Explicit`. The report lists them as `not_selected_network_cases`.
An executed network case, a different skip state or a missing declared name
blocks the offline gate. Their full names and source category declarations are pinned. New network classifications
and changes to the full shared explicit-test inventory block the source preflight
until reviewed. A successful report includes a source digest. It is a complete
offline result, never a claim that every test in the assembly passed.
The four existing imported-sample menu cases (three BasicExample and one SiriAI)
are also outside the package-only contract when their exact reason says that the
sample is absent. Their complete parameterized ids and reason are pinned; they
are reported separately. A failed imported-sample assertion is never exempted.
Allocation-counter skips, unsupported Omni speech and missing local references
remain failures of this gate, listed separately in `unresolved_cases`. They have
no automatic exemption. A separate host Mono measurement cannot clear these
Unity runtime gaps, even when the same original allocation tests pass there.

Unity Test Framework 1.1.33 constructs its assembly selection using NUnit
`AssemblyNameFilter`, which matches each leaf and explicitly selects all local
`[Explicit]` methods. An ordinary negative category filter stops explicit
selection for local methods too. The offline runner therefore uses one fixed
positive full-name expression that matches every name except the seven exact
network names. It keeps all 15 existing local explicit methods, including the
five native speech methods, six Studio LLM methods, two Studio timing methods,
catalog-cache benchmark and local catalog classification audit. This contract
was checked against the installed NUnit runner. No local explicit method may
remain unselected or ignored.

The seven `[Category("RequiresNetwork")]` methods are two live model-hub catalog
audits, one real CDN file transfer and four complete ModelScope LLM transfers.
Their original assertions remain intact. Run their independent scope only when
live external discovery and downloads are intended:

```sh
python3 -B Packages/com.eitan.mnn/Tools~/Testing/run_shared_managed_tests.py \
  --unity /path/to/licensed/Unity --scope network \
  --output TestArtifacts~/SharedNetworkTests
```

This scope positively selects the declared network category and requires exactly
those seven cases in its result. Its four model-download destinations are
private paths under the isolated project's `TestArtifacts~/NetworkDownloads`.
It never changes the existing local-inference model inputs into download targets.
The offline scope reuses existing deployed assets and does not run these transfers.
`suite-inventory.json` inventories every package `[Explicit]` declaration with
its assembly, source location and environment names, including the separate
integration and Player assemblies. An explicit attribute is an execution choice,
not an assumption that a test uses no network or needs no local resources.

`--prepare-only` prepares without starting Unity or claiming a pass.
`--check-results /path/to/results.xml --unity-exit-code N` checks saved evidence
without starting Unity. This inspection does not establish that old evidence was
produced from current sources; normal execution deletes stale XML before launch.

The `Offline shared regression` workflow runs this gate on trusted `dev`/`main`
pushes and manual dispatch. It uses the existing licensed `mnn-unity` runner and
`unity-validation` environment. Configure these repository variables:

| Variable | Purpose |
| --- | --- |
| `MNN_UNITY_EXECUTABLE` | Activated Unity executable |
| `MNN_TEST_MODELS_ROOT` | Existing deployed Models directory on that host |
| `MNN_TEST_REFERENCE_ENVIRONMENT` | Nonsecret reference environment JSON on that host |

The workflow uploads XML, logs, source digest and tool inventory even after a
failure. Branch protection and runner provisioning are repository settings.
Its separate `Required quality tool prerequisites` job waits until the full
regression job has run and uploaded evidence, then runs even if regression failed.
It requires `crapper`, `dryer`, `mutator` and the Unity coverage package. A missing
tool makes that job and the workflow fail; unconfigured tools cannot produce a
green quality prerequisite check. This does not prevent the complete Unity suite
from executing or its evidence from uploading. Require both job checks in branch
protection when provisioning the continuous quality gate.

## Quality metric prerequisites

The original eight allocation/callback/layout tests also have a separate host
Mono gate for runtimes where Unity's allocation counter is unavailable:

```sh
python3 -B Packages/com.eitan.mnn/Tools~/Testing/run_mono_allocation_tests.py \
  --unity /path/to/installed/Unity --output TestArtifacts~/HostMonoAllocation
```

It compiles the actual package assemblies using `check_assembly_compilation.py`,
then executes the eight exact original NUnit cases using `NUnitTestAssemblyRunner`
under an existing host `mono`/`csc` installation. An existing complete compiler
output can be supplied with `--compiled-assemblies`. Missing tests, counters that
skip, failures or inconclusive results block this separate gate. The report hashes
the actual test assembly and states `unity_allocation_verified: false`.
`--unity-project /path/to/already/compiled/project` instead reuses that project's
actual `Library/ScriptAssemblies` and resolved NUnit references. CI uses the
isolated project from its complete offline run, so it does not require a second
dependency cache in the checkout root or a second Unity launch.
The workflow runs this step after uploading Unity evidence, even if Unity failed,
and uploads its results separately. It never clears Unity's allocation skips or
claims that host Mono measurements establish Unity/IL2CPP/device/native allocation.

```sh
python3 -B Packages/com.eitan.mnn/Tools~/Testing/probe_quality_tools.py \
  --require crapper dryer mutator unity-coverage \
  --output TestArtifacts~/QualityTools/required.json
```

This command fails if any requested tool is missing. It never installs or runs a
tool, and its report explicitly says that quality metrics were not verified.
The inventory also detects common coverage, duplication and mutation alternatives.
Tool presence, successful compilation and functional tests do not establish
CRAP below 10, duplication rank below 0.8, coverage percentage or mutation score.
These metrics require provisioned tools and their real reports.

## Physical Unity audio acceptance

`MNNUnityAudioAcceptanceTests.cs` is an opt-in PlayMode fixture for an isolated
project on a host with a real microphone, working output route and microphone
permission. It emits a quiet 100 ms tone per cycle. Copy it from this tooling
directory into `Assets/MNNAudioAcceptance/`, with a test assembly named
`MNN.Unity.AudioAcceptance.Tests`, referencing `MNN.Unity` and
`optionalUnityReferences: ["TestAssemblies"]`. Do not include the fixture in the
application's shipping assembly. Run the exact fixture with graphics/audio enabled:

```sh
/path/to/licensed/Unity -batchmode -projectPath /absolute/isolated/project \
  -runTests -testPlatform PlayMode \
  -testFilter MNN.Unity.AudioAcceptance.MNNUnityAudioAcceptanceTests \
  -testResults /absolute/TestArtifacts~/HardwareAudio/results.xml \
  -logFile /absolute/TestArtifacts~/HardwareAudio/unity.log
```

The fixture opens, negotiates, captures finite PCM, renders output, waits for
estimated presentation receipts and closes twice. Missing hardware, permission
failure, a stopped DSP callback or incomplete cleanup fails the test. It never
turns those conditions into a passing skip. It validates the shared device path,
including DSP-budget reads before output binding; it does not establish speaker
perception, ASR accuracy, acoustic AEC or native continuous-audio inference.

`MNNStandaloneHardwareAcceptanceTests.cs` additionally exercises the imported
Basic Inference sample's actual `MNNStandaloneConversationExample` component.
Import the sample into the isolated project and add the
`MNN.Unity.Samples.BasicExample` reference to the acceptance test assembly above.
Copy this fixture into that assembly and configure three existing local directories
using `MNN_STANDALONE_MODEL`, `MNN_STANDALONE_ASR` and `MNN_STANDALONE_TTS`.
`MNN_STANDALONE_MICROPHONE` optionally selects an exact name advertised by
`Microphone.devices`; the fixture rejects an unavailable selection.
Run the same PlayMode command with filter
`MNN.Unity.AudioAcceptance.MNNStandaloneHardwareAcceptanceTests`.

This calls the component's public UI commands, records at least half a second of
actual microphone frames, drains capture and completes local ASR without adding
the draft to the visible conversation. It then generates local text/TTS, observes
finite nonzero output in both the response's post-filter waveform and Unity's
actual listener mix, waits for presentation and
checks the history consumed by the built-in GUI. It closes and repeats with a
fresh application owner. The GUI component stays enabled; pixel layout and button
hit testing are not asserted. Uncontrolled microphone content is not an accuracy
oracle, and estimated presentation receipts do not establish perceived sound or
native duplex. Device startup, real inference and lifecycle failures remain
failures, including on machines where the smaller device-only fixture passed.

Completed standalone runs also support a latency distribution report:

```sh
python3 -B Packages/com.eitan.mnn/Tools~/Testing/summarize_standalone_acceptance.py \
  TestArtifacts~/Standalone/CompletedRun
```

It requires a completed passing measurement run, complete sequential CSV rows
and matching source counters. It calculates nearest-rank p50/p95/p99 for actual
first-token, first-PCM and turn latency, counting startup once per session.
Simulated render window percentiles stay separate. Reports hash both inputs,
omit transcript text and do not assert performance targets. Sampled native-only,
GPU and interrupt measurements remain outside this tool's scope.

## Cloud service prerequisites and actual C# requests

Copy `cloud_acceptance.example.json` into `TestArtifacts~/` and replace its model,
voice, endpoint and reference placeholders with the deployed service's values.
Keep credentials in named environment variables; never put them in the JSON.
ASR needs an existing PCM16 mono WAV, up to 30 seconds, and its independent text
reference. An existing service `/models` JSON or list of model ids can block
unknown models before any request:

```sh
python3 -B Packages/com.eitan.mnn/Tools~/Testing/preflight_cloud_acceptance.py \
  --config TestArtifacts~/CloudAcceptance/plan.json \
  --models TestArtifacts~/CloudAcceptance/model-ids.json
```

Missing credentials, missing reference files, placeholder/unknown models,
incompatible protocols, unsafe endpoint URLs and invalid time budgets fail the
preflight. Reports contain credential source names and presence only. A ready
preflight makes no requests and always records `acceptance_verified: false`.
It does not infer ASR/TTS support from a working chat endpoint.

`run_cloud_acceptance.py` first applies that same preflight. By default it only
prepares a separate Unity project and copies the real C# fixture. `--execute`
performs the plan's bounded real service requests through package APIs:

```sh
python3 -B Packages/com.eitan.mnn/Tools~/Testing/run_cloud_acceptance.py \
  --config TestArtifacts~/CloudAcceptance/plan.json \
  --models TestArtifacts~/CloudAcceptance/model-ids.json \
  --output TestArtifacts~/CloudAcceptance/LiveRun \
  --unity /path/to/licensed/Unity --execute
```

The fixture assembly is `MNN.Unity.CloudAcceptance.Tests`, with fixture
`MNN.Unity.CloudAcceptance.MNNCloudAcceptanceTests`. Chat and OpenAI ASR call
`MNNOpenAIConversationProvider`; task-specific recognition and synthesis call
`MNNCloudSpeechProvider`. Python does not reimplement the service protocols.
The old Unity NUnit runner receives a synchronous test method that joins the
async provider work on a worker task.

Chat/ASR compare the normalized response with the independent expected text.
Chat also requires nonempty deltas. Synthesis checks bounded, finite, non-silent
PCM at the provider's 24 kHz rate. All scenarios enforce the configured latency
budget. Logs contain counts/timings rather than transcripts, credentials or
provider bodies. A real unsupported service/model fails; it is never converted
into a passing skip. Audio waveform validity does not establish perceptual speech
quality or recognition accuracy beyond the supplied reference.
