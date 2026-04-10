using EtpClient.SampleConsole.Tests.TestSupport;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// Tests for <see cref="SampleConsoleOptions"/> validation rules (User Story 2).
/// </summary>
public sealed class SampleConsoleOptionsValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingEndpointUri_ReturnsError(string? uri)
    {
        var options = SampleTestData.ValidOptions(endpointUri: uri!);
        var error = options.Validate();
        Assert.NotNull(error);
        Assert.Contains("Etp:EndpointUri", error);
    }

    [Fact]
    public void Validate_RelativeEndpointUri_ReturnsError()
    {
        var options = SampleTestData.ValidOptions(endpointUri: "/relative/path");
        var error = options.Validate();
        Assert.NotNull(error);
        Assert.Contains("Etp:EndpointUri", error);
    }

    [Theory]
    [InlineData("http://host/etp")]
    [InlineData("https://host/etp")]
    [InlineData("ftp://host/etp")]
    public void Validate_WrongScheme_ReturnsError(string uri)
    {
        var options = SampleTestData.ValidOptions(endpointUri: uri);
        var error = options.Validate();
        Assert.NotNull(error);
        Assert.Contains("ws", error);
    }

    [Theory]
    [InlineData("ws://host/etp")]
    [InlineData("wss://host/etp")]
    public void Validate_ValidScheme_DoesNotErrorOnScheme(string uri)
    {
        var options = SampleTestData.ValidOptions(endpointUri: uri);
        var error = options.Validate();
        // Error should be null (assuming username/password also valid)
        Assert.Null(error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingUsername_ReturnsError(string? username)
    {
        var options = SampleTestData.ValidOptions(username: username!);
        var error = options.Validate();
        Assert.NotNull(error);
        Assert.Contains("Etp:Username", error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_MissingPassword_ReturnsError(string? password)
    {
        var options = SampleTestData.ValidOptions(password: password!);
        var error = options.Validate();
        Assert.NotNull(error);
        Assert.Contains("Etp:Password", error);
    }

    [Fact]
    public void Validate_AllValidFields_ReturnsNull()
    {
        var options = SampleTestData.ValidOptions();
        Assert.Null(options.Validate());
    }

    [Fact]
    public void Validate_NonPositiveProtocolRequestTimeout_ReturnsError()
    {
        var options = SampleTestData.ValidOptions();
        options.ProtocolRequestTimeoutSeconds = 0;

        var error = options.Validate();

        Assert.NotNull(error);
        Assert.Contains("ProtocolRequestTimeoutSeconds", error);
    }

    [Fact]
    public void Validate_MissingUri_MessageContainsDotnetUserSecretsHint()
    {
        var options = SampleTestData.ValidOptions(endpointUri: null!);
        var error = options.Validate();
        Assert.NotNull(error);
        Assert.Contains("dotnet user-secrets set", error);
    }

    [Fact]
    public void Validate_MissingUsername_MessageContainsDotnetUserSecretsHint()
    {
        var options = SampleTestData.ValidOptions(username: null!);
        var error = options.Validate();
        Assert.NotNull(error);
        Assert.Contains("dotnet user-secrets set", error);
    }

    [Fact]
    public void Validate_MissingPassword_MessageContainsDotnetUserSecretsHint()
    {
        var options = SampleTestData.ValidOptions(password: null!);
        var error = options.Validate();
        Assert.NotNull(error);
        Assert.Contains("dotnet user-secrets set", error);
    }
}
