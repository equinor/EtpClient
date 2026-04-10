using EtpClient.Models;

namespace EtpClient.UnitTests.Models;

/// <summary>
/// Unit tests for encoding-related behavior of <see cref="EtpConnectionOptions"/>.
/// T011 [US1]: Validates that the default encoding and explicit encoding selection work correctly.
/// </summary>
public sealed class EtpConnectionOptionsEncodingTests
{
    private static readonly Uri DefaultUri = new("wss://example.com/etp");

    [Fact]
    public void DefaultEncoding_IsBinary()
    {
        var options = new EtpConnectionOptions(DefaultUri, "user", "pass");

        Assert.Equal(EtpMessageEncoding.Binary, options.MessageEncoding);
        // Protocol 1 (ChannelStreaming, consumer) must still be in the default set
        var proto1 = options.RequestedProtocols.SingleOrDefault(p => p.Protocol == 1);
        Assert.NotNull(proto1);
        Assert.Equal(ProtocolVersion.Etp11, proto1.Version);
        Assert.Equal("consumer", proto1.Role);
    }

    [Fact]
    public void BinaryEncoding_IsPreservedWhenSet()
    {
        var options = new EtpConnectionOptions(
            DefaultUri, "user", "pass",
            messageEncoding: EtpMessageEncoding.Binary);

        Assert.Equal(EtpMessageEncoding.Binary, options.MessageEncoding);
    }

    [Fact]
    public void JsonEncoding_IsPreservedWhenSet()
    {
        var options = new EtpConnectionOptions(
            DefaultUri, "user", "pass",
            messageEncoding: EtpMessageEncoding.Json);

        Assert.Equal(EtpMessageEncoding.Json, options.MessageEncoding);
    }

    [Fact]
    public void EncodingIsOrthogonalToOtherOptions_DoesNotAffectUri()
    {
        var options = new EtpConnectionOptions(
            DefaultUri, "user", "pass",
            messageEncoding: EtpMessageEncoding.Json);

        // Other properties remain unaffected
        Assert.Equal(DefaultUri, options.EndpointUri);
        Assert.Equal("user", options.Username);
    }

    [Fact]
    public void ExplicitClientInstanceId_IsPreservedWithJsonEncoding()
    {
        var id = Guid.NewGuid();
        var options = new EtpConnectionOptions(
            DefaultUri, "user", "pass",
            clientInstanceId: id,
            messageEncoding: EtpMessageEncoding.Json);

        Assert.Equal(id, options.ClientInstanceId);
        Assert.Equal(EtpMessageEncoding.Json, options.MessageEncoding);
    }
}
