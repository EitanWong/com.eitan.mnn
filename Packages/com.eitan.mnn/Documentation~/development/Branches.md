# Branches and installation references

Contributions target `dev`. Maintainers review a `dev → main` pull request before
merging; creating a PR does not authorize its merge or publish a release. Fetch
and inspect remote changes before pushing. Avoid force pushes to shared branches.

The repository is a Unity development project. The UPM package lives in
`Packages/com.eitan.mnn`, so installation URLs must include the package path:

```text
https://github.com/EitanWong/com.eitan.mnn.git?path=/Packages/com.eitan.mnn#dev
```

Use `#main` for merged code, an existing `#v<version>` tag for a release, or a full
commit SHA for reproducible dependencies. `dev` and `main` are moving references;
`main` does not imply every platform or task has been validated.

The historical `upm` branch is not maintained by the current release workflow.
Package metadata alone does not establish that a corresponding Git tag exists.
See [releases](Releases.md) and [repository maintenance](../RepositoryManagement.md).
