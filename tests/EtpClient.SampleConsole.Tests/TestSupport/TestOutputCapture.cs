namespace EtpClient.SampleConsole.Tests.TestSupport;

/// <summary>
/// Captures output written to stdout/stderr for assertion in tests.
/// </summary>
internal sealed class TestOutputCapture
{
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public string Out => _out.ToString();
    public string Error => _error.ToString();

    /// <summary>Creates a <see cref="SampleOutputWriter"/> that writes into this capture.</summary>
    public SampleOutputWriter CreateOutputWriter() => new(_out, _error);
}
