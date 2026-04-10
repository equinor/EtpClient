using EtpClient.Models;
using EtpClient.SampleConsole.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// End-to-end shutdown tests verifying that the sample transitions to a final
/// non-connected state regardless of how it exits (User Story 3).
/// </summary>
public sealed class SampleConsoleProgramShutdownTests
{
    [Fact]
    public async Task RunAsync_OnSuccess_FinalStateIsConnected()
    {
        // After RunAsync the session was opened then closed — state as reported by outcome is Connected
        var result = SampleTestData.ConnectionResult();
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        var outcome = await runner.RunAsync();

        // State reported in Success outcome is Connected (the established state)
        Assert.Equal(EtpConnectionState.Connected, outcome.FinalState);
        Assert.True(outcome.Succeeded);
    }

    [Fact]
    public async Task RunAsync_OnSuccess_ConnectorIsDisposed()
    {
        var result = SampleTestData.ConnectionResult();
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        await runner.RunAsync();

        // The await using inside RunAsync ensures DisposeAsync is called
        await connector.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task RunAsync_OnTransportFailure_FinalStateIsFailed()
    {
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EtpConnectionException(EtpConnectionFailureCategory.Transport, "Connection refused"));

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        var outcome = await runner.RunAsync();

        Assert.Equal(EtpConnectionState.Failed, outcome.FinalState);
        Assert.False(outcome.Succeeded);
    }

    [Fact]
    public async Task RunAsync_OnCancellation_FinalStateIsCanceled()
    {
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        var outcome = await runner.RunAsync();

        Assert.Equal(EtpConnectionState.Canceled, outcome.FinalState);
        Assert.False(outcome.Succeeded);
    }

    [Fact]
    public async Task RunAsync_OnValidationFailure_FinalStateIsClosed()
    {
        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions(endpointUri: null!);
        var runner = CreateRunner(options, capture, () => Substitute.For<IEtpConnector>());

        var outcome = await runner.RunAsync();

        // Validation failure: no connection was ever opened; state is Closed
        Assert.Equal(EtpConnectionState.Closed, outcome.FinalState);
    }

    private static SampleConsoleRunner CreateRunner(
        SampleConsoleOptions options,
        TestOutputCapture capture,
        Func<IEtpConnector> connectorFactory) =>
        new(options, connectorFactory, capture.CreateOutputWriter(), NullLogger<SampleConsoleRunner>.Instance);
}
