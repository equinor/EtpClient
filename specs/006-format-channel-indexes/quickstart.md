# Quickstart: Format Channel Index Output

## Goal

Verify that the sample console prints primary index values in a human-readable way for time-indexed and depth-indexed channels while preserving fallback behavior for unsupported index types.

## Prerequisites

- .NET 10 SDK installed
- Existing sample configuration for a reachable ETP endpoint
- A target channel or test fixture that returns Protocol 1 channel metadata and channel data

## Verification Paths

### 1. Run automated tests

```bash
dotnet test tests/EtpClient.UnitTests/EtpClient.UnitTests.csproj
dotnet test tests/EtpClient.SampleConsole.Tests/EtpClient.SampleConsole.Tests.csproj
dotnet test tests/EtpClient.IntegrationTests/EtpClient.IntegrationTests.csproj
```

Expected verification:

- codec and model tests confirm that primary-index metadata preserves scale and time datum information
- sample output tests confirm that live and range output use the same formatting rules
- fallback tests confirm that unsupported or incomplete metadata does not produce misleading output

### 2. Verify time-index formatting in the sample

Run the sample against a time-indexed channel.

Expected behavior:

- a raw time index such as `1775845444000000` is not printed as the raw long value
- the sample prints the corresponding timestamp in local time
- channel name and value remain visible on the same output line

### 3. Verify depth-index formatting in the sample

Run the sample against a depth-indexed channel with a preserved scale.

Expected behavior:

- a raw depth index such as `403675000` is converted using the channel's scale
- the sample prints a human-readable depth such as `4036,75m` when the current culture uses comma decimals
- the same interpretation is used for both live output and range output

### 4. Verify fallback behavior

Run the sample against a channel whose primary index metadata is unsupported or incomplete.

Expected behavior:

- the sample does not claim a time or depth interpretation it cannot justify
- the raw or fallback representation remains readable and stable

## Implementation Notes to Validate During Review

- `ChannelDataItem.Indexes` stays raw and unchanged
- `ChannelDefinition` preserves the primary-index metadata needed for conversion
- one shared formatting path is used for both live and historical sample output
- local-time rendering is applied only at the sample presentation boundary
