using EtpClient.Models;

namespace EtpClient.UnitTests.Models;

/// <summary>
/// Unit tests verifying that <see cref="EtpConnectionOptions.RequestedProtocols"/>
/// includes Protocol 3 (Discovery) in the default negotiation set.
/// T005 [Foundational]: Validates Discovery protocol opt-in.
/// </summary>
public sealed class EtpConnectionOptionsDiscoveryTests
{
    // Access the default protocols through a default-constructed options instance
    private static IReadOnlyList<SupportedProtocol> DefaultProtocols =>
        new EtpConnectionOptions(
            new Uri("wss://example.com/etp"), "user", "pass")
            .RequestedProtocols;

    [Fact]
    public void DefaultRequestedProtocols_IncludesDiscoveryProtocol()
    {
        Assert.Contains(DefaultProtocols, p => p.Protocol == 3);
    }

    [Fact]
    public void DefaultRequestedProtocols_DiscoveryRoleIsStore()
    {
        var discovery = DefaultProtocols.FirstOrDefault(p => p.Protocol == 3);

        Assert.NotNull(discovery);
        Assert.Equal("store", discovery.Role);
    }

    [Fact]
    public void DefaultRequestedProtocols_IncludesChannelStreamingProtocol()
    {
        // Regression guard: Protocol 1 (ChannelStreaming) must still be present
        Assert.Contains(DefaultProtocols, p => p.Protocol == 1);
    }

    [Fact]
    public void DefaultRequestedProtocols_HasAtLeastTwoProtocols()
    {
        Assert.True(DefaultProtocols.Count >= 2,
            $"Expected at least 2 protocols but found {DefaultProtocols.Count}");
    }
}
