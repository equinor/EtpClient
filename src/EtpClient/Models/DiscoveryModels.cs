namespace EtpClient.Models;

/// <summary>
/// Categorizes the outcome of a Discovery traversal step.
/// </summary>
public enum DiscoveryResultState
{
    /// <summary>The server returned one or more child resources.</summary>
    CompletedWithResources,

    /// <summary>The server confirmed the URI is valid but has no children.</summary>
    CompletedEmpty,

    /// <summary>The server rejected the request (invalid URI, unsupported protocol, limit exceeded).</summary>
    Failed,
}

/// <summary>
/// The logical result of one ETP Discovery traversal step.
/// Aggregates all <c>GetResourcesResponse</c> parts and captures the empty-acknowledgement outcome.
/// </summary>
public sealed class DiscoveryResult
{
    /// <summary>The URI that was requested.</summary>
    public required string RequestedUri { get; init; }

    /// <summary>Resources returned by the server, in response order.</summary>
    public required IReadOnlyList<DiscoveredResource> Resources { get; init; }

    /// <summary>
    /// <see langword="true"/> when the server confirmed the URI is valid but has no children
    /// by sending an <c>Acknowledge</c> message on Protocol 3.
    /// </summary>
    public required bool WasEmptyAcknowledged { get; init; }

    /// <summary>The ETP message encoding used for this traversal request.</summary>
    public required EtpMessageEncoding MessageEncoding { get; init; }

    /// <summary>The observed traversal outcome.</summary>
    public DiscoveryResultState State =>
        WasEmptyAcknowledged || Resources.Count == 0
            ? DiscoveryResultState.CompletedEmpty
            : DiscoveryResultState.CompletedWithResources;
}

/// <summary>
/// Public representation of one ETP <c>Resource</c> record returned by a Discovery response.
/// Field order matches the ETP v1.1 Avro schema for <c>Energistics.Datatypes.Object.Resource</c>.
/// </summary>
public sealed class DiscoveredResource
{
    /// <summary>URI that uniquely identifies this resource in the ETP address space.</summary>
    public required string Uri { get; init; }

    /// <summary>MIME-style content type describing the data model of this resource.</summary>
    public required string ContentType { get; init; }

    /// <summary>Human-readable display name for this resource.</summary>
    public required string Name { get; init; }

    /// <summary>
    /// The ETP resource type: typically <c>UriProtocol</c>, <c>Folder</c>, or <c>DataObject</c>.
    /// Unknown values from the server are passed through unchanged.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    /// Approximate count of children.
    /// <c>-1</c> = unknown, <c>0</c> = no children, positive = has children.
    /// </summary>
    public required int HasChildren { get; init; }

    /// <summary><see langword="true"/> when this resource can be used with ChannelStreaming.</summary>
    public required bool ChannelSubscribable { get; init; }

    /// <summary><see langword="true"/> when the resource supports change notifications.</summary>
    public required bool ObjectNotifiable { get; init; }

    /// <summary>Optional UUID assigned by the server. May be <see langword="null"/>.</summary>
    public string? Uuid { get; init; }

    /// <summary>Unix epoch milliseconds timestamp of the last change to this resource.</summary>
    public long LastChanged { get; init; }

    /// <summary>Optional custom metadata supplied by the server. Never null; may be empty.</summary>
    public IReadOnlyDictionary<string, string> CustomData { get; init; } =
        new Dictionary<string, string>();
}

/// <summary>
/// Thrown when an ETP Discovery request fails at the protocol level.
/// The <see cref="Exception.Message"/> is guaranteed to be secret-safe.
/// </summary>
public sealed class EtpDiscoveryException : Exception
{
    /// <summary>The URI that was being traversed when the failure occurred.</summary>
    public string RequestedUri { get; }

    /// <summary>ETP protocol exception error code returned by the server, when available.</summary>
    public int? EtpErrorCode { get; }

    /// <summary>
    /// Constructs a new <see cref="EtpDiscoveryException"/>.
    /// Do NOT include credential values in <paramref name="message"/>.
    /// </summary>
    public EtpDiscoveryException(string message, string requestedUri, int? etpErrorCode = null)
        : base(message)
    {
        RequestedUri = requestedUri;
        EtpErrorCode = etpErrorCode;
    }
}
