namespace EtpClient.Models;

/// <summary>
/// Thrown when an ETP connection attempt fails. The <see cref="Message"/> property
/// is guaranteed to be secret-safe (no credentials or encoded Authorization values).
/// </summary>
public sealed class EtpConnectionException : Exception
{
    /// <summary>Identifies the root cause category of the failure.</summary>
    public EtpConnectionFailureCategory Category { get; }

    /// <summary>HTTP status code from the WebSocket upgrade response, when available.</summary>
    public int? HttpStatusCode { get; }

    /// <summary>ETP protocol exception error code, when available.</summary>
    public int? EtpErrorCode { get; }

    /// <summary>
    /// Constructs a new <see cref="EtpConnectionException"/>.
    /// Do NOT include credential values in <paramref name="message"/>.
    /// </summary>
    public EtpConnectionException(
        EtpConnectionFailureCategory category,
        string message,
        Exception? innerException = null,
        int? httpStatusCode = null,
        int? etpErrorCode = null)
        : base(message, innerException)
    {
        Category = category;
        HttpStatusCode = httpStatusCode;
        EtpErrorCode = etpErrorCode;
    }
}

/// <summary>
/// Returned when an ETP connection attempt completes successfully.
/// </summary>
public sealed class EtpConnectionResult
{
    /// <summary>Session details negotiated during the ETP OpenSession exchange.</summary>
    public required NegotiatedSessionInfo Session { get; init; }

    /// <summary>Timestamp (UTC) when the session reached the Connected state.</summary>
    public required DateTimeOffset ConnectedAtUtc { get; init; }

    /// <summary>Endpoint host (without credentials) for diagnostic purposes.</summary>
    public required string EndpointHost { get; init; }

    /// <summary>
    /// The ETP message encoding used for the session.
    /// Reflects the caller's <see cref="EtpConnectionOptions.MessageEncoding"/> selection.
    /// </summary>
    public required EtpMessageEncoding MessageEncoding { get; init; }
}
