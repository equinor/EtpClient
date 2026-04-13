using EtpExplorer;

namespace EtpExplorer.Tests;

public sealed class ExplorerOptionsTests
{
    // ── Valid options ──────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithAllValid_ReturnsNull()
    {
        var opts = new ExplorerOptions
        {
            EndpointUri = "wss://server/etp",
            Username = "user",
            Password = "pass",
            ProtocolRequestTimeoutSeconds = 10,
        };

        Assert.Null(opts.Validate());
    }

    [Theory]
    [InlineData("ws://server/etp")]
    [InlineData("WS://SERVER/ETP")]
    [InlineData("wss://server/etp")]
    public void Validate_WithValidSchemes_ReturnsNull(string uri)
    {
        var opts = new ExplorerOptions
        {
            EndpointUri = uri,
            Username = "user",
            Password = "pass",
        };

        Assert.Null(opts.Validate());
    }

    // ── Missing required fields ────────────────────────────────────────────────

    [Fact]
    public void Validate_WithMissingEndpointUri_ReturnsMessage()
    {
        var opts = new ExplorerOptions { Username = "u", Password = "p" };
        var msg = opts.Validate();
        Assert.NotNull(msg);
        Assert.Contains("EndpointUri", msg);
        Assert.Contains("user-secrets", msg);
    }

    [Fact]
    public void Validate_WithMissingUsername_ReturnsMessage()
    {
        var opts = new ExplorerOptions { EndpointUri = "wss://x/etp", Password = "p" };
        var msg = opts.Validate();
        Assert.NotNull(msg);
        Assert.Contains("Username", msg);
        Assert.Contains("user-secrets", msg);
    }

    [Fact]
    public void Validate_WithMissingPassword_ReturnsMessage()
    {
        var opts = new ExplorerOptions { EndpointUri = "wss://x/etp", Username = "u" };
        var msg = opts.Validate();
        Assert.NotNull(msg);
        Assert.Contains("Password", msg);
        Assert.Contains("user-secrets", msg);
    }

    // ── Invalid URI format ─────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithNonAbsoluteUri_ReturnsMessage()
    {
        var opts = new ExplorerOptions
        {
            EndpointUri = "not-a-uri",
            Username = "secret-user",
            Password = "secret-pass",
        };

        var msg = opts.Validate();
        Assert.NotNull(msg);
        Assert.DoesNotContain("secret-user", msg);
        Assert.DoesNotContain("secret-pass", msg);
    }

    [Fact]
    public void Validate_WithHttpScheme_ReturnsMessage()
    {
        var opts = new ExplorerOptions
        {
            EndpointUri = "https://server/etp",
            Username = "u",
            Password = "p",
        };

        var msg = opts.Validate();
        Assert.NotNull(msg);
        Assert.Contains("ws", msg);
    }

    // ── Timeout ────────────────────────────────────────────────────────────────

    [Fact]
    public void Validate_WithZeroTimeout_ReturnsMessage()
    {
        var opts = new ExplorerOptions
        {
            EndpointUri = "wss://x/etp",
            Username = "u",
            Password = "p",
            ProtocolRequestTimeoutSeconds = 0,
        };

        var msg = opts.Validate();
        Assert.NotNull(msg);
        Assert.Contains("ProtocolRequestTimeoutSeconds", msg);
    }

    // ── Secrets must never be echoed ──────────────────────────────────────────

    [Fact]
    public void Validate_ErrorMessages_NeverEchoCredentials()
    {
        const string user = "secret-user-xyz";
        const string pass = "secret-pass-xyz";

        var opts = new ExplorerOptions
        {
            EndpointUri = "https://bad-scheme",
            Username = user,
            Password = pass,
        };

        var msg = opts.Validate()!;
        Assert.DoesNotContain(user, msg);
        Assert.DoesNotContain(pass, msg);
    }
}
