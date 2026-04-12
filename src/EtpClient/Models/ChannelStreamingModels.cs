namespace EtpClient.Models;

// ── Channel Description (User Story 1) ────────────────────────────────────────

/// <summary>Categorizes the outcome of a channel description request.</summary>
public enum ChannelDescriptionState
{
    /// <summary>The server returned one or more channel definitions.</summary>
    Completed,

    /// <summary>The server rejected the request (unsupported URI, permission denied, etc.).</summary>
    Failed,
}

/// <summary>
/// The complete result of one <c>ChannelDescribe</c> operation.
/// Aggregates all <c>ChannelMetadata</c> parts and captures request context.
/// </summary>
public sealed class ChannelDescriptionResult
{
    /// <summary>The URI(s) that were described.</summary>
    public required IReadOnlyList<string> RequestedUris { get; init; }

    /// <summary>Channel definitions returned by the producer, in response order.</summary>
    public required IReadOnlyList<ChannelDefinition> Channels { get; init; }

    /// <summary>The ETP message encoding used for this request.</summary>
    public required EtpMessageEncoding MessageEncoding { get; init; }

    /// <summary><see langword="true"/> when the metadata arrived in multiple parts.</summary>
    public required bool WasMultipart { get; init; }

    /// <summary>The observed description outcome.</summary>
    public required ChannelDescriptionState State { get; init; }
}

/// <summary>
/// Public representation of one Protocol 1 channel described by a producer.
/// Field order matches the ETP v1.1 Avro schema for
/// <c>Energistics.Datatypes.ChannelData.ChannelMetadataRecord</c>.
/// </summary>
public sealed class ChannelDefinition
{
    /// <summary>URI that uniquely identifies this channel within the session.</summary>
    public required string ChannelUri { get; init; }

    /// <summary>Producer-assigned integer identifier for this channel. Session-scoped.</summary>
    public required long ChannelId { get; init; }

    /// <summary>Human-readable name for the channel (e.g., mnemonic).</summary>
    public required string ChannelName { get; init; }

    /// <summary>Fully-qualified Avro data type name (e.g., <c>double</c>, <c>int</c>).</summary>
    public required string DataType { get; init; }

    /// <summary>Unit of measure for all data values in this channel.</summary>
    public required string Uom { get; init; }

    /// <summary>Type of the primary index: <c>Time</c> or <c>Depth</c>.</summary>
    public required string IndexType { get; init; }

    /// <summary>Unit of measure for the primary index.</summary>
    public required string IndexUom { get; init; }

    /// <summary>Direction of the primary index: <c>Increasing</c> or <c>Decreasing</c>.</summary>
    public required string IndexDirection { get; init; }

    /// <summary>
    /// Power-of-ten scale factor for depth indexes.
    /// A raw depth index value is divided by 10^<see cref="IndexScale"/> to obtain the physical depth.
    /// Zero means no scaling.
    /// </summary>
    public int IndexScale { get; init; }

    /// <summary>
    /// Optional UTC datum for time indexes, as an ISO 8601 string.
    /// When present, raw time index values are interpreted as microsecond offsets from this datum.
    /// When absent, the Unix epoch (1970-01-01T00:00:00Z) is used.
    /// </summary>
    public string? IndexTimeDatum { get; init; }

    /// <summary>Optional depth datum identifier or URI for depth indexes.</summary>
    public string? IndexDepthDatum { get; init; }

    /// <summary>Optional mnemonic for the primary index.</summary>
    public string? IndexMnemonic { get; init; }

    /// <summary>Optional human-readable description of the primary index.</summary>
    public string? IndexDescription { get; init; }

    /// <summary>First recorded primary index value, when available.</summary>
    public long? StartIndex { get; init; }

    /// <summary>Last recorded primary index value, when available.</summary>
    public long? EndIndex { get; init; }

    /// <summary>Human-readable description of the channel.</summary>
    public required string Description { get; init; }

    /// <summary>Current status: <c>Active</c>, <c>Inactive</c>, or <c>Closed</c>.</summary>
    public required string Status { get; init; }

    /// <summary>Content type when the data points are structured objects, otherwise empty.</summary>
    public string ContentType { get; init; } = string.Empty;

    /// <summary>Source or contractor name for this channel.</summary>
    public required string Source { get; init; }

    /// <summary>Measurement class (e.g., angular velocity).</summary>
    public required string MeasureClass { get; init; }

    /// <summary>Optional UUID. May be <see langword="null"/>.</summary>
    public string? Uuid { get; init; }

    /// <summary>Optional custom metadata supplied by the producer. Never null; may be empty.</summary>
    public IReadOnlyDictionary<string, string> CustomData { get; init; } =
        new Dictionary<string, string>();
}

// ── Live Streaming (User Story 2) ─────────────────────────────────────────────

/// <summary>Represents a request to subscribe to one channel for live streaming.</summary>
public sealed class ChannelSubscriptionInfo
{
    /// <summary>Channel ID obtained from a prior <c>ChannelDescribe</c>.</summary>
    public long ChannelId { get; }

    /// <summary>
    /// <see langword="true"/> to stream from the latest value (StartIndex = null).
    /// When <see langword="false"/>, use <see cref="StartIndexValue"/>.
    /// </summary>
    public bool StartLatest { get; }

    /// <summary>Specific primary index value to start from, when <see cref="StartLatest"/> is false.</summary>
    public long? StartIndexValue { get; }

    /// <summary>Whether to receive <c>ChannelDataChange</c> notifications.</summary>
    public bool ReceiveChangeNotifications { get; }

    /// <summary>Creates a subscription starting at the latest value.</summary>
    public ChannelSubscriptionInfo(long channelId, bool startLatest, bool receiveChangeNotifications)
    {
        ChannelId = channelId;
        StartLatest = startLatest;
        ReceiveChangeNotifications = receiveChangeNotifications;
    }

    /// <summary>Creates a subscription starting from a specific index value.</summary>
    public ChannelSubscriptionInfo(long channelId, long startIndexValue, bool receiveChangeNotifications)
    {
        ChannelId = channelId;
        StartLatest = false;
        StartIndexValue = startIndexValue;
        ReceiveChangeNotifications = receiveChangeNotifications;
    }
}

/// <summary>Identifies the kind of a live channel event.</summary>
public enum ChannelEventKind
{
    /// <summary>New data point(s) arrived for one or more channels.</summary>
    Data,

    /// <summary>Existing data points were modified for a channel.</summary>
    DataChange,

    /// <summary>The status of a channel changed.</summary>
    StatusChange,

    /// <summary>A channel was removed and will not produce further data.</summary>
    Remove,
}

/// <summary>
/// One decoded event received from a Protocol 1 producer during live streaming.
/// </summary>
public sealed class ChannelEvent
{
    /// <summary>The kind of event.</summary>
    public required ChannelEventKind Kind { get; init; }

    /// <summary>
    /// For <see cref="ChannelEventKind.Data"/>: the data items received.
    /// Empty for other event kinds.
    /// </summary>
    public IReadOnlyList<ChannelDataItem> DataItems { get; init; } = [];

    /// <summary>
    /// For <see cref="ChannelEventKind.DataChange"/>: the channel ID affected.
    /// 0 for other event kinds.
    /// </summary>
    public long ChannelId { get; init; }

    /// <summary>For <see cref="ChannelEventKind.DataChange"/>: start index of the changed range.</summary>
    public long StartIndex { get; init; }

    /// <summary>For <see cref="ChannelEventKind.DataChange"/>: end index of the changed range.</summary>
    public long EndIndex { get; init; }

    /// <summary>
    /// For <see cref="ChannelEventKind.StatusChange"/>: the new status string.
    /// <c>null</c> for other event kinds.
    /// </summary>
    public string? NewStatus { get; init; }

    /// <summary>
    /// For <see cref="ChannelEventKind.Remove"/>: optional human-readable reason.
    /// <c>null</c> when not provided.
    /// </summary>
    public string? RemoveReason { get; init; }
}

/// <summary>
/// One decoded data point from a <c>ChannelData</c> Protocol 1 message.
/// </summary>
public sealed class ChannelDataItem
{
    /// <summary>Primary (and optionally secondary) index values for this data point.</summary>
    public required IReadOnlyList<long> Indexes { get; init; }

    /// <summary>Producer-assigned channel ID this data point belongs to.</summary>
    public required long ChannelId { get; init; }

    /// <summary>
    /// Decoded channel value. May be <see langword="null"/>, <see cref="double"/>,
    /// <see cref="float"/>, <see cref="int"/>, <see cref="long"/>, <see cref="string"/>,
    /// <see cref="bool"/>, or <see langword="byte"/>[] depending on the channel's data type.
    /// </summary>
    public required object? Value { get; init; }
}

// ── Range Request (User Story 3) ───────────────────────────────────────────────

/// <summary>Identifies the outcome state of a bounded range request.</summary>
public enum ChannelRangeResultState
{
    /// <summary>All parts were received and the result is complete.</summary>
    Completed,

    /// <summary>
    /// The multipart response was interrupted by a reconnect;
    /// the result must not be treated as complete.
    /// </summary>
    IncompleteAfterReconnect,

    /// <summary>The server rejected the request.</summary>
    Failed,
}

/// <summary>
/// Represents one bounded historical data request for one or more channels.
/// All channels must share a common index type, UOM, and direction.
/// </summary>
public sealed class ChannelRangeRequestModel
{
    /// <summary>Channel IDs for which historical data is requested.</summary>
    public required IReadOnlyList<long> ChannelIds { get; init; }

    /// <summary>Primary index start value (inclusive).</summary>
    public required long FromIndex { get; init; }

    /// <summary>Primary index end value (inclusive).</summary>
    public required long ToIndex { get; init; }
}

/// <summary>
/// The complete result of one <c>ChannelRangeRequest</c> operation.
/// </summary>
public sealed class ChannelRangeResult
{
    /// <summary>The request that produced this result.</summary>
    public required ChannelRangeRequestModel Request { get; init; }

    /// <summary>Data items returned for the requested range, in response order.</summary>
    public required IReadOnlyList<ChannelDataItem> Samples { get; init; }

    /// <summary><see langword="true"/> when the response arrived in multiple parts.</summary>
    public required bool WasMultipart { get; init; }

    /// <summary>Result outcome.</summary>
    public required ChannelRangeResultState State { get; init; }
}

// ── Wire helpers (internal) ────────────────────────────────────────────────────

/// <summary>Internal wire representation of a ChannelRangeInfo for encoding.</summary>
internal sealed class ChannelRangeInfoWire
{
    public IReadOnlyList<long> ChannelIds { get; }
    public long StartIndex { get; }
    public long EndIndex { get; }

    public ChannelRangeInfoWire(IReadOnlyList<long> channelIds, long startIndex, long endIndex)
    {
        ChannelIds = channelIds;
        StartIndex = startIndex;
        EndIndex = endIndex;
    }
}

// ── Exceptions ─────────────────────────────────────────────────────────────────

/// <summary>
/// Thrown when a Protocol 1 ChannelStreaming request fails at the protocol level.
/// The <see cref="Exception.Message"/> is guaranteed to be secret-safe.
/// </summary>
public sealed class EtpChannelStreamingException : Exception
{
    /// <summary>The URI(s) or channel identifiers involved in the failed request.</summary>
    public string RequestedTarget { get; }

    /// <summary>ETP protocol error code returned by the server, when available.</summary>
    public int? EtpErrorCode { get; }

    /// <summary>
    /// Constructs a new <see cref="EtpChannelStreamingException"/>.
    /// Do NOT include credential values in <paramref name="message"/>.
    /// </summary>
    public EtpChannelStreamingException(
        string message,
        string requestedTarget,
        int? etpErrorCode = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        RequestedTarget = requestedTarget;
        EtpErrorCode = etpErrorCode;
    }
}
