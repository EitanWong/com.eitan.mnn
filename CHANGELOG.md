# Changelog

Notable repository changes are recorded here. Package-specific release history is
available in the [package changelog](Packages/com.eitan.mnn/CHANGELOG.md).

## Unreleased

### Added

- Added a concise English repository README and a Simplified Chinese translation
  under `docs/`, with links to installation, platform constraints and package guides.
- Added OmniVoice synthesis and optional compact sampling, with profiling and
  experimental classifier-free guidance and trajectory distillation notes.

### Changed

- Organized repository-level documentation under `docs/`; detailed package guides
  remain alongside the package.
- Extended Git ignore rules for local EasyMic packages, imported `Assets/Samples`,
  local models and Mono crash reports. Existing tracked sample assets are removed
  from version control while remaining available locally.
- The development project's EasyMic manifest entries are local-only and are not
  included in this documentation and repository housekeeping change.

### Package development

- The package includes direct C++ ABI bindings, model management and Studio tools.
  Platform and model validation have specific limits; see the
  [testing documentation](Packages/com.eitan.mnn/Documentation~/Testing.md).
- See the [package changelog](Packages/com.eitan.mnn/CHANGELOG.md) for earlier
  package changes and release history.
