# Project conventions

- Do not introduce native bridge libraries or custom C ABI exports, including
  custom exports compiled into MNN itself. The user requires calling official
  MNN C++ APIs directly and clarified that no additional C++ code should be
  written. Managed C# interop and marshalling are allowed. Do not modify official
  MNN source code. The current Native~ bindings and MNN_* P/Invoke exports
  predate this stricter requirement and do not satisfy it; do not extend them or
  describe their test results as validation of direct official C++ calls.
- C# calls to C++ symbols require an explicitly supported compiler/platform ABI
  contract. Do not present hand-written STL layouts, private object offsets, or
  virtual table assumptions as a portable, officially supported MNN C# API.
- Organize native C# declarations by responsibility or inference task using
  `MNNInterop.*.cs` partial files. Keep library names and calling conventions
  shared, safe handles under `Runtime/Interop/Handles`, and managed APIs in
  `Runtime/Core`. Add task-specific files only for implemented native exports.
- Keep build artifacts, test logs, and temporary diagnostics within this project
  under `TestArtifacts~/`. Do not download models or dependencies unnecessarily
  or scatter files elsewhere.
- Follow `Packages/com.eitan.mnn/Documentation~/ProjectStructure.md` for directory
  ownership and assembly boundaries. Put managed API code in the corresponding
  `Runtime/Core` domain and MonoBehaviours in `Runtime/Components`. Keep Editor
  and Player tests under `Tests/Editor` and `Tests/Runtime`, with shared helpers
  in their respective `Fixtures` folders. Standalone tooling belongs in `Tools~`.
- Preserve `.meta` GUIDs when moving Unity assets, keep public namespaces and
  assembly names stable, and follow `.editorconfig` and `.gitattributes`. Run
  `python3 Packages/com.eitan.mnn/Tools~/Validation/validate_package.py` after
  package layout changes; this check is not native inference validation.
