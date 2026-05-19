using EtpClient.Models;
using Microsoft.Extensions.Logging;

namespace EtpClient.SampleConsole;

/// <summary>
/// Orchestrates the ETP sample run: validates configuration, connects using the public
/// <see cref="IEtpConnector"/> API, writes a success/failure summary, and shuts down cleanly.
/// </summary>
public sealed class SampleConsoleRunner
{
    private readonly SampleConsoleOptions _options;
    private readonly Func<IEtpConnector> _connectorFactory;
    private readonly SampleOutputWriter _outputWriter;
    private readonly ILogger<SampleConsoleRunner> _logger;

    /// <summary>
    /// Creates a runner with the production connector factory (uses real WebSocket transport).
    /// </summary>
    public SampleConsoleRunner(
        SampleConsoleOptions options,
        SampleOutputWriter outputWriter,
        ILogger<SampleConsoleRunner> logger)
        : this(options, () => new EtpConnector(), outputWriter, logger)
    {
    }

    /// <summary>
    /// Creates a runner with an injectable connector factory (for testing).
    /// </summary>
    public SampleConsoleRunner(
        SampleConsoleOptions options,
        Func<IEtpConnector> connectorFactory,
        SampleOutputWriter outputWriter,
        ILogger<SampleConsoleRunner> logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _connectorFactory = connectorFactory ?? throw new ArgumentNullException(nameof(connectorFactory));
        _outputWriter = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the sample: validates configuration, opens an ETP session, and reports the result.
    /// </summary>
    /// <param name="ct">Cancellation token for clean shutdown.</param>
    /// <returns>A <see cref="SampleRunOutcome"/> describing the run result.</returns>
    public async Task<SampleRunOutcome> RunAsync(CancellationToken ct = default)
    {
        // Validate configuration before attempting any network operation
        var validationError = _options.Validate();
        if (validationError is not null)
        {
            var validationOutcome = SampleRunOutcome.FromValidationError(validationError);
            _outputWriter.WriteFailure(validationOutcome);
            return validationOutcome;
        }

        var connectionOptions = _options.ToConnectionOptions();
        var endpointHost = connectionOptions.EndpointUri.Host;

        await using var connector = _connectorFactory();

        try
        {
            _logger.LogInformation("Connecting to {EndpointHost}...", endpointHost);
            var result = await connector.ConnectAsync(connectionOptions, ct).ConfigureAwait(false);

            DiscoveryResult? discoveryResult = null;
            ChannelDescriptionResult? channelDescriptionResult = null;

            if (_options.SkipDiscovery)
            {
                _logger.LogInformation("Skipping discovery because it is disabled in the sample configuration.");
            }
            else if (result.Session.SupportsDiscovery)
            {
                var uri = "eml://witsml14/well(93c581f9-f11b-49a2-9cdb-ca52787bc628)/wellbore(45b3380b-8cb6-46b7-a510-28d6a842e1c8)/log(MSP_Surface_Time_VLOG)";
                _logger.LogInformation($"Discovering resources at {uri}...");
                try
                {
                    discoveryResult = await connector.DiscoverResourcesAsync(uri, ct).ConfigureAwait(false);
                }
                catch (EtpDiscoveryException ex)
                {
                    _logger.LogWarning("Discovery failed: {Message}", ex.Message);
                }
            }
            else
            {
                _logger.LogInformation("Skipping discovery because the server did not negotiate Protocol 3 (Discovery).");
            }

            if (!string.IsNullOrWhiteSpace(_options.ChannelUri) && !result.Session.SupportsChannelStreaming)
            {
                _logger.LogInformation(
                    "Skipping channel describe because the server did not negotiate Protocol 1 (ChannelStreaming).");
            }
            else if (!string.IsNullOrWhiteSpace(_options.ChannelUri))
            {
                _logger.LogInformation("Describing channels for {ChannelUri}...", _options.ChannelUri);
                using var describeCts = CreateProtocolRequestCancellationTokenSource(ct);
                try
                {
                    channelDescriptionResult = await connector.DescribeChannelsAsync(
                        [_options.ChannelUri], describeCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Channel describe timed out after {TimeoutSeconds} second(s). The server may not support Protocol 1 for this URI or did not respond.",
                        _options.ProtocolRequestTimeoutSeconds);
                }
                catch (EtpChannelStreamingException ex)
                {
                    _logger.LogWarning("Channel describe failed: {Message}", ex.Message);
                }
            }

            var streamableChannels = channelDescriptionResult?.Channels
                .Where(IsStreamableChannel)
                .ToList();

            if (channelDescriptionResult is { Channels.Count: > 0 } && streamableChannels is { Count: 0 })
            {
                _logger.LogInformation(
                    "Skipping live streaming and range requests because the described channels are not active session-registered channels.");
            }

            LiveStreamingResult? liveStreamingResult = null;
            if (streamableChannels is { Count: > 0 })
            {
                var channelsById = streamableChannels.ToDictionary(channel => channel.ChannelId);
                var subscriptions = streamableChannels
                    .Select(ch => new ChannelSubscriptionInfo(ch.ChannelId, startLatest: true, receiveChangeNotifications: false))
                    .ToList();
                _logger.LogInformation("Starting live channel streaming for {Count} channel(s)...", subscriptions.Count);
                try
                {
                    var eventsReceived = 0;
                    var endedByRemove = false;
                    await foreach (var ev in connector.StartChannelStreamingAsync(subscriptions, ct).ConfigureAwait(false))
                    {
                        eventsReceived++;
                        if (ev.Kind == ChannelEventKind.Data && ev.DataItems.Count > 0)
                            _outputWriter.WriteLiveData(ev.DataItems, channelsById);

                        if (ev.Kind == ChannelEventKind.Remove)
                        {
                            endedByRemove = true;
                            break;
                        }
                    }
                    liveStreamingResult = new LiveStreamingResult
                    {
                        SubscribedChannelIds = subscriptions.Select(s => s.ChannelId).ToList(),
                        EventsReceived = eventsReceived,
                        EndedByRemove = endedByRemove,
                    };
                    _logger.LogInformation("Streaming ended: {EventsReceived} event(s) received.", eventsReceived);
                }
                catch (EtpChannelStreamingException ex)
                {
                    _logger.LogWarning("Live streaming failed: {Message}", ex.Message);
                }
            }

            ChannelRangeRequestModel? rangeRequest = null;
            IReadOnlyList<ChannelDataItem>? rangeSamples = null;
            if (streamableChannels is { Count: > 0 }
                && _options.ChannelRangeFromIndex.HasValue
                && _options.ChannelRangeToIndex.HasValue)
            {
                rangeRequest = new ChannelRangeRequestModel
                {
                    ChannelIds = streamableChannels.Select(ch => ch.ChannelId).ToList(),
                    FromIndex = _options.ChannelRangeFromIndex.Value,
                    ToIndex = _options.ChannelRangeToIndex.Value,
                };
                _logger.LogInformation("Requesting channel range [{From}, {To}]...",
                    _options.ChannelRangeFromIndex.Value, _options.ChannelRangeToIndex.Value);
                using var rangeCts = CreateProtocolRequestCancellationTokenSource(ct);
                try
                {
                    var samples = new List<ChannelDataItem>();
                    await foreach (var item in connector.RequestChannelRangeAsync(rangeRequest, rangeCts.Token).ConfigureAwait(false))
                        samples.Add(item);
                    rangeSamples = samples;
                    _logger.LogInformation("Range request returned {Count} sample(s).", rangeSamples.Count);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning(
                        "Channel range request timed out after {TimeoutSeconds} second(s).",
                        _options.ProtocolRequestTimeoutSeconds);
                }
                catch (EtpChannelStreamingException ex)
                {
                    _logger.LogWarning("Range request failed: {Message}", ex.Message);
                }
            }

            var successOutcome = SampleRunOutcome.FromSuccess(result, discoveryResult, channelDescriptionResult, liveStreamingResult, rangeRequest, rangeSamples);
            _outputWriter.WriteSuccess(successOutcome, _options.ShowSessionDetails);
            _outputWriter.WriteDiscovery(successOutcome);
            _outputWriter.WriteChannelDescription(successOutcome);
            _outputWriter.WriteLiveStreaming(successOutcome);
            _outputWriter.WriteChannelRange(successOutcome);
            _logger.LogInformation("Closing session...");
            await connector.CloseAsync(ct).ConfigureAwait(false);

            return successOutcome;
        }
        catch (EtpConnectionException ex)
        {
            var failureOutcome = SampleRunOutcome.FromException(ex, endpointHost);
            _outputWriter.WriteFailure(failureOutcome);
            return failureOutcome;
        }
        catch (OperationCanceledException)
        {
            var canceledOutcome = SampleRunOutcome.FromException(
                new EtpConnectionException(
                    EtpConnectionFailureCategory.Cancellation,
                    "The operation was canceled."),
                endpointHost);
            _outputWriter.WriteFailure(canceledOutcome);
            return canceledOutcome;
        }
    }

    private CancellationTokenSource CreateProtocolRequestCancellationTokenSource(CancellationToken ct)
    {
        var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(_options.ProtocolRequestTimeoutSeconds));
        return linkedCts;
    }

    private static bool IsStreamableChannel(ChannelDefinition channel) =>
        channel.ChannelId >= 0 &&
        !string.Equals(channel.Status, "Closed", StringComparison.OrdinalIgnoreCase);
}
