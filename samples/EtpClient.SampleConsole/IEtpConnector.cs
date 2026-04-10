using EtpClient.Models;

namespace EtpClient.SampleConsole;

/// <summary>
/// Abstracts the ETP connection lifecycle for the sample runner.
/// Enables unit testing without real network calls.
/// </summary>
public interface IEtpConnector : IAsyncDisposable
{
    /// <summary>Opens an authenticated ETP session.</summary>
    Task<EtpConnectionResult> ConnectAsync(EtpConnectionOptions options, CancellationToken ct);

    /// <summary>Discovers immediate child resources of the specified ETP URI.</summary>
    Task<DiscoveryResult> DiscoverResourcesAsync(string uri, CancellationToken ct);

    /// <summary>Sends a WebSocket close frame and transitions to closed state.</summary>
    Task CloseAsync(CancellationToken ct);
}
