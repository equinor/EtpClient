# Contract: CI Workflow Interfaces

**Feature**: 009-ci-github-workflows  
**Date**: 2026-04-14

This document defines the externally observable contracts of the two GitHub Actions workflow files: their triggers, inputs, outputs, and the guarantees they provide to consumers (contributors, maintainers, and downstream automation).

---

## `ci.yml` — Continuous Integration Workflow

### Triggers

| Event | Condition |
|---|---|
| `push` | Branch `main`; paths matching any component filter or `Directory.Packages.props` or `.github/workflows/ci.yml` |
| `pull_request` | `types: [opened, synchronize, reopened]`; target branch `main`; same path filters |

Pushes and PRs to branches other than `main` do not trigger this workflow.

### Concurrency Contract

```
group: ci-{github.ref}
cancel-in-progress: true
```

- For PRs: a new push to the same PR branch cancels the currently running workflow for that branch.
- For `main`: duplicate runs for the same ref are prevented, but no cancellation occurs (the run completes to preserve badge accuracy).

### Jobs

#### `changes` (always runs when workflow triggers)

**Purpose**: Detect which components have changed and publish boolean outputs for downstream jobs.

**Outputs**:

| Output name | Type | Meaning |
|---|---|---|
| `library` | `'true'` / `'false'` | True when `src/EtpClient/**`, `tests/EtpClient.UnitTests/**`, `tests/EtpClient.IntegrationTests/**`, `Directory.Packages.props`, or `.github/workflows/ci.yml` changed |
| `sample-console` | `'true'` / `'false'` | True when `src/EtpClient/**`, `samples/EtpClient.SampleConsole/**`, `tests/EtpClient.SampleConsole.Tests/**`, `Directory.Packages.props`, or `.github/workflows/ci.yml` changed |
| `etp-explorer` | `'true'` / `'false'` | True when `src/EtpClient/**`, `samples/EtpExplorer/**`, `tests/EtpExplorer.Tests/**`, `Directory.Packages.props`, or `.github/workflows/ci.yml` changed |

#### `library` (conditional on `changes.outputs.library == 'true'`)

**Purpose**: Build and test the `EtpClient` core library.

**Environment**: `ubuntu-latest`, .NET 10 SDK  
**Timeout**: 15 minutes  
**Steps**:
1. Checkout repository
2. Set up .NET 10 SDK (`10.0.x`)
3. Restore packages (`dotnet restore EtpClient.slnx`)
4. Build unit test project (`dotnet build tests/EtpClient.UnitTests/ --configuration Release --no-restore`)
5. Build integration test project (`dotnet build tests/EtpClient.IntegrationTests/ --configuration Release --no-restore`)
6. Run unit tests (`dotnet test tests/EtpClient.UnitTests/ --no-build --configuration Release`)
7. Run integration tests (`dotnet test tests/EtpClient.IntegrationTests/ --no-build --configuration Release`)

**Pass condition**: All steps exit 0; `dotnet test` reports 0 failures.

#### `sample-console` (conditional on `changes.outputs.sample-console == 'true'`)

**Purpose**: Build and test the `EtpClient.SampleConsole` sample and its test project.

**Environment**: `ubuntu-latest`, .NET 10 SDK  
**Timeout**: 15 minutes  
**Steps**:
1. Checkout repository
2. Set up .NET 10 SDK
3. Restore packages
4. Build SampleConsole test project (`dotnet build tests/EtpClient.SampleConsole.Tests/ --configuration Release --no-restore`)
5. Run SampleConsole tests (`dotnet test tests/EtpClient.SampleConsole.Tests/ --no-build --configuration Release`)

**Pass condition**: All steps exit 0.

#### `etp-explorer` (conditional on `changes.outputs.etp-explorer == 'true'`)

**Purpose**: Build and test the `EtpExplorer` sample application and its test project.

**Environment**: `ubuntu-latest`, .NET 10 SDK  
**Timeout**: 15 minutes  
**Steps**:
1. Checkout repository
2. Set up .NET 10 SDK
3. Restore packages
4. Build EtpExplorer test project (`dotnet build tests/EtpExplorer.Tests/ --configuration Release --no-restore`)
5. Run EtpExplorer tests (`dotnet test tests/EtpExplorer.Tests/ --no-build --configuration Release`)

**Pass condition**: All steps exit 0.

### Status Badge

The `main`-branch run status is emitted as a workflow status badge at:

```
https://github.com/equinor/EtpClient/actions/workflows/ci.yml/badge.svg?branch=main
```

This badge is embedded in `README.md` immediately after the `# EtpClient` heading.

---

## `publish-nuget.yml` — NuGet Publish Workflow

### Triggers

| Event | Condition |
|---|---|
| `push` | Branch `main`; path `Directory.Build.props` only — fires when a version bump is merged |
| `workflow_dispatch` | Manual dispatch with an optional `version` input for override or pre-release |

### Inputs (workflow_dispatch only)

| Input | Required | Default | Description |
|---|---|---|---|
| `version` | No | (none) | Semantic version override. Omit to use `<VersionPrefix>` from `Directory.Build.props`. Supply to publish a pre-release or to republish a specific version. |

### Version Resolution

The `publish` job resolves the version to publish before packing:
- If `inputs.version` is non-empty → use it (manual override path)
- Otherwise → extract `<VersionPrefix>` from `Directory.Build.props` using `grep`

This means both trigger modes publish the version declared in `Directory.Build.props` by default, keeping the version in a single location.

### Required Permissions

```yaml
permissions:
  contents: read
  packages: write
```

`packages: write` is required to push to the GitHub Package Repository. The `GITHUB_TOKEN` with this permission is sufficient; no PAT or repository secret is needed.

### Job: `publish`

**Environment**: `ubuntu-latest`, .NET 10 SDK, .NET 10 is `10.0.x`  
**Timeout**: 10 minutes  

**Steps**:

1. Checkout repository
2. Set up .NET 10 SDK (`10.0.x`)
3. Resolve package version (reads `Directory.Build.props` or uses `inputs.version` override)
4. Restore packages (`dotnet restore src/EtpClient/EtpClient.csproj`)
5. Build library in Release (`dotnet build src/EtpClient/EtpClient.csproj --configuration Release --no-restore`)
6. Pack library (`dotnet pack ... --no-build -p:PackageVersion=<resolved-version>`)
7. Push to GitHub Packages (`dotnet nuget push ... --skip-duplicate`)

**Pass condition**: All steps exit 0; package is visible under the repository's Packages page.

**`--skip-duplicate` behaviour**: If the same version already exists on GitHub Packages, the push step exits 0 rather than failing. This allows safe re-triggering without version bumping (e.g. after a transient network error).

### NuGet Target Registry

| Field | Value |
|---|---|
| Registry URL | `https://nuget.pkg.github.com/equinor/index.json` |
| Package ID | `EtpClient` (as set in `EtpClient.csproj`) |
| Authentication | `GITHUB_TOKEN` via `--api-key` |

---

## `Directory.Build.props` and `EtpClient.csproj` Metadata Contract

`Directory.Build.props` at the repository root is the **single source of truth** for the library version:

```xml
<Project>
  <PropertyGroup>
    <VersionPrefix>0.1.0</VersionPrefix>
  </PropertyGroup>
</Project>
```

To release a new version: bump `VersionPrefix` in a PR, merge to `main`, and `publish-nuget.yml` fires automatically.

The following properties MUST be present in `EtpClient.csproj` (version is NOT declared here):

```xml
<PackageId>EtpClient</PackageId>
<PackageVersion>0.1.0</PackageVersion>
<Authors>Equinor</Authors>
<Description>A .NET 10 ETP v1.1 client library for authenticated session setup, Discovery traversal, and Protocol 1 ChannelStreaming.</Description>
<RepositoryUrl>https://github.com/equinor/EtpClient</RepositoryUrl>
<RepositoryType>git</RepositoryType>
<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>
<PackageReadmeFile>README.md</PackageReadmeFile>
```

The `PackageVersion` in the csproj is the fallback default; it is always overridden by `-p:PackageVersion=<input>` in the publish workflow. It SHOULD be kept at `0.1.0` until a formal release policy is established.
