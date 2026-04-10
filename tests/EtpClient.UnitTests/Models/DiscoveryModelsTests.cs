using EtpClient.Models;

namespace EtpClient.UnitTests.Models;

/// <summary>
/// Unit tests for <see cref="DiscoveryResult"/>, <see cref="DiscoveredResource"/>,
/// and <see cref="EtpDiscoveryException"/>.
/// T009 [US1, US2]: Validates model properties and state computation.
/// </summary>
public sealed class DiscoveryModelsTests
{
    // ── DiscoveryResult.State ─────────────────────────────────────────────────

    [Fact]
    public void State_WhenResourcesPresent_IsCompletedWithResources()
    {
        var result = new DiscoveryResult
        {
            RequestedUri = "eml://",
            Resources = [CreateResource("eml://witsml20")],
            WasEmptyAcknowledged = false,
            MessageEncoding = EtpMessageEncoding.Binary,
        };

        Assert.Equal(DiscoveryResultState.CompletedWithResources, result.State);
    }

    [Fact]
    public void State_WhenAcknowledgedEmpty_IsCompletedEmpty()
    {
        var result = new DiscoveryResult
        {
            RequestedUri = "eml://",
            Resources = [],
            WasEmptyAcknowledged = true,
            MessageEncoding = EtpMessageEncoding.Binary,
        };

        Assert.Equal(DiscoveryResultState.CompletedEmpty, result.State);
    }

    [Fact]
    public void State_WhenEmptyAndNotAcknowledged_IsCompletedEmpty()
    {
        // Edge case: resources is empty but WasEmptyAcknowledged is false.
        // Should still report CompletedEmpty rather than crashing.
        var result = new DiscoveryResult
        {
            RequestedUri = "eml://",
            Resources = [],
            WasEmptyAcknowledged = false,
            MessageEncoding = EtpMessageEncoding.Binary,
        };

        Assert.Equal(DiscoveryResultState.CompletedEmpty, result.State);
    }

    // ── EtpDiscoveryException ─────────────────────────────────────────────────

    [Fact]
    public void EtpDiscoveryException_StoresRequestedUri()
    {
        const string uri = "eml://bad/uri";
        var ex = new EtpDiscoveryException("Discovery failed", uri);

        Assert.Equal(uri, ex.RequestedUri);
    }

    [Fact]
    public void EtpDiscoveryException_StoresEtpErrorCode()
    {
        var ex = new EtpDiscoveryException("Discovery failed", "eml://", etpErrorCode: 1003);

        Assert.Equal(1003, ex.EtpErrorCode);
    }

    [Fact]
    public void EtpDiscoveryException_WhenNoErrorCode_IsNull()
    {
        var ex = new EtpDiscoveryException("Discovery failed", "eml://");

        Assert.Null(ex.EtpErrorCode);
    }

    // ── DiscoveredResource ────────────────────────────────────────────────────

    [Fact]
    public void DiscoveredResource_CustomData_DefaultsToEmptyDictionary()
    {
        // CustomData is optional — if not set, must not be null
        var resource = new DiscoveredResource
        {
            Uri = "eml://witsml20/well(x)",
            ContentType = "",
            Name = "Well X",
            ResourceType = "DataObject",
            HasChildren = 0,
            ChannelSubscribable = false,
            ObjectNotifiable = false,
        };

        Assert.NotNull(resource.CustomData);
        Assert.Empty(resource.CustomData);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static DiscoveredResource CreateResource(string uri) =>
        new()
        {
            Uri = uri,
            ContentType = "application/x-witsml",
            Name = "Root",
            ResourceType = "UriProtocol",
            HasChildren = 1,
            ChannelSubscribable = false,
            ObjectNotifiable = false,
        };
}
