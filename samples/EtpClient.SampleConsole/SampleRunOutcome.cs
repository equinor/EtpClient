using EtpClient.Models;

namespace EtpClient.SampleConsole;

/// <summary>Process exit codes for the sample application.</summary>
public static class SampleExitCode
{
    /// <summary>Session established; sample completed normally.</summary>
    public const int Success = 0;

    /// <summary>One or more required configuration values are missing or malformed.</summary>
    public const int ValidationFailure = 2;

    /// <summary>Server rejected credentials.</summary>
    public const int AuthenticationFailure = 3;

    /// <summary>A network or transport error prevented the connection.</summary>
    public const int TransportFailure = 4;

    /// <summary>The ETP session negotiation failed.</summary>
    public const int ProtocolFailure = 5;

    /// <summary>The connection attempt was canceled.</summary>
    public const int CancellationFailure = 6;

    /// <summary>Maps an <see cref="EtpConnectionFailureCategory"/> to an exit code.</summary>
    public static int FromCategory(EtpConnectionFailureCategory category) => category switch
    {
        EtpConnectionFailureCategory.Validation => ValidationFailure,
        EtpConnectionFailureCategory.Authentication => AuthenticationFailure,
        EtpConnectionFailureCategory.Transport => TransportFailure,
        EtpConnectionFailureCategory.Protocol => ProtocolFailure,
        EtpConnectionFailureCategory.Cancellation => CancellationFailure,
        _ => 1,
    };
}

/// <summary>
/// Represents the summarized result of one sample execution.
/// </summary>
public sealed class SampleRunOutcome
{
    /// <summary>Whether the session was established successfully.</summary>
    public bool Succeeded { get; }

    /// <summary>Final connection state observed by the sample.</summary>
    public EtpConnectionState FinalState { get; }

    /// <summary>Failure category when <see cref="Succeeded"/> is <see langword="false"/>.</summary>
    public EtpConnectionFailureCategory? FailureCategory { get; }

    /// <summary>Secret-safe failure message for local troubleshooting. Null on success.</summary>
    public string? FailureMessage { get; }

    /// <summary>Server application name from negotiated session info. Null on failure.</summary>
    public string? ServerApplicationName { get; }

    /// <summary>Server application version from negotiated session info. Null on failure.</summary>
    public string? ServerApplicationVersion { get; }

    /// <summary>Server instance identifier. Null on failure.</summary>
    public Guid? ServerInstanceId { get; }

    /// <summary>The endpoint host (without credentials) for display purposes.</summary>
    public string EndpointHost { get; }

    private SampleRunOutcome(
        bool succeeded,
        EtpConnectionState finalState,
        string endpointHost,
        EtpConnectionFailureCategory? failureCategory,
        string? failureMessage,
        string? serverApplicationName,
        string? serverApplicationVersion,
        Guid? serverInstanceId)
    {
        Succeeded = succeeded;
        FinalState = finalState;
        EndpointHost = endpointHost;
        FailureCategory = failureCategory;
        FailureMessage = failureMessage;
        ServerApplicationName = serverApplicationName;
        ServerApplicationVersion = serverApplicationVersion;
        ServerInstanceId = serverInstanceId;
    }

    /// <summary>Creates a success outcome from a completed connection result.</summary>
    public static SampleRunOutcome FromSuccess(EtpConnectionResult result) =>
        new(
            succeeded: true,
            finalState: EtpConnectionState.Connected,
            endpointHost: result.EndpointHost,
            failureCategory: null,
            failureMessage: null,
            serverApplicationName: result.Session.ServerApplicationName,
            serverApplicationVersion: result.Session.ServerApplicationVersion,
            serverInstanceId: result.Session.ServerInstanceId);

    /// <summary>Creates a failure outcome from an <see cref="EtpConnectionException"/>.</summary>
    public static SampleRunOutcome FromException(EtpConnectionException ex, string endpointHost) =>
        new(
            succeeded: false,
            finalState: ex.Category == EtpConnectionFailureCategory.Cancellation
                ? EtpConnectionState.Canceled
                : EtpConnectionState.Failed,
            endpointHost: endpointHost,
            failureCategory: ex.Category,
            failureMessage: ex.Message,
            serverApplicationName: null,
            serverApplicationVersion: null,
            serverInstanceId: null);

    /// <summary>Creates a validation failure outcome before any connection attempt.</summary>
    public static SampleRunOutcome FromValidationError(string message, string endpointHost = "") =>
        new(
            succeeded: false,
            finalState: EtpConnectionState.Closed,
            endpointHost: endpointHost,
            failureCategory: EtpConnectionFailureCategory.Validation,
            failureMessage: message,
            serverApplicationName: null,
            serverApplicationVersion: null,
            serverInstanceId: null);

    /// <summary>Maps this outcome to a process exit code.</summary>
    public int ToExitCode() =>
        Succeeded ? SampleExitCode.Success
            : FailureCategory.HasValue
                ? SampleExitCode.FromCategory(FailureCategory.Value)
                : 1;
}
