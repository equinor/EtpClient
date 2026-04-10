using EtpClient.Models;
using EtpClient.SampleConsole.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// Tests for the runner's invalid-configuration and connection-failure flows (User Story 2).
/// </summary>
public sealed class SampleConsoleProgramFailureTests
{
    // --- Validation failure path ---

    [Fact]
    public async Task RunAsync_MissingEndpointUri_ReturnsValidationFailure()
    {
        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions(endpointUri: null!);
        var runner = CreateRunner(options, capture);

        var outcome = await runner.RunAsync();

        Assert.False(outcome.Succeeded);
        Assert.Equal(EtpConnectionFailureCategory.Validation, outcome.FailureCategory);
    }

    [Fact]
    public async Task RunAsync_MissingEndpointUri_WritesFailureToStderr()
    {
        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions(endpointUri: null!);
        var runner = CreateRunner(options, capture);

        await runner.RunAsync();

        Assert.NotEqual(string.Empty, capture.Error);
        Assert.Equal(string.Empty, capture.Out);
    }

    [Fact]
    public async Task RunAsync_MissingEndpointUri_ReturnsValidationExitCode()
    {
        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions(endpointUri: null!);
        var runner = CreateRunner(options, capture);

        var outcome = await runner.RunAsync();

        Assert.Equal(SampleExitCode.ValidationFailure, outcome.ToExitCode());
    }

    [Fact]
    public async Task RunAsync_MissingUsername_ReturnsValidationFailure()
    {
        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions(username: null!);
        var runner = CreateRunner(options, capture);

        var outcome = await runner.RunAsync();

        Assert.False(outcome.Succeeded);
        Assert.Equal(EtpConnectionFailureCategory.Validation, outcome.FailureCategory);
    }

    [Fact]
    public async Task RunAsync_MissingPassword_ReturnsValidationFailure()
    {
        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions(password: null!);
        var runner = CreateRunner(options, capture);

        var outcome = await runner.RunAsync();

        Assert.False(outcome.Succeeded);
        Assert.Equal(EtpConnectionFailureCategory.Validation, outcome.FailureCategory);
    }

    [Fact]
    public async Task RunAsync_WithInvalidConfig_DoesNotAttemptConnection()
    {
        var connector = Substitute.For<IEtpConnector>();
        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions(endpointUri: null!);
        var runner = CreateRunner(options, capture, () => connector);

        await runner.RunAsync();

        await connector.DidNotReceive().ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>());
    }

    // --- Connection failure path ---

    [Theory]
    [InlineData(EtpConnectionFailureCategory.Authentication, SampleExitCode.AuthenticationFailure)]
    [InlineData(EtpConnectionFailureCategory.Transport, SampleExitCode.TransportFailure)]
    [InlineData(EtpConnectionFailureCategory.Protocol, SampleExitCode.ProtocolFailure)]
    public async Task RunAsync_WhenConnectThrows_ReturnsMappedExitCode(
        EtpConnectionFailureCategory category, int expectedExitCode)
    {
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EtpConnectionException(category, $"{category} error"));

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        var outcome = await runner.RunAsync();

        Assert.False(outcome.Succeeded);
        Assert.Equal(category, outcome.FailureCategory);
        Assert.Equal(expectedExitCode, outcome.ToExitCode());
    }

    [Fact]
    public async Task RunAsync_WhenConnectThrows_WritesFailureToStderr()
    {
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EtpConnectionException(EtpConnectionFailureCategory.Transport, "Refused"));

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        await runner.RunAsync();

        Assert.Contains("ETP Sample Failed", capture.Error);
        Assert.Equal(string.Empty, capture.Out);
    }

    [Fact]
    public async Task RunAsync_WhenConnectThrows_FailureMessageIsSecretSafe()
    {
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EtpConnectionException(EtpConnectionFailureCategory.Authentication, "401 Unauthorized"));

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(password: "secretpassword"), capture, () => connector);

        await runner.RunAsync();

        Assert.DoesNotContain("secretpassword", capture.Error);
    }

    private static SampleConsoleRunner CreateRunner(
        SampleConsoleOptions options,
        TestOutputCapture capture,
        Func<IEtpConnector>? connectorFactory = null)
    {
        // When no connector factory provided (validation path), we still need a factory
        // but it should never be called for invalid config
        connectorFactory ??= () => Substitute.For<IEtpConnector>();

        return new SampleConsoleRunner(
            options,
            connectorFactory,
            capture.CreateOutputWriter(),
            NullLogger<SampleConsoleRunner>.Instance);
    }
}
