using EtpClient.Models;
using Microsoft.Extensions.Logging;

namespace EtpExplorer;

/// <summary>
/// Resolves streamable channel endpoints from a <see cref="ChannelDescriptionResult"/>.
/// Filters out channels with closed or non-streamable status.
/// </summary>
public sealed class ExplorerEndpointResolver
{
    private readonly ILogger<ExplorerEndpointResolver> _logger;

    public ExplorerEndpointResolver(ILogger<ExplorerEndpointResolver> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Maps channel definitions from a describe result to <see cref="ResolvedStreamableEndpoint"/> records.
    /// Only channels with a non-closed status and valid channel IDs are included.
    /// </summary>
    /// <param name="describeResult">Result of a <c>ChannelDescribe</c> operation.</param>
    /// <param name="sourceResourceUri">The resource URI that was described.</param>
    public IReadOnlyList<ResolvedStreamableEndpoint> ResolveEndpoints(
        ChannelDescriptionResult describeResult,
        string sourceResourceUri)
    {
        return describeResult.Channels
            .Where(ch => !string.Equals(ch.Status, "Closed", StringComparison.OrdinalIgnoreCase))
            .Select(ch => new ResolvedStreamableEndpoint
            {
                SourceResourceUri = sourceResourceUri,
                ChannelId = ch.ChannelId,
                ChannelUri = ch.ChannelUri,
                ChannelName = ch.ChannelName,
                DataType = ch.DataType,
                IndexType = ch.IndexType,
                Status = ch.Status,
            })
            .ToList();
    }
}
