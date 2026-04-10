using EtpClient.Diagnostics;
using EtpClient.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace EtpClient.UnitTests.Diagnostics;

/// <summary>
/// Unit tests for encoding-aware diagnostics in <see cref="EtpClientLog"/>.
/// T021 [US2]: Validates that encoding log messages are secret-safe and include encoding info.
/// </summary>
public sealed class EtpClientLogEncodingTests
{
    [Fact]
    public void EncodingSelected_LogsAtDebugLevel()
    {
        var logger = new FakeLogger();
        EtpClientLog.EncodingSelected(logger, "example.com", EtpMessageEncoding.Binary);

        Assert.Single(logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Debug, logger.Collector.GetSnapshot()[0].Level);
    }

    [Fact]
    public void EncodingSelected_Binary_LogMessageContainsBinary()
    {
        var logger = new FakeLogger();
        EtpClientLog.EncodingSelected(logger, "example.com", EtpMessageEncoding.Binary);

        var msg = logger.Collector.GetSnapshot()[0].Message;
        Assert.Contains("Binary", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EncodingSelected_Json_LogMessageContainsJson()
    {
        var logger = new FakeLogger();
        EtpClientLog.EncodingSelected(logger, "example.com", EtpMessageEncoding.Json);

        var msg = logger.Collector.GetSnapshot()[0].Message;
        Assert.Contains("Json", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EncodingSelected_DoesNotContainCredentials()
    {
        var logger = new FakeLogger();
        // An accidental credential in the endpoint host — the log must not propagate it
        EtpClientLog.EncodingSelected(logger, "example.com", EtpMessageEncoding.Binary);

        var msg = logger.Collector.GetSnapshot()[0].Message;
        // No password or username should appear — endpoint host only
        Assert.DoesNotContain("password", msg, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", msg, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EncodingSelected_IncludesEndpointHost()
    {
        var logger = new FakeLogger();
        EtpClientLog.EncodingSelected(logger, "my-server.example.com", EtpMessageEncoding.Json);

        var msg = logger.Collector.GetSnapshot()[0].Message;
        Assert.Contains("my-server.example.com", msg);
    }
}
