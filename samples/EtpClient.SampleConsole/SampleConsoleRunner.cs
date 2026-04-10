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
            if (result.Session.SupportsDiscovery)
            {
                _logger.LogInformation("Discovering resources at eml://...");
                try
                {
                    discoveryResult = await connector.DiscoverResourcesAsync("eml://", ct).ConfigureAwait(false);
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

            var successOutcome = SampleRunOutcome.FromSuccess(result, discoveryResult);
            _outputWriter.WriteSuccess(successOutcome, _options.ShowSessionDetails);
            _outputWriter.WriteDiscovery(successOutcome);

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
}
