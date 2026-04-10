# Research: Sample Console Application

## Decision 1: Add a dedicated console sample project under `samples/`

**Decision**: Create a new .NET 10 console application at `samples/EtpClient.SampleConsole` and add it to the solution alongside the existing library and test projects.

**Rationale**: The feature is a runnable reference experience, not production library logic. A dedicated sample project keeps the library clean, makes the example discoverable, and allows the sample to carry app-specific configuration like `UserSecretsId` without polluting the library project.

**Alternatives considered**:
- Add a demo entry point inside `src/EtpClient`: rejected because it mixes library and executable concerns.
- Add a shell script or ad hoc snippet only: rejected because the feature explicitly requires a runnable sample application.
- Put the sample under `tests/`: rejected because the sample is user-facing, not test infrastructure.

## Decision 2: Use .NET user secrets as the local input source, loaded explicitly

**Decision**: Store `EndpointUri`, `Username`, and `Password` in .NET user secrets and load them explicitly from the sample app configuration pipeline.

**Rationale**: Official Microsoft guidance for non-web apps requires adding user secrets support explicitly for `Microsoft.NET.Sdk` console apps. Loading user secrets explicitly keeps the sample predictable and avoids relying on `DOTNET_ENVIRONMENT=Development` behavior alone. This matches the feature requirement that the sample should use .NET secrets for input.

**Alternatives considered**:
- Load user secrets only through Generic Host defaults in `Development`: rejected because it is environment-sensitive and less predictable for a first-run sample.
- Use command-line arguments for credentials: rejected because the user requested .NET secrets for input and command lines can leak secrets in shell history.
- Store secrets in `appsettings.json`: rejected because secrets must stay out of source-controlled files.

## Decision 3: Use Generic Host for configuration, logging, and lifetime management

**Decision**: Build the sample around `Host.CreateApplicationBuilder(args)` and then explicitly add user secrets to the app configuration for the sample assembly.

**Rationale**: Generic Host provides consistent configuration layering, console logging, and clean cancellation/disposal semantics. It also makes the sample easier to extend later while still remaining minimal.

**Alternatives considered**:
- Raw `ConfigurationBuilder` with top-level statements only: rejected because it would require more manual wiring for logging and cancellation.
- Manual environment-variable parsing: rejected because it weakens the user-secrets-first workflow.

## Decision 4: Bind to a strongly defined sample configuration model and validate before connecting

**Decision**: Introduce a small options model for the sample run and validate the required settings before any network attempt.

**Rationale**: The sample should fail fast and tell the user which required input is missing or malformed. This also keeps the connection flow in `Program.cs` readable and useful as reference code.

**Alternatives considered**:
- Read configuration keys ad hoc in multiple places: rejected because it scatters validation and hurts the sample’s clarity.
- Reuse `EtpConnectionOptions` directly as the configuration type: rejected because the sample still needs its own user-facing validation and presentation model before constructing library options.

## Decision 5: Test the sample at the app boundary, not by re-testing protocol wiring

**Decision**: Add automated tests that cover sample configuration validation, success/failure presentation, and host/application flow while continuing to rely on the existing library tests for protocol correctness.

**Rationale**: The new feature is a sample app. The protocol wire behavior is already tested in the library projects. Sample-specific tests should prove the sample consumes the library correctly and handles secrets/configuration safely.

**Alternatives considered**:
- Recreate full protocol coverage in the sample tests: rejected because it duplicates existing library test coverage.
- Ship the sample with no tests: rejected because the repo constitution requires automated coverage for user-facing changes.

## Decision 6: Use a documented secret key contract for local setup

**Decision**: Define a stable configuration contract under the `Etp` section, with keys `Etp:EndpointUri`, `Etp:Username`, and `Etp:Password`.

**Rationale**: Developers need copy-pasteable setup instructions and stable key names for `dotnet user-secrets set`. A simple hierarchical contract aligns with Microsoft configuration conventions.

**Alternatives considered**:
- Flat key names like `EndpointUri`: rejected because namespacing is clearer and easier to extend.
- Multiple competing configuration shapes: rejected because the sample should teach one canonical flow.
