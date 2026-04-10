using EtpClient.Models;
using EtpClient.SampleConsole.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// Tests for cancellation handling and clean shutdown in <see cref="SampleConsoleRunner"/> (User Story 3).
/// </summary>
public sealed class SampleConsoleRunnerShutdownTests
{
    [Fact]
    public async Task RunAsync_WhenCanceled_ReturnsCancellationOutcome()
    {
        // Arrange
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync<OperationCanceledException>();

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        // Act
        var outcome = await runner.RunAsync();

        // Assert
        Assert.False(outcome.Succeeded);
        Assert.Equal(EtpConnectionFailureCategory.Cancellation, outcome.FailureCategory);
        Assert.Equal(SampleExitCode.CancellationFailure, outcome.ToExitCode());
    }

    [Fact]
    public async Task RunAsync_WhenCanceled_WritesFailureOutput()
    {
        // Arrange
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync<OperationCanceledException>();

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        // Act
        await runner.RunAsync();

        // Assert: failure written to stderr
        Assert.Contains("Cancellation", capture.Error);
        Assert.Equal(string.Empty, capture.Out);
    }

    [Fact]
    public async Task RunAsync_WhenCanceled_FinalStateIsCanceled()
    {
        // Arrange
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync<OperationCanceledException>();

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        // Act
        var outcome = await runner.RunAsync();

        // Assert
        Assert.Equal(EtpConnectionState.Canceled, outcome.FinalState);
    }

    [Fact]
    public async Task RunAsync_ConnectionSuccess_DisposesConnector()
    {
        // Arrange
        var result = SampleTestData.ConnectionResult();
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(result);

        var connectorCreated = connector;
        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connectorCreated);

        // Act
        await runner.RunAsync();

        // Assert: DisposeAsync called due to await using
        await connector.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task RunAsync_ConnectionFailure_DisposesConnector()
    {
        // Arrange
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new EtpConnectionException(EtpConnectionFailureCategory.Transport, "Refused"));

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        // Act
        await runner.RunAsync();

        // Assert: DisposeAsync still called after exception
        await connector.Received(1).DisposeAsync();
    }

    [Fact]
    public async Task RunAsync_WhenCanceled_DoesNotCallCloseAsync()
    {
        // When connection is canceled, CloseAsync should not be called since
        // the connection never completed successfully.
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync<OperationCanceledException>();

        var capture = new TestOutputCapture();
        var runner = CreateRunner(SampleTestData.ValidOptions(), capture, () => connector);

        // Act
        await runner.RunAsync();

        // Assert: CloseAsync not called — connect never succeeded
        await connector.DidNotReceive().CloseAsync(Arg.Any<CancellationToken>());
    }

    private static SampleConsoleRunner CreateRunner(
        SampleConsoleOptions options,
        TestOutputCapture capture,
        Func<IEtpConnector> connectorFactory) =>
        new(options, connectorFactory, capture.CreateOutputWriter(), NullLogger<SampleConsoleRunner>.Instance);
}
