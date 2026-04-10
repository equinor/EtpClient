using EtpClient.Models;
using EtpClient.SampleConsole.Tests.TestSupport;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// Tests for <see cref="SampleOutputWriter"/> failure rendering and secret-safety (User Story 2).
/// </summary>
public sealed class SampleOutputWriterFailureTests
{
    [Fact]
    public void WriteFailure_WritesToStderr_NotStdout()
    {
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();
        var outcome = SampleRunOutcome.FromValidationError("Config is missing", "host.example");

        writer.WriteFailure(outcome);

        Assert.NotEqual(string.Empty, capture.Error);
        Assert.Equal(string.Empty, capture.Out);
    }

    [Fact]
    public void WriteFailure_ContainsFailureCategoryLabel()
    {
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();
        var ex = new EtpConnectionException(EtpConnectionFailureCategory.Authentication, "Unauthorized");
        var outcome = SampleRunOutcome.FromException(ex, "host.example");

        writer.WriteFailure(outcome);

        Assert.Contains("Authentication", capture.Error);
    }

    [Fact]
    public void WriteFailure_ContainsStateLabel()
    {
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();
        var ex = new EtpConnectionException(EtpConnectionFailureCategory.Transport, "Connection refused");
        var outcome = SampleRunOutcome.FromException(ex, "host.example");

        writer.WriteFailure(outcome);

        Assert.Contains("Failed", capture.Error);
    }

    [Fact]
    public void WriteFailure_DoesNotContainCredentials()
    {
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();
        var ex = new EtpConnectionException(EtpConnectionFailureCategory.Authentication, "Invalid credentials");
        var outcome = SampleRunOutcome.FromException(ex, "host.example");

        writer.WriteFailure(outcome);

        // Passwords and base64-encoded auth headers should never appear
        Assert.DoesNotContain("testpass", capture.Error);
        Assert.DoesNotContain("Authorization:", capture.Error);
        Assert.DoesNotContain("Basic ", capture.Error);
    }

    [Theory]
    [InlineData(EtpConnectionFailureCategory.Validation)]
    [InlineData(EtpConnectionFailureCategory.Authentication)]
    [InlineData(EtpConnectionFailureCategory.Transport)]
    [InlineData(EtpConnectionFailureCategory.Protocol)]
    [InlineData(EtpConnectionFailureCategory.Cancellation)]
    public void WriteFailure_EachCategoryRendersDistinctly(EtpConnectionFailureCategory category)
    {
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();
        var ex = new EtpConnectionException(category, $"{category} error");
        var outcome = SampleRunOutcome.FromException(ex, "host");

        writer.WriteFailure(outcome);

        Assert.Contains(category.ToString(), capture.Error);
    }

    [Fact]
    public void WriteSuccess_DoesNotContainCredentials()
    {
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();
        var result = SampleTestData.ConnectionResult(endpointHost: "host.example");
        var outcome = SampleRunOutcome.FromSuccess(result);

        writer.WriteSuccess(outcome, showSessionDetails: true);

        Assert.DoesNotContain("testpass", capture.Out);
        Assert.DoesNotContain("Authorization:", capture.Out);
        Assert.DoesNotContain("Basic ", capture.Out);
    }

    [Fact]
    public void WriteSuccess_WithNoNegotiatedProtocols_ShowsNoneReported()
    {
        var capture = new TestOutputCapture();
        var writer = capture.CreateOutputWriter();
        var outcome = SampleRunOutcome.FromSuccess(SampleTestData.ConnectionResult());

        writer.WriteSuccess(outcome, showSessionDetails: true);

        Assert.Contains("Protocols: (none reported)", capture.Out);
    }
}
