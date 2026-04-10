using EtpClient.Models;
using EtpClient.SampleConsole.Tests.TestSupport;
using Xunit;

namespace EtpClient.SampleConsole.Tests;

/// <summary>
/// Tests for encoding option binding and usage in the sample console app.
/// T028 [US3]: Validates that the encoding selection flows from options to connection.
/// </summary>
public sealed class SampleConsoleEncodingOptionTests
{
    // ── Default encoding ──────────────────────────────────────────────────────

    [Fact]
    public void MessageEncoding_Default_IsBinary()
    {
        var options = SampleTestData.ValidOptions();
        Assert.Equal(EtpMessageEncoding.Binary, options.MessageEncoding);
    }

    // ── Explicit encoding selection ───────────────────────────────────────────

    [Fact]
    public void MessageEncoding_SetToJson_IsPreserved()
    {
        var options = SampleTestData.ValidOptions();
        options.MessageEncoding = EtpMessageEncoding.Json;
        Assert.Equal(EtpMessageEncoding.Json, options.MessageEncoding);
    }

    [Fact]
    public void MessageEncoding_SetToBinary_IsPreserved()
    {
        var options = SampleTestData.ValidOptions();
        options.MessageEncoding = EtpMessageEncoding.Binary;
        Assert.Equal(EtpMessageEncoding.Binary, options.MessageEncoding);
    }

    // ── ToConnectionOptions forwards encoding ─────────────────────────────────

    [Fact]
    public void ToConnectionOptions_BinaryEncoding_ConnectionOptionsHasBinary()
    {
        var options = SampleTestData.ValidOptions();
        options.MessageEncoding = EtpMessageEncoding.Binary;

        var connectionOptions = options.ToConnectionOptions();

        Assert.Equal(EtpMessageEncoding.Binary, connectionOptions.MessageEncoding);
    }

    [Fact]
    public void ToConnectionOptions_JsonEncoding_ConnectionOptionsHasJson()
    {
        var options = SampleTestData.ValidOptions();
        options.MessageEncoding = EtpMessageEncoding.Json;

        var connectionOptions = options.ToConnectionOptions();

        Assert.Equal(EtpMessageEncoding.Json, connectionOptions.MessageEncoding);
    }

    // ── SampleRunOutcome reports encoding ─────────────────────────────────────

    [Fact]
    public void SampleRunOutcome_FromSuccess_ReportsEncoding()
    {
        var result = SampleTestData.ConnectionResult();

        var outcome = SampleRunOutcome.FromSuccess(result);

        Assert.Equal(EtpMessageEncoding.Binary, outcome.MessageEncoding);
    }

    // ── SampleOutputWriter includes encoding in success output ────────────────

    [Fact]
    public void WriteSuccess_IncludesEncodingLine()
    {
        using var output = new System.IO.StringWriter();
        using var error = new System.IO.StringWriter();
        var writer = new SampleOutputWriter(output, error);
        var outcome = SampleRunOutcome.FromSuccess(SampleTestData.ConnectionResult());

        writer.WriteSuccess(outcome, showSessionDetails: false);

        var text = output.ToString();
        Assert.Contains("Encoding", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Binary", text, StringComparison.OrdinalIgnoreCase);
    }
}
