# Research: CI GitHub Actions Workflows

**Feature**: 009-ci-github-workflows  
**Date**: 2026-04-14  
**Resolves**: path-filter approach, NuGet metadata requirements, GITHUB_TOKEN auth, workflow concurrency, dotnet pack/push patterns

---

## 1. Path-Filtered Builds

### Decision
Use `dorny/paths-filter@v3` in a dedicated `changes` job at the start of `ci.yml`. The `changes` job produces boolean outputs (`library`, `sample-console`, `etp-explorer`) that downstream jobs consume via `needs.changes.outputs.<name> == 'true'`.

### Rationale
GitHub Actions natively supports `paths:` filters at the workflow trigger level, but those are all-or-nothing: the entire workflow is skipped if no matching paths change. That does not meet the requirement of running only the relevant _job_ when, say, `src/EtpClient` changes — because such a filter at the trigger level would also skip the Explorer job even when Explorer files changed.

`dorny/paths-filter` solves this cleanly: it detects changed files within a running workflow and emits granular outputs per path-set. Downstream jobs then use `if:` conditions on those outputs.

### Alternatives Considered

| Alternative | Rejected because |
|---|---|
| Separate `ci-library.yml`, `ci-explorer.yml`, `ci-sample-console.yml` workflow files | Three independent badges; no shared concurrency group; DRY violations on SDK version and runner configuration |
| Native `paths:` on each trigger (all-or-nothing) | Cannot achieve per-job skipping; either the whole workflow fires or it doesn't |
| Manual `git diff` script step | Fragile with merge commits; re-implements what `dorny/paths-filter` does reliably |
| GitHub's native path-filter per job (does not exist as of 2026) | Not a GitHub Actions feature |

### Trust Assessment for `dorny/paths-filter@v3`
- Author: Michal Dorner (dorny), widely used across OSS (millions of runs/month per GitHub Marketplace)
- Pinning strategy: use `dorny/paths-filter@v3` (tracks the v3 major tag); for maximum reproducibility, pin to a specific commit SHA in tasks (e.g. `dorny/paths-filter@de90cc6`)
- The action reads `git diff` output only; it does not execute arbitrary code on repository contents and has no write permissions

---

## 2. GitHub Actions Workflow Structure

### Decision
One `ci.yml` file with four jobs: `changes` (path-filter detection) → three parallel downstream jobs (`library`, `sample-console`, `etp-explorer`), each conditional on the relevant `changes` output.

### Job Design: `library`

```yaml
library:
  needs: changes
  if: needs.changes.outputs.library == 'true'
  runs-on: ubuntu-latest
  timeout-minutes: 15
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with:
        dotnet-version: '10.0.x'
    - name: Restore packages
      run: dotnet restore EtpClient.slnx
    - name: Build library (Release)
      run: dotnet build src/EtpClient/EtpClient.csproj --configuration Release --no-restore
    - name: Run unit tests
      run: dotnet test tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj --configuration Release --no-build --logger "trx;LogFileName=unit-tests.trx"
    - name: Run integration tests
      run: dotnet test tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj --configuration Release --no-build --logger "trx;LogFileName=integration-tests.trx"
```

**Note on `--no-build` for test projects**: `dotnet test --no-build` skips the build step and runs the already-compiled test binary. Since the library job builds only `src/EtpClient/EtpClient.csproj` first, the test projects are NOT yet compiled. The correct approach is to either:

1. Build the test project explicitly (which triggers its dependency chain), or
2. Use `dotnet test` _without_ `--no-build` and use `--no-restore` instead (restore was done once upfront).

**Chosen approach**: Restore once with `dotnet restore EtpClient.slnx`, then for each job build the specific test project(s) (which cascades to build dependencies via MSBuild's project-reference graph) and run with `--no-restore`. This is efficient without requiring a global solution build.

For the `library` job that means:
- `dotnet build tests/EtpClient.UnitTests/... --configuration Release --no-restore` (builds EtpClient transitively)
- `dotnet build tests/EtpClient.IntegrationTests/... --configuration Release --no-restore`
- `dotnet test tests/EtpClient.UnitTests/... --no-build`
- `dotnet test tests/EtpClient.IntegrationTests/... --no-build`

### Timeout Justification
| Job | Justification for 15 min |
|---|---|
| `library` | Integration tests use an in-process TestHost; build + ~200 unit + integration tests should complete in < 5 min; 15 min is safe headroom |
| `sample-console` | Smaller test suite; 15 min is conservative |
| `etp-explorer` | Spectre.Console rendering tests are CPU-bound; 15 min is safe |

### Concurrency (PR only)
```yaml
concurrency:
  group: ci-${{ github.ref }}
  cancel-in-progress: true
```
This cancels in-flight runs for the same PR branch when a new push arrives. On `main`, concurrency groups still prevent duplicate runs but do not cancel (important for badge accuracy).

---

## 3. Status Badge

### Decision
Add a single badge to `README.md` pointing to the `ci.yml` workflow's `main`-branch status.

### Badge URL format
```markdown
[![CI](https://github.com/equinor/EtpClient/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/equinor/EtpClient/actions/workflows/ci.yml)
```

### Placement
Insert immediately after the `# EtpClient` heading in `README.md`, before the first paragraph, so it is visible without scrolling.

---

## 4. NuGet Metadata for `EtpClient.csproj`

### Decision
Add the following properties to the `<PropertyGroup>` in `src/EtpClient/EtpClient.csproj`:

```xml
<PackageId>EtpClient</PackageId>
<!-- Version is set centrally in Directory.Build.props via VersionPrefix -->
<Authors>Equinor</Authors>
<Description>A .NET 10 ETP v1.1 client library for authenticated session setup, Discovery traversal, and Protocol 1 ChannelStreaming.</Description>
<RepositoryUrl>https://github.com/equinor/EtpClient</RepositoryUrl>
<RepositoryType>git</RepositoryType>
<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>
<PackageReadmeFile>README.md</PackageReadmeFile>
```

The version is NOT declared in `EtpClient.csproj`. Instead, `Directory.Build.props` at the repository root declares:

```xml
<VersionPrefix>0.1.0</VersionPrefix>
```

MSBuild automatically imports `Directory.Build.props` for every project in the tree and derives `PackageVersion` from `VersionPrefix`. A maintainer bumps the version by editing this single file.

`PackageVersion` defaults to the `VersionPrefix` from `Directory.Build.props`. The manual publish workflow can override it at pack-time with `-p:PackageVersion=<value>` (e.g. for pre-releases).

### Rationale
- `PackageId` and `Authors` are required for a well-formed NuGet package
- `Description` is displayed on GitHub Packages; missing it degrades discoverability
- `RepositoryUrl` allows package consumers to navigate to source
- `PackageLicenseExpression` communicates the license (Apache-2.0 matches the LICENSE file)
- `PackageReadmeFile` surfaces the README in NuGet clients
- `Directory.Build.props` is the single file to edit when releasing a new version

---

## 5. NuGet Publish Workflow

### Decision
Separate file `publish-nuget.yml` with two triggers:
1. **`push` to `main` with `paths: ['Directory.Build.props']`** — fires automatically when a PR that bumps `VersionPrefix` is merged.
2. **`workflow_dispatch`** with an optional `version` input — for manual re-publish or pre-release overrides.

### Version Resolution
A `Resolve package version` step runs before `dotnet pack`:

```bash
if [ -n "${{ inputs.version }}" ]; then
  echo "value=${{ inputs.version }}" >> $GITHUB_OUTPUT
else
  VERSION=$(grep -oP '(?<=<VersionPrefix>)[^<]+' Directory.Build.props)
  echo "value=$VERSION" >> $GITHUB_OUTPUT
fi
```

- When triggered by a `push` event, `inputs.version` is empty → version is read from `Directory.Build.props`.
- When triggered manually with a version → that value is used (allows pre-release override).
- When triggered manually without a version → version is read from `Directory.Build.props` (idiomatic re-publish).

### Authentication
GitHub Package Registry requires NuGet authentication. The `GITHUB_TOKEN` is sufficient when:
1. The NuGet source is `https://nuget.pkg.github.com/equinor/index.json`
2. The NuGet push command uses `--api-key ${{ secrets.GITHUB_TOKEN }}`
3. The job has `permissions: packages: write`

No PAT or repository secret is required.

### Version Input
The `workflow_dispatch` exposes a required `version` input of type `string`. The pack step uses `-p:PackageVersion=${{ inputs.version }}` to override the default version in the csproj without editing the file.

### Push command
```bash
dotnet nuget push "src/EtpClient/bin/Release/*.nupkg" \
  --source "https://nuget.pkg.github.com/equinor/index.json" \
  --api-key "${{ secrets.GITHUB_TOKEN }}"
```

The `--skip-duplicate` flag can be added to avoid errors when re-pushing an already-published version (idempotency).

### Source Registration
When using `dotnet nuget push` with an explicit `--source` URL and API key, no `nuget.config` or local source registration step is needed. The URL and key are passed inline.

---

## 6. SDK Version Pin

### Decision
Use `dotnet-version: '10.0.x'` in `actions/setup-dotnet@v4`. This resolves to the latest .NET 10 patch SDK available on the runner, which tracks the same major/minor as the projects' `<TargetFramework>net10.0`.

### Rationale
Using `10.0.x` (semver wildcard) avoids hard-coding a patch version that would require manual updates on each SDK release while staying pinned to the confirmed-compatible major/minor.

---

## 7. `dotnet test` — Skipped Tests vs Failures

### Decision
Use no special flags. `dotnet test` exits with code 0 on skip-only runs and non-zero on any failing test. GitHub Actions reads the exit code; a skip does not cause failure.

### Rationale
The edge case in the spec (skipped-but-not-failing) is handled correctly by default `dotnet test` behavior. No additional tooling is required.

---

## Summary of Decisions

| Question | Decision |
|---|---|
| Path filter approach | `dorny/paths-filter@v3` in a `changes` job with boolean outputs |
| CI file structure | Single `ci.yml` with `changes` + 3 conditional jobs |
| Publish file | Separate `publish-nuget.yml` with `workflow_dispatch` |
| NuGet auth | Built-in `GITHUB_TOKEN` + `permissions: packages: write` |
| SDK version | `10.0.x` in `actions/setup-dotnet@v4` |
| NuGet metadata | `PackageId`, `Authors`, `Description`, `RepositoryUrl`, `RepositoryType`, license, readme in `EtpClient.csproj` |
| Version source | `<VersionPrefix>` in `Directory.Build.props` (single source of truth) |
| Publish trigger | Auto on `push` to `main` when `Directory.Build.props` changes; manual `workflow_dispatch` with optional override |
| Status badge | `ci.yml` main-branch badge in README after `# EtpClient` heading |
| Concurrency | `cancel-in-progress: true` scoped to `github.ref` |
| Restore strategy | `dotnet restore EtpClient.slnx` once per job, then `--no-restore` on all subsequent steps |
