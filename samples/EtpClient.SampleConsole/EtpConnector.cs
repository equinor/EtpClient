using EtpClient.Models;

namespace EtpClient.SampleConsole;

/// <summary>
/// Production <see cref="IEtpConnector"/> implementation wrapping the library <see cref="global::EtpClient.EtpClient"/>.
/// </summary>
public sealed class EtpConnector : IEtpConnector
{
    private readonly global::EtpClient.EtpClient _client = new();

    /// <inheritdoc/>
    public Task<EtpConnectionResult> ConnectAsync(EtpConnectionOptions options, CancellationToken ct) =>
        _client.ConnectAsync(options, ct);

    /// <inheritdoc/>
    public Task CloseAsync(CancellationToken ct) => _client.CloseAsync(ct);

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => _client.DisposeAsync();
}
