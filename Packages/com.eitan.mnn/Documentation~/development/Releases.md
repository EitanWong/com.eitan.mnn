# Releases

`dev` contains development work; merging a PR to `main` does not publish a package.
The `Package release` workflow runs only when a maintainer dispatches it from `main`.
It prepares a GitHub draft release for an existing tag; a maintainer reviews and
publishes that draft separately.

## Prepare a release

1. Merge the intended `dev → main` PR and confirm its repository checks pass.
2. Set the package version in `Packages/com.eitan.mnn/package.json`. Record user-visible
   changes and a release date in the package changelog; keep unreleased changes separate.
   Submit version changes through the same review process.
3. Run the relevant Unity/native checks for the supported platforms. Preserve evidence
   and name the tested commit; static checks alone are not release qualification.
4. Create and push an annotated tag `v<package-version>` at the reviewed main commit.
   Do not move a published tag or reuse a version for different package contents.
5. Dispatch `Package release` from `main` and supply the version without `v`.

The workflow checks the tag's ancestry on `main`, matches the package version,
and runs package/repository validation. It archives only `Packages/com.eitan.mnn`
with a `package/` prefix, includes native plugins, and attaches the `.tgz` and
SHA-256 checksum to a draft release. Git-ignored files and development-project
assets cannot enter that archive. It does not build native plugins.

A rerun can update the same draft. The workflow refuses to modify an already
published release. Review the changelog-based draft notes and archive contents
before publishing. Configure required reviewers on the `release` environment
if release preparation needs approval.

## Versioning and installation

Use semantic versions for the package. The current `3.6.1` metadata is retained
from the existing package; its match with the MNN engine version is not evidence
of a released tag. A future package version can change independently of MNN.
Record an engine update and its compiler/ABI impact explicitly.

UPM consumers install from this repository's package subdirectory using a tag or
commit SHA. See [branches and installation](Branches.md). The draft release archive
can be used by consumers needing an explicit package artifact. This workflow does
not update the historical `upm` branch, publish to OpenUPM or use Unity registry credentials.
