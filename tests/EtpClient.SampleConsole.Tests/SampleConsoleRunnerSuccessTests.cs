using EtpClient.Models;
using EtpClient.SampleConsole.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// Unit tests for <see cref="SampleConsoleRunner"/> success path (User Story 1).
/// </summary>
public sealed class SampleConsoleRunnerSuccessTests
{
    [Fact]
    public async Task RunAsync_WithValidOptions_ReturnsSucceeded()
    {
        // Arrange
        var result = SampleTestData.ConnectionResult();
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions();
        var runner = CreateRunner(options, () => connector, capture);

        // Act
        var outcome = await runner.RunAsync();

        // Assert
        Assert.True(outcome.Succeeded);
        Assert.Equal(EtpConnectionState.Connected, outcome.FinalState);
        Assert.Null(outcome.FailureCategory);
    }

    [Fact]
    public async Task RunAsync_WithValidOptions_PopulatesServerInfo()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var result = SampleTestData.ConnectionResult(
            endpointHost: "localhost",
            appName: "WITSML Server",
            appVersion: "2.0",
            instanceId: instanceId);
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), () => connector, capture);

        // Act
        var outcome = await runner.RunAsync();

        // Assert
        Assert.Equal("WITSML Server", outcome.ServerApplicationName);
        Assert.Equal("2.0", outcome.ServerApplicationVersion);
        Assert.Equal(instanceId, outcome.ServerInstanceId);
        Assert.Equal("localhost", outcome.EndpointHost);
    }

    [Fact]
    public async Task RunAsync_WithValidOptions_WritesSuccessToOutput()
    {
        // Arrange
        var result = SampleTestData.ConnectionResult();
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), () => connector, capture);

        // Act
        await runner.RunAsync();

        // Assert: success written to stdout, nothing to stderr
        Assert.Contains("ETP Session Established", capture.Out);
        Assert.Equal(string.Empty, capture.Error);
    }

    [Fact]
    public async Task RunAsync_WithValidOptions_CallsCloseAsyncAfterConnect()
    {
        // Arrange
        var result = SampleTestData.ConnectionResult();
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), () => connector, capture);

        // Act
        await runner.RunAsync();

        // Assert: CloseAsync was called exactly once
        await connector.Received(1).CloseAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_WithValidOptions_ReturnsExitCodeZero()
    {
        // Arrange
        var result = SampleTestData.ConnectionResult();
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), () => connector, capture);

        // Act
        var outcome = await runner.RunAsync();

        // Assert
        Assert.Equal(SampleExitCode.Success, outcome.ToExitCode());
    }

    [Fact]
    public async Task RunAsync_WithShowSessionDetails_PrintsSessionDetailLines()
    {
        // Arrange
        var instanceId = Guid.NewGuid();
        var result = SampleTestData.ConnectionResult(instanceId: instanceId, appName: "DetailServer", appVersion: "3.0");
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions(showSessionDetails: true);
        var runner = CreateRunner(options, () => connector, capture);

        // Act
        await runner.RunAsync();

        // Assert
        Assert.Contains("DetailServer", capture.Out);
        Assert.Contains("3.0", capture.Out);
        Assert.Contains(instanceId.ToString(), capture.Out);
    }

    private static SampleConsoleRunner CreateRunner(
        SampleConsoleOptions options,
        Func<IEtpConnector> factory,
        TestOutputCapture capture) =>
        new(options, factory, capture.CreateOutputWriter(), NullLogger<SampleConsoleRunner>.Instance);
}
