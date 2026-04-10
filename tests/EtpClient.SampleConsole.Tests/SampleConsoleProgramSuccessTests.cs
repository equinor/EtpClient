using EtpClient.Models;
using EtpClient.SampleConsole.Tests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// Integration-style tests for the sample application host wiring (User Story 1 success path).
/// Verifies that the DI container correctly resolves the runner and that configuration binds as expected.
/// </summary>
public sealed class SampleConsoleProgramSuccessTests
{
    [Fact]
    public void SampleConsoleOptions_BindsFromConfiguration()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Etp:EndpointUri"] = "wss://host.example.com/etp",
                ["Etp:Username"] = "user1",
                ["Etp:Password"] = "secret",
                ["Etp:ShowSessionDetails"] = "true",
            })
            .Build();

        var options = new SampleConsoleOptions();
        config.GetSection("Etp").Bind(options);

        // Assert
        Assert.Equal("wss://host.example.com/etp", options.EndpointUri);
        Assert.Equal("user1", options.Username);
        Assert.Equal("secret", options.Password);
        Assert.True(options.ShowSessionDetails);
    }

    [Fact]
    public void SampleConsoleOptions_Validate_NullOnValidConfig()
    {
        // Arrange
        var options = SampleTestData.ValidOptions();

        // Act
        var error = options.Validate();

        // Assert
        Assert.Null(error);
    }

    [Fact]
    public async Task Runner_WithFakeConnector_ReturnsSuccessOutcome()
    {
        // Arrange
        var connectionResult = SampleTestData.ConnectionResult();
        var connector = Substitute.For<IEtpConnector>();
        connector.ConnectAsync(Arg.Any<EtpConnectionOptions>(), Arg.Any<CancellationToken>())
            .Returns(connectionResult);

        var capture = new TestOutputCapture();
        var options = SampleTestData.ValidOptions();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(options);
        services.AddSingleton(capture.CreateOutputWriter());
        services.AddTransient<SampleConsoleRunner>(sp =>
            new SampleConsoleRunner(
                sp.GetRequiredService<SampleConsoleOptions>(),
                () => connector,
                sp.GetRequiredService<SampleOutputWriter>(),
                sp.GetRequiredService<ILogger<SampleConsoleRunner>>()));

        await using var provider = services.BuildServiceProvider();
        var runner = provider.GetRequiredService<SampleConsoleRunner>();

        // Act
        var outcome = await runner.RunAsync();

        // Assert
        Assert.True(outcome.Succeeded);
        Assert.Contains("ETP Session Established", capture.Out);
    }
}
