# Quickstart: CI GitHub Actions Workflows

**Feature**: 009-ci-github-workflows  
**Date**: 2026-04-14

---

## Prerequisites

- Access to the `equinor/EtpClient` repository on GitHub (at minimum read access for PR validation, write access for the `main` branch status badge, maintainer role for manual publish).
- No additional tools or secrets are required. The `GITHUB_TOKEN` is provided automatically by GitHub Actions.

---

## 1. Verifying CI on a Pull Request

1. Create a branch from `main`, make any change to a tracked path (e.g. add a comment to `src/EtpClient/EtpClient.csproj`).
2. Open a pull request targeting `main`.
3. After a few seconds, a **"CI"** check appears in the PR's Checks section with status _Queued_ → _In progress_ → _Success_ or _Failure_.
4. Click **Details** next to any job to see the step-level output including build and test results.
5. If the PR build is green, the merge button becomes available (subject to repository branch protection rules).

**Expected path filtering behaviour:**
- If your change touches only `samples/EtpExplorer/**`, only the **etp-explorer** job runs; `library` and `sample-console` show as **Skipped**.
- If your change touches `src/EtpClient/**` or `Directory.Packages.props`, all three jobs run.

---

## 2. Verifying the README Badge

1. Navigate to the repository home page at `https://github.com/equinor/EtpClient`.
2. Below the `# EtpClient` heading, a badge labelled **CI** is displayed.
3. The badge shows **passing** (green) or **failing** (red) based on the most recent completed run on `main`.
4. Click the badge to open the Actions workflow run history for `ci.yml` on `main`.

---

## 3. Triggering the Manual NuGet Publish

> **Requires**: Maintainer-level access and a semantic version number.

1. Navigate to **Actions** → **Publish NuGet** in the repository sidebar.
2. Click **Run workflow**.
3. In the dropdown, ensure the **`main`** branch is selected.
4. Enter the semantic version to publish (e.g. `1.0.0`).
5. Click **Run workflow**.
6. The workflow stages:
   - **Restore** → **Build (Release)** → **Pack** → **Push to GitHub Packages**
7. When complete, navigate to the repository's **Packages** tab. The `EtpClient` package should be listed with the version you supplied.

**Re-publishing the same version**: The push step uses `--skip-duplicate`, so pushing an already-published version is a no-op (exits 0). To overwrite an existing version you must delete the existing package version from GitHub Packages first.

---

## 4. Path Filter Reference

| Changed files | `library` job | `sample-console` job | `etp-explorer` job |
|---|---|---|---|
| `src/EtpClient/**` | Runs | Runs | Runs |
| `Directory.Packages.props` | Runs | Runs | Runs |
| `.github/workflows/ci.yml` | Runs | Runs | Runs |
| `samples/EtpClient.SampleConsole/**` | Skipped | Runs | Skipped |
| `tests/EtpClient.SampleConsole.Tests/**` | Skipped | Runs | Skipped |
| `samples/EtpExplorer/**` | Skipped | Skipped | Runs |
| `tests/EtpExplorer.Tests/**` | Skipped | Skipped | Runs |
| `tests/EtpClient.UnitTests/**` | Runs | Skipped | Skipped |
| `tests/EtpClient.IntegrationTests/**` | Runs | Skipped | Skipped |
| `specs/**`, `docs/**`, `README.md` only | Skipped | Skipped | Skipped |

---

## 5. Acceptance Validation for SC-006 (Path Filtering)

*Timed 6-step procedure. Target: under 5 minutes per scenario.*

### Scenario A — Explorer-only change

1. On a branch, touch only `samples/EtpExplorer/ExplorerApp.cs` (e.g. add/remove a blank line).
2. Push the branch and open a PR.
3. In the PR Checks panel, observe: **etp-explorer** shows _In progress_; **library** and **sample-console** show _Skipped_.
4. Wait for the etp-explorer job to complete. **Pass**: green check, 0 failures.

### Scenario B — Library change propagates

1. On a branch, touch `src/EtpClient/EtpClient.csproj` (e.g. add/remove a blank line).
2. Push and open a PR.
3. All three jobs appear as _In progress_ simultaneously.
4. All three complete successfully. **Pass**: all green.

---

## 6. NuGet Package Consumption

Once published, consumers can add the GitHub Package Registry as a NuGet source and install the package:

```bash
dotnet nuget add source \
  "https://nuget.pkg.github.com/equinor/index.json" \
  --name "github-equinor" \
  --username <github-username> \
  --password <github-personal-access-token>

dotnet add package EtpClient --version 1.0.0
```

The package requires a GitHub personal access token (PAT) with at least `read:packages` scope and access to the `equinor` organisation, or a scoped fine-grained token with packages read permission.
