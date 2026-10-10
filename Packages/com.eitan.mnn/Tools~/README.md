# Package tools

Unity ignores `Tools~`; these scripts do not compile into Runtime, Editor or test assemblies.
Run commands from the development repository root. No tool automatically downloads models or dependencies.

| Tool | Purpose | Requirements |
| --- | --- | --- |
| `Validation/validate_package.py` | Layout, assembly ownership, meta/GUID and documentation checks | Python 3.9+ standard library |
| `Testing/check_platform_compilation.py` | 13 conditional C# compilation/import configurations | Installed macOS Unity .NET/Roslyn and `monodis` |
| `Testing/prepare_multimodal_fixtures.py` | Small local color images and WAV input | Python 3.9+, macOS `say`/`afconvert` and installed voice |
| `Testing/run_model_tests.py` | Six representative models, strict offline EditMode run | Python 3.9+, existing isolated Unity project, downloaded models and local fixtures |
| `Testing/run_platform_tests.py` | PlayMode/Player inference or platform rejection policy | Python 3.9+, existing isolated Unity project and target build support |
| `Testing/PlatformValidationSetup.cs` | Player launch/build configuration helper copied by the runner | Isolated Unity Editor only |

Use `python3 <tool-path> --help` for arguments. The repository-relative output root is `TestArtifacts~/`.
Sync the current package into an existing isolated project before running Unity tests; do not launch a second
Unity instance against an open project. A structural check or conditional compile is not a native-link,
device or model-inference test. A contract-only run verifies the supported-platform guard.

The representative model runner filters the six named fixtures as well as their category, so later
generation/Studio fixtures sharing `RepresentativeModel` do not silently expand this validation scope.
The helper class and assembly names remain stable when source files move.

See [project structure](../Documentation~/ProjectStructure.md) and [test evidence and commands](../Documentation~/Testing.md).
