# Feature Specification: CI GitHub Actions Workflows

**Feature Branch**: `009-ci-github-workflows`  
**Created**: 2026-04-14  
**Status**: Draft  
**Input**: User description: "The repository should have sensible CI github action workflows"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Build and Test on Pull Request (Priority: P1)

A contributor opens a pull request against `main`. They need fast, automated feedback that their changes compile correctly and all tests pass — before the PR can be merged. The workflow runs automatically, reports pass/fail per step, and blocks merge on failure.

**Why this priority**: This is the core CI value proposition. Without a passing-build gate, broken changes can silently reach `main`. It is the minimum useful workflow.

**Independent Test**: Can be validated by opening a PR with a deliberate compilation error and confirming the workflow run fails, then opening a PR with no errors and confirming it passes.

**Acceptance Scenarios**:

1. **Given** a pull request is opened or updated targeting `main`, **When** the workflow triggers, **Then** `dotnet build` runs against the full solution and the step reports success or failure.
2. **Given** the solution builds successfully, **When** the workflow continues, **Then** all four test projects run (`EtpClient.UnitTests`, `EtpClient.IntegrationTests`, `EtpClient.SampleConsole.Tests`, `EtpExplorer.Tests`) and each reports its pass/fail count.
3. **Given** any test project has failing tests, **When** the workflow completes, **Then** the overall workflow run is marked failed and GitHub blocks merge on the protected branch.
4. **Given** a PR workflow run passes for the first time, **When** a subsequent commit is pushed to the same PR branch, **Then** the workflow re-triggers automatically.

---

### User Story 2 - Build and Test on Push to Main (Priority: P2)

A maintainer merges a pull request into `main` (or commits directly). The same build-and-test cycle runs automatically so the repository health status on `main` is always known, and the README badge stays accurate.

**Why this priority**: PR checks alone do not protect against merge-induced breakage (e.g., merge conflicts resolved in-browser). Continuous feedback on `main` is needed for the README status badge and to catch regressions early.

**Independent Test**: Can be validated by pushing a passing commit to `main` and confirming the workflow run succeeds; the status badge in the README reflects the latest run outcome.

**Acceptance Scenarios**:

1. **Given** a commit is pushed to `main`, **When** the workflow triggers, **Then** `dotnet build` and all tests run using the same steps as the PR workflow.
2. **Given** the `main` workflow run completes, **When** the README is viewed on GitHub, **Then** a status badge links to the latest `main` run result (passing or failing).
3. **Given** the `main` branch build fails, **When** a developer views the repository home page, **Then** the badge visibly shows failure, drawing attention to the broken state.

---

### User Story 3 - Readable Workflow Configuration (Priority: P3)

A new contributor reads the CI workflow YAML to understand how the project is built and tested, without needing to consult additional documentation. The workflow file is self-contained, annotated, and follows the project's existing conventions (`.NET 10`, central package management).

**Why this priority**: Readable infrastructure reduces onboarding friction and makes it easier to extend CI later (e.g., adding publishing). It is a quality concern rather than a blocking capability.

**Independent Test**: Can be validated by reviewing the YAML for named steps, absence of hard-coded versions that drift from `Directory.Packages.props`, and the presence of at least one comment per logical block.

**Acceptance Scenarios**:

1. **Given** the workflow YAML is open in a code review, **When** a developer reads it, **Then** each step has a `name:` field that says what it does in plain language.
2. **Given** the .NET SDK version is specified in the workflow, **When** the project upgrades to a new .NET version, **Then** there is a single, obvious place to update the SDK version.

---

### User Story 4 - Path-Filtered Builds (Priority: P2)

A contributor changes only the `EtpExplorer` sample application. The CI run that triggers should build and test only the Explorer sample and its test project — not the core library or the `SampleConsole` sample — because those are unaffected. This keeps CI feedback fast and avoids burning Actions minutes on irrelevant rebuilds.

**Why this priority**: The repository has three independently deployable components (library, SampleConsole sample, EtpExplorer sample). Building all of them for a change to one is wasteful, but correctness requires that a library change still rebuilds and retests everything that depends on it.

**Independent Test**: Can be validated by changing a file only in `samples/EtpExplorer/` and observing that the library and SampleConsole CI jobs are either skipped or do not run; only the Explorer job runs.

**Acceptance Scenarios**:

1. **Given** only files under `samples/EtpExplorer/**` change, **When** the CI workflow triggers, **Then** only the Explorer build-and-test job runs; the library and SampleConsole jobs are skipped.
2. **Given** only files under `samples/EtpClient.SampleConsole/**` change, **When** the CI workflow triggers, **Then** only the SampleConsole build-and-test job runs; the library and Explorer jobs are skipped.
3. **Given** files under `src/EtpClient/**` change, **When** the CI workflow triggers, **Then** all three build-and-test jobs run because the library is a common dependency.
4. **Given** `Directory.Packages.props` changes, **When** the CI workflow triggers, **Then** all three build-and-test jobs run because package versions affect every project.

---

### User Story 5 - Publish Library to GitHub Package Repository (Priority: P3)

A maintainer wants to publish a new version of the `EtpClient` library as a NuGet package to the GitHub Package Repository (GPR) under the `equinor` organisation. The publish is triggered manually so that the maintainer chooses exactly when a release happens and which version number to stamp on it.

**Why this priority**: Making the library available for consumption outside this repository requires a published NuGet package. Manual triggering (rather than automatic) gives maintainers explicit control over release cadence and version numbers.

**Independent Test**: Can be validated by triggering the manual workflow with a test version number in a dry environment and confirming the `.nupkg` is listed under Packages on the repository page.

**Acceptance Scenarios**:

1. **Given** a maintainer navigates to Actions and triggers the publish workflow, **When** they provide a semantic version (e.g. `1.0.0`), **Then** the workflow packs the library with that version and pushes it to `https://nuget.pkg.github.com/equinor/index.json`.
2. **Given** the publish workflow completes successfully, **When** the maintainer views the repository's Packages page, **Then** the new version of `EtpClient` is listed and downloadable.
3. **Given** the workflow runs, **When** the NuGet push step executes, **Then** authentication uses `GITHUB_TOKEN` — no manually managed secrets are required.
4. **Given** the library project lacks NuGet metadata (PackageId, description, Authors, RepositoryUrl), **When** the publish workflow is introduced, **Then** those metadata fields are added to `EtpClient.csproj` so the package is well-formed.

---

### Edge Cases

- A test project hangs indefinitely — the workflow should have a per-job timeout so builds do not consume Actions minutes forever.
- `dotnet test` exits with a non-zero code when tests are skipped but none fail — the workflow must distinguish a skip-only run (success) from a failing run (failure).
- The integration tests use an in-process test server (ASP.NET Core `TestHost`) and do not require an external ETP endpoint; CI must not attempt to configure external infrastructure.
- A contributor pushes hundreds of small commits to a PR branch rapidly — the workflow should not queue more than the in-flight run (consider `concurrency` to cancel outdated runs).

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: A CI workflow MUST trigger on every pull request event (opened, synchronised, reopened) targeting the `main` branch.
- **FR-002**: A CI workflow MUST trigger on every push to the `main` branch.
- **FR-003**: The CI workflow MUST build the entire solution using `dotnet build` with the `Release` configuration.
- **FR-004**: The CI workflow MUST run all four test projects: `EtpClient.UnitTests`, `EtpClient.IntegrationTests`, `EtpClient.SampleConsole.Tests`, and `EtpExplorer.Tests`.
- **FR-005**: A test failure in any project MUST cause the workflow run to fail and prevent auto-merge on protected branches.
- **FR-006**: The workflow MUST use the .NET 10 SDK, specified in a single location that is easy to update.
- **FR-007**: The `main`-branch workflow run result MUST be surfaced as a status badge in `README.md`.
- **FR-008**: Each workflow job MUST declare a `timeout-minutes` limit to prevent runaway builds consuming Actions quota.
- **FR-009**: The PR workflow MUST use a `concurrency` group keyed on the PR number so that a newer push cancels the in-flight run for the same PR.
- **FR-010**: Workflow steps MUST each carry a human-readable `name:` field.
- **FR-011**: The CI workflow MUST use path filters so that a change to only one component (library, SampleConsole sample, or EtpExplorer sample) triggers build and test for that component only; a change to `src/EtpClient/**` or `Directory.Packages.props` triggers all three components because both are common dependencies.
- **FR-012**: A separate manual workflow (`workflow_dispatch`) MUST allow a maintainer to publish the `EtpClient` library as a NuGet package to the GitHub Package Repository using a caller-supplied semantic version string.
- **FR-013**: The NuGet publish workflow MUST authenticate to the GitHub Package Repository using the built-in `GITHUB_TOKEN`; no manually managed secrets MUST be required.
- **FR-014**: `EtpClient.csproj` MUST carry the NuGet metadata fields (`PackageId`, `Description`, `Authors`, `RepositoryUrl`) required to produce a well-formed package.

### Key Entities

- **Workflow file (PR)**: A YAML file in `.github/workflows/` that defines the pull-request CI job.
- **Workflow file (main CI)**: A YAML file (may be the same file or a separate one) defining the push-to-main CI job.
- **Workflow file (NuGet publish)**: A separate YAML file triggered manually via `workflow_dispatch` for publishing the library.
- **Status badge**: A Markdown image link in `README.md` pointing to the `main`-branch workflow run.
- **SDK version pin**: The single location (`uses: actions/setup-dotnet` step) where the .NET SDK version is declared.
- **Component**: One of three independently buildable units — the library (`src/EtpClient`), the SampleConsole sample, or the EtpExplorer sample — each with its own dependent test projects.
- **Path filter**: A set of file glob patterns per component that determines whether a CI job for that component runs, based on what changed in the trigger commit.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A pull request with a deliberate compilation error causes the CI workflow to fail within 5 minutes of the push.
- **SC-002**: A pull request with all tests passing causes the CI workflow to succeed; the merge button becomes available without manual override.
- **SC-003**: A push to `main` triggers a workflow run and the resulting pass/fail status is visible as a badge on the repository home page.
- **SC-004**: A rapid sequence of commits to an open PR results in at most one active (non-cancelled) workflow run at a time.
- **SC-005**: The workflow YAML can be read by a new contributor in under 5 minutes to understand every build and test step without consulting other documentation.
- **SC-006**: A commit that changes only `samples/EtpExplorer/**` results in zero library or SampleConsole CI jobs running; only the Explorer job runs.
- **SC-007**: After triggering the manual publish workflow with a valid semantic version, the package appears on the repository's Packages page within 5 minutes.

## Assumptions

- The repository is hosted on GitHub (equinor/EtpClient) and GitHub Actions is the target CI platform.
- All tests in all four test projects are self-contained and require no external ETP server, credentials, or network access — integration tests use an in-process `TestHost`.
- NuGet package publishing to the GitHub Package Repository is in scope via a manual `workflow_dispatch` workflow; automated release on tag/push and publishing to nuget.org are out of scope.
- The workflow targets `ubuntu-latest` runners as the default; cross-platform matrix testing is out of scope for this initial CI setup.
- No secrets or environment variables beyond what GitHub Actions provides by default (`GITHUB_TOKEN`) are needed to build, test, or publish.
- `dotnet build` with `--configuration Release` is the authoritative build check; the `Debug` configuration used in VS tasks is not separately verified in CI.
- The path-filter implementation may use a well-maintained third-party GitHub Action (such as `dorny/paths-filter`) to detect changed paths; the choice is justified in `research.md`.
