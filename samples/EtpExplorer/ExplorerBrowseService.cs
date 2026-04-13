using EtpClient.Models;
using Microsoft.Extensions.Logging;

namespace EtpExplorer;

/// <summary>
/// Maps ETP Discovery results to <see cref="BrowseableResource"/> representations
/// and resolves root nodes from a top-level discovery pass.
/// </summary>
public sealed class ExplorerBrowseService
{
    private readonly ILogger<ExplorerBrowseService> _logger;

    public ExplorerBrowseService(ILogger<ExplorerBrowseService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Extracts root node options from a top-level <c>eml://</c> discovery result.
    /// Each resource at the top level becomes a selectable root node.
    /// </summary>
    public IReadOnlyList<RootNodeOption> MapRootNodes(DiscoveryResult result)
    {
        if (result.Resources.Count == 0)
            return [];

        return result.Resources
            .Select(r => new RootNodeOption
            {
                Name = r.Name is { Length: > 0 } ? r.Name : r.Uri,
                Uri = r.Uri,
                Description = r.ContentType is { Length: > 0 } ? r.ContentType : null,
            })
            .ToList();
    }

    /// <summary>
    /// Maps a <see cref="DiscoveryResult"/> to a list of <see cref="BrowseableResource"/> records.
    /// </summary>
    /// <param name="result">Discovery result for the current URI.</param>
    /// <param name="parentUri">Parent URI for back-navigation.</param>
    /// <param name="depth">Current browse depth.</param>
    public IReadOnlyList<BrowseableResource> MapResources(
        DiscoveryResult result,
        string? parentUri = null,
        int depth = 0)
    {
        return result.Resources
            .Select(r => new BrowseableResource
            {
                Uri = r.Uri,
                Name = r.Name is { Length: > 0 } ? r.Name : r.Uri,
                ResourceType = r.ResourceType,
                ContentType = r.ContentType,
                HasChildren = r.HasChildren,
                ChannelSubscribable = r.ChannelSubscribable,
                Depth = depth,
                ParentUri = parentUri,
            })
            .ToList();
    }
}
