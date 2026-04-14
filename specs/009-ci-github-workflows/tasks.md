# Tasks: CI GitHub Actions Workflows

**Input**: Design documents from `specs/009-ci-github-workflows/`
**Prerequisites**: plan.md ✓, spec.md ✓, research.md ✓, contracts/workflow-interfaces.md ✓, quickstart.md ✓

**Tests**: No new automated tests — this feature adds CI configuration and library metadata. Validation is done by running the workflows on GitHub Actions.

**Organization**: Tasks are grouped by user story to enable independent delivery of each CI capability.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to
- All file paths are repository-relative

---

## Phase 1: Setup

**Purpose**: Create the `.github/workflows/` directory and the `EtpClient.csproj` NuGet metadata needed by both the CI and publish workflows.

- [X] T001 Create `.github/workflows/` directory (it does not exist yet)
- [X] T002 [P] Add NuGet package metadata to `src/EtpClient/EtpClient.csproj`: `PackageId`, `PackageVersion` (default `0.1.0`), `Authors`, `Description`, `RepositoryUrl`, `RepositoryType`, `PackageLicenseExpression`, `PackageReadmeFile` per the contract in `specs/009-ci-github-workflows/contracts/workflow-interfaces.md`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The README badge (US2) depends on having a named `ci.yml` workflow. The path-filter `changes` job (US4) must exist before the conditional downstream jobs (US1/US4) can reference its outputs. These tasks establish shared infrastructure consumed by all downstream stories.

**⚠️ CRITICAL**: US1, US2, and US4 jobs all live inside `ci.yml` — write the file once, fully, in US1/US4. No separate foundational YAML file is needed.

- [X] T003 Verify `.github/workflows/` directory exists (created in T001); no further setup needed — YAML files are created per-story

---

## Phase 3: User Story 1 + User Story 4 — Path-Filtered PR and Main CI (Priority: P1 + P2) 🎯 MVP

**Goal**: A single `ci.yml` that triggers on PRs and pushes to `main`, uses `dorny/paths-filter@v3` to detect changed components, and runs the appropriate build-and-test job(s) only.

**Why combined**: US1 (PR CI) and US4 (path filtering) live in the same file. Delivering them together is the simplest correct increment; splitting would require writing the file twice.

**Independent Test**: Open a PR touching only `samples/EtpExplorer/**` — only the `etp-explorer` job runs; `library` and `sample-console` are Skipped. Open a PR touching `src/EtpClient/**` — all three jobs run.

### Implementation for User Story 1 + User Story 4

- [X] T004 [US1] Create `.github/workflows/ci.yml` with:
  - `on:` triggers for `pull_request` (types: opened, synchronize, reopened; branches: main) and `push` (branches: main)
  - Top-level `concurrency:` block: `group: ci-${{ github.ref }}`, `cancel-in-progress: true`
  - Job `changes`: uses `actions/checkout@v4` then `dorny/paths-filter@v3` with three filter sets (`library`, `sample-console`, `etp-explorer`) as specified in `specs/009-ci-github-workflows/plan.md` Component → Path Filter table; emits boolean job outputs
  - Job `library`: `needs: changes`, `if: needs.changes.outputs.library == 'true'`, `runs-on: ubuntu-latest`, `timeout-minutes: 15`; steps: checkout → setup-dotnet (`10.0.x`) → restore (`dotnet restore EtpClient.slnx`) → build `tests/EtpClient.UnitTests/` (`--configuration Release --no-restore`) → build `tests/EtpClient.IntegrationTests/` (`--configuration Release --no-restore`) → test `tests/EtpClient.UnitTests/` (`--no-build`) → test `tests/EtpClient.IntegrationTests/` (`--no-build`)
  - Job `sample-console`: `needs: changes`, `if: needs.changes.outputs.sample-console == 'true'`, `timeout-minutes: 15`; steps: checkout → setup-dotnet → restore → build `tests/EtpClient.SampleConsole.Tests/` → test `tests/EtpClient.SampleConsole.Tests/` (`--no-build`)
  - Job `etp-explorer`: `needs: changes`, `if: needs.changes.outputs.etp-explorer == 'true'`, `timeout-minutes: 15`; steps: checkout → setup-dotnet → restore → build `tests/EtpExplorer.Tests/` → test `tests/EtpExplorer.Tests/` (`--no-build`)
  - Every step in every job MUST have a `name:` field (FR-010)

**Checkpoint**: US1 + US4 fully deliverable and independently testable once this single task is complete.

---

## Phase 4: User Story 2 — Status Badge in README (Priority: P2)

**Goal**: The `main`-branch run result for `ci.yml` is visible as a badge on the repository home page.

**Independent Test**: After T004 is merged to `main` and at least one run completes, view the repo home page — the **CI** badge is visible and reflects the last run outcome.

### Implementation for User Story 2

- [X] T005 [US2] Add the CI status badge to `README.md` immediately after the `# EtpClient` heading (before the first paragraph):
  ```markdown
  [![CI](https://github.com/equinor/EtpClient/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/equinor/EtpClient/actions/workflows/ci.yml)
  ```
- [X] T006 [US2] Update the "Work in progress" bullet in `README.md` that currently reads `(CI) Automated builds in Github, including tests` to reflect that CI is now implemented (remove or update the WIP note)

**Checkpoint**: Badge is visible on the repository home page after the first `main`-branch run completes.

---

## Phase 5: User Story 3 — Readable Workflow Configuration (Priority: P3)

**Goal**: `ci.yml` is self-documenting: every step has a `name:`, every logical block has a comment, the SDK version is in one place, and nothing is hard-coded in a way that would require multiple edits on a .NET upgrade.

**Independent Test**: Read `ci.yml` top-to-bottom; every step has a `name:`, the .NET version appears once, and there is at least one YAML comment per logical block (triggers, concurrency, each job).

### Implementation for User Story 3

- [X] T007 [US3] Review `ci.yml` (created in T004) and verify:
  - Each step has `name:` ✓ (enforced during authoring in T004)
  - `dotnet-version: '10.0.x'` appears exactly once per job (defined as the `10.0.x` token pointing to .NET 10) — if it is duplicated across jobs, extract into an `env:` block or accept the per-job repetition with a comment explaining how to update it
  - At least one YAML comment is present above the `on:`, `concurrency:`, `jobs.changes`, and each build job block
  - If any of the above are missing, update `ci.yml` accordingly

**Checkpoint**: A new contributor can read `ci.yml` in under 5 minutes and understand every step without consulting other documentation.

---

## Phase 6: User Story 5 — Manual NuGet Publish Workflow (Priority: P3)

**Goal**: A maintainer can trigger `publish-nuget.yml` from the GitHub Actions UI, supply a semantic version, and have the `EtpClient` package published to the GitHub Package Repository — using only the built-in `GITHUB_TOKEN`.

**Independent Test**: Trigger the workflow with version `0.1.0-ci-test` (or similar pre-release label); the package appears on the repository's Packages page; the workflow step log shows the push succeeded.

### Implementation for User Story 5

- [X] T008 [P] [US5] Create `.github/workflows/publish-nuget.yml` with:
  - `on: workflow_dispatch:` with a required `version` input (type: `string`, description: `Semantic version to publish (e.g. 1.0.0)`)
  - `permissions: contents: read`, `packages: write`
  - Job `publish`: `runs-on: ubuntu-latest`, `timeout-minutes: 10`
  - Steps: checkout → setup-dotnet (`10.0.x`) → restore (`dotnet restore src/EtpClient/EtpClient.csproj`) → build (`dotnet build src/EtpClient/EtpClient.csproj --configuration Release --no-restore`) → pack (`dotnet pack src/EtpClient/EtpClient.csproj --configuration Release --no-build -p:PackageVersion=${{ inputs.version }}`) → push (`dotnet nuget push "src/EtpClient/bin/Release/*.nupkg" --source "https://nuget.pkg.github.com/equinor/index.json" --api-key "${{ secrets.GITHUB_TOKEN }}" --skip-duplicate`)
  - Every step MUST have a `name:` field

**Checkpoint**: US5 is fully deliverable and independently testable (manual dispatch + Packages page check).

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation and documentation consistency.

- [X] T009 Verify `src/EtpClient/EtpClient.csproj` builds cleanly locally with `dotnet build src/EtpClient/EtpClient.csproj --configuration Release` after the T002 metadata additions — ensure no new warnings or errors are introduced
- [X] T010 [P] Verify `dotnet pack src/EtpClient/EtpClient.csproj --configuration Release` produces a `.nupkg` in `src/EtpClient/bin/Release/` with the correct `PackageId`, `Authors`, and `Description` metadata (inspect with `dotnet nuget inspect` or by extracting the zip)
- [X] T011 [P] Update `specs/009-ci-github-workflows/quickstart.md` if any step details changed during implementation (e.g. actual commit SHA pinning for `dorny/paths-filter`, exact version used)

---

## Dependencies

```
T001 (create .github/workflows dir)
  └─► T004 (ci.yml — US1+US4)
        └─► T005, T006 (README badge — US2)
        └─► T007 (readability check — US3)

T002 (EtpClient.csproj metadata)
  └─► T008 (publish-nuget.yml — US5)
  └─► T009, T010 (polish/verify)

T003 (no-op verify) — independent
T011 (quickstart update) — independent, after T004 and T008
```

**Parallel opportunities per story**:
- T002 and T004 can start simultaneously (different files)
- T005 and T006 can proceed once T004 exists (`ci.yml` is written)
- T008 can proceed once T002 is done (it depends on csproj metadata)
- T009, T010, T011 are independent polish tasks, all parallelizable

---

## Implementation Strategy

**MVP** (just US1 + US4):
→ T001 → T004

**Full P1+P2** (add badge):
→ T001 → T004 → T005, T006

**Full delivery** (all stories):
→ T001, T002 (parallel) → T004, T008 (parallel once dirs exist) → T005, T006, T007 → T009, T010, T011

Total tasks: **11**
