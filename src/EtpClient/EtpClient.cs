using EtpClient.Connection;
using EtpClient.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EtpClient;

/// <summary>
/// Minimal ETP client that can open an authenticated session with an ETP server
/// using WebSocket + Basic authentication and complete the Protocol 0 handshake.
/// </summary>
/// <example>
/// <code lang="csharp">
/// await using var client = new EtpClient(logger);
/// var options = new EtpConnectionOptions(new Uri("wss://server/etp"), "user", "pass");
/// var result = await client.ConnectAsync(options);
/// Console.WriteLine($"Connected: {result.Session.ServerApplicationName}");
/// await client.CloseAsync();
/// </code>
/// </example>
public sealed class EtpClient : IAsyncDisposable
{
    private readonly Func<IWebSocketTransport> _transportFactory;
    private readonly ILogger _logger;

    private EtpSessionManager? _manager;

    /// <summary>Gets the current connection state.</summary>
    public EtpConnectionState State => _manager?.State ?? EtpConnectionState.Closed;

    /// <summary>
    /// Creates a new <see cref="EtpClient"/> using the production WebSocket transport.
    /// </summary>
    /// <param name="logger">Optional logger; uses <see cref="NullLogger"/> when omitted.</param>
    public EtpClient(ILogger<EtpClient>? logger = null)
        : this(() => new ClientWebSocketTransport(), (ILogger?)logger)
    {
    }

    /// <summary>Internal ctor for unit/integration testing with a custom transport factory.</summary>
    internal EtpClient(Func<IWebSocketTransport> transportFactory, ILogger? logger = null)
    {
        _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// Opens a WebSocket connection to the ETP server, attaches Basic authentication,
    /// and completes the ETP Protocol 0 RequestSession/OpenSession handshake.
    /// </summary>
    /// <param name="options">Connection configuration. Must not be <see langword="null"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="EtpConnectionResult"/> containing the negotiated session details.
    /// </returns>
    /// <exception cref="EtpConnectionException">
    /// Thrown when authentication fails, the server rejects the session, or a transport error occurs.
    /// </exception>
    public async Task<EtpConnectionResult> ConnectAsync(
        EtpConnectionOptions options,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Create a fresh transport + manager for each connection attempt
        var transport = _transportFactory();
        _manager = new EtpSessionManager(transport, _logger);

        return await _manager.ConnectAsync(options, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Discovers immediate child resources of the specified ETP URI using Protocol 3 (Discovery).
    /// </summary>
    /// <param name="uri">
    /// The ETP URI whose children are to be discovered (e.g. <c>"eml://"</c> for the root).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="DiscoveryResult"/> containing the list of discovered resources plus
    /// metadata about whether the server acknowledged an empty result.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the session is not in the <see cref="EtpConnectionState.Connected"/> state.
    /// Call <see cref="ConnectAsync"/> first.
    /// </exception>
    /// <exception cref="EtpDiscoveryException">
    /// Thrown when the server returns a <c>ProtocolException</c> or an unexpected message
    /// during the discovery exchange.
    /// </exception>
    public Task<DiscoveryResult> DiscoverResourcesAsync(string uri, CancellationToken ct = default)
    {
        if (_manager is null || State != EtpConnectionState.Connected)
            throw new InvalidOperationException("Discovery requires an active Connected session.");

        return _manager.DiscoverResourcesAsync(uri, ct);
    }

    /// <summary>
    /// Sends a WebSocket close frame and transitions to <see cref="EtpConnectionState.Closed"/>.
    /// Safe to call when already closed.
    /// </summary>
    public Task CloseAsync(CancellationToken ct = default)
    {
        if (_manager is null)
            return Task.CompletedTask;

        return _manager.CloseAsync(ct);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);

        if (_manager is not null)
        {
            // Transport is owned by the manager — disposing manager disposes its transport
            // (IWebSocketTransport : IAsyncDisposable handled by ClientWebSocketTransport)
        }
    }
}
