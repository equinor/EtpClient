using System.Runtime.CompilerServices;
using EtpClient.Models;

namespace EtpExplorer.Tests.TestSupport;

/// <summary>
/// Fake implementation of <see cref="IExplorerClient"/> for explorer tests.
/// Allows tests to control what results are returned and what exceptions are thrown.
/// </summary>
public sealed class FakeExplorerClient : IExplorerClient
{
    private EtpConnectionState _state = EtpConnectionState.Closed;

    // ── Configurable results ──────────────────────────────────────────────────

    public EtpConnectionResult? ConnectResult { get; set; }
    public Exception? ConnectException { get; set; }

    public DiscoveryResult? DiscoverResult { get; set; }
    public Exception? DiscoverException { get; set; }
    public Dictionary<string, DiscoveryResult> DiscoverResultsByUri { get; } = new();

    public ChannelDescriptionResult? DescribeResult { get; set; }
    public Exception? DescribeException { get; set; }
    public Dictionary<string, ChannelDescriptionResult> DescribeResultsByUri { get; } = new();

    /// <summary>Channel events to yield from <see cref="StartChannelStreamingAsync"/>.</summary>
    public IReadOnlyList<EtpClient.Models.ChannelEvent> StreamEvents { get; set; } = [];

    // ── Captured calls ────────────────────────────────────────────────────────

    public int ConnectCallCount { get; private set; }
    public int DiscoverCallCount { get; private set; }
    public List<string> DiscoveredUris { get; } = new();
    public int DescribeCallCount { get; private set; }
    public List<IReadOnlyList<string>> DescribedUris { get; } = new();
    public int StartStreamingCallCount { get; private set; }
    public int StopStreamingCallCount { get; private set; }
    public int CloseCallCount { get; private set; }
    public bool WasDisposed { get; private set; }

    // ── IExplorerClient ───────────────────────────────────────────────────────

    public EtpConnectionState State => _state;

    public Task<EtpConnectionResult> ConnectAsync(EtpConnectionOptions options, CancellationToken ct = default)
    {
        ConnectCallCount++;
        if (ConnectException is not null) throw ConnectException;

        _state = EtpConnectionState.Connected;
        return Task.FromResult(ConnectResult ?? BuildDefaultConnectResult());
    }

    public Task<DiscoveryResult> DiscoverResourcesAsync(string uri, CancellationToken ct = default)
    {
        DiscoverCallCount++;
        DiscoveredUris.Add(uri);
        if (DiscoverException is not null) throw DiscoverException;

        if (DiscoverResultsByUri.TryGetValue(uri, out var mappedResult))
            return Task.FromResult(mappedResult);

        return Task.FromResult(DiscoverResult ?? BuildEmptyDiscoveryResult(uri));
    }

    public Task<ChannelDescriptionResult> DescribeChannelsAsync(IReadOnlyList<string> uris, CancellationToken ct = default)
    {
        DescribeCallCount++;
        DescribedUris.Add(uris);
        if (DescribeException is not null) throw DescribeException;

        if (uris.Count == 1 && DescribeResultsByUri.TryGetValue(uris[0], out var mappedResult))
            return Task.FromResult(mappedResult);

        return Task.FromResult(DescribeResult ?? BuildEmptyDescribeResult(uris));
    }

    public async IAsyncEnumerable<EtpClient.Models.ChannelEvent> StartChannelStreamingAsync(
        IReadOnlyList<ChannelSubscriptionInfo> subscriptions,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        StartStreamingCallCount++;
        foreach (var evt in StreamEvents)
        {
            ct.ThrowIfCancellationRequested();
            yield return evt;
        }
    }

    public Task StopChannelStreamingAsync(IReadOnlyList<long> channelIds, CancellationToken ct = default)
    {
        StopStreamingCallCount++;
        return Task.CompletedTask;
    }

    public Task CloseAsync(CancellationToken ct = default)
    {
        CloseCallCount++;
        _state = EtpConnectionState.Closed;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        WasDisposed = true;
        return ValueTask.CompletedTask;
    }

    // ── Factory helpers ───────────────────────────────────────────────────────

    public static EtpConnectionResult BuildDefaultConnectResult() => new()
    {
        Session = new NegotiatedSessionInfo
        {
            ServerApplicationName = "FakeServer",
            ServerApplicationVersion = "1.0",
            ServerInstanceId = Guid.NewGuid(),
            SupportedProtocols = [],
            SupportedCompression = string.Empty,
            SupportedFormats = [],
        },
        ConnectedAtUtc = DateTimeOffset.UtcNow,
        EndpointHost = "fake-host",
        MessageEncoding = EtpMessageEncoding.Binary,
    };

    public static DiscoveryResult BuildEmptyDiscoveryResult(string uri) => new()
    {
        RequestedUri = uri,
        Resources = [],
        WasEmptyAcknowledged = true,
        MessageEncoding = EtpMessageEncoding.Binary,
    };

    public static DiscoveryResult BuildDiscoveryResult(string requestedUri, IReadOnlyList<DiscoveredResource> resources) => new()
    {
        RequestedUri = requestedUri,
        Resources = resources,
        WasEmptyAcknowledged = false,
        MessageEncoding = EtpMessageEncoding.Binary,
    };

    public static DiscoveredResource BuildResource(string uri, string name, string resourceType = "Folder", bool channelSubscribable = false, int hasChildren = 1) => new()
    {
        Uri = uri,
        Name = name,
        ContentType = string.Empty,
        ResourceType = resourceType,
        HasChildren = hasChildren,
        ChannelSubscribable = channelSubscribable,
        ObjectNotifiable = false,
    };

    public static ChannelDescriptionResult BuildEmptyDescribeResult(IReadOnlyList<string> uris) => new()
    {
        RequestedUris = uris,
        Channels = [],
        MessageEncoding = EtpMessageEncoding.Binary,
        WasMultipart = false,
        State = ChannelDescriptionState.Completed,
    };

    public static ChannelDescriptionResult BuildDescribeResult(IReadOnlyList<string> uris, IReadOnlyList<ChannelDefinition> channels) => new()
    {
        RequestedUris = uris,
        Channels = channels,
        MessageEncoding = EtpMessageEncoding.Binary,
        WasMultipart = false,
        State = ChannelDescriptionState.Completed,
    };

    public static ChannelDefinition BuildChannel(long id, string uri, string name, string status = "Active") => new()
    {
        ChannelId = id,
        ChannelUri = uri,
        ChannelName = name,
        DataType = "double",
        Uom = "unitless",
        IndexType = "Time",
        IndexUom = "ms",
        IndexDirection = "Increasing",
        Description = name,
        Status = status,
        Source = "fake",
        MeasureClass = "unknown",
    };
}
