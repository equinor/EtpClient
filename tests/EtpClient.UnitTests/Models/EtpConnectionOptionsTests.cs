using EtpClient.Models;
using Xunit;

namespace EtpClient.UnitTests.Models;

public sealed class EtpConnectionOptionsTests
{
    [Fact]
    public void ValidOptions_DoNotThrow()
    {
        var options = new EtpConnectionOptions(
            endpointUri: new Uri("wss://example.com/etp"),
            username: "user",
            password: "pass");

        Assert.Equal("wss", options.EndpointUri.Scheme);
        Assert.Equal("user", options.Username);
    }

    [Fact]
    public void WsScheme_IsAllowed()
    {
        var options = new EtpConnectionOptions(new Uri("ws://localhost/etp"), "u", "p");
        Assert.Equal("ws", options.EndpointUri.Scheme);
    }

    [Fact]
    public void NullUri_Throws_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EtpConnectionOptions(null!, "user", "pass"));
    }

    [Fact]
    public void RelativeUri_Throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new EtpConnectionOptions(new Uri("/relative", UriKind.Relative), "u", "p"));
    }

    [Theory]
    [InlineData("http")]
    [InlineData("https")]
    [InlineData("ftp")]
    public void NonWsScheme_Throws_ArgumentException(string scheme)
    {
        Assert.Throws<ArgumentException>(() =>
            new EtpConnectionOptions(new Uri($"{scheme}://example.com/etp"), "u", "p"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankUsername_Throws_ArgumentException(string username)
    {
        Assert.Throws<ArgumentException>(() =>
            new EtpConnectionOptions(new Uri("wss://example.com/etp"), username, "pass"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankPassword_Throws_ArgumentException(string password)
    {
        Assert.Throws<ArgumentException>(() =>
            new EtpConnectionOptions(new Uri("wss://example.com/etp"), "user", password));
    }

    [Fact]
    public void DefaultClientInstanceId_IsNonEmpty()
    {
        var options = new EtpConnectionOptions(new Uri("wss://example.com/etp"), "u", "p");
        Assert.NotEqual(Guid.Empty, options.ClientInstanceId);
    }

    [Fact]
    public void ExplicitClientInstanceId_IsPreserved()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var options = new EtpConnectionOptions(new Uri("wss://example.com/etp"), "u", "p",
            clientInstanceId: id);
        Assert.Equal(id, options.ClientInstanceId);
    }
}
