namespace EtpClient.SampleConsole;

/// <summary>
/// Writes secret-safe sample run summaries to the console.
/// </summary>
public sealed class SampleOutputWriter
{
    private readonly TextWriter _out;
    private readonly TextWriter _error;

    /// <summary>Creates a writer targeting the standard console streams.</summary>
    public SampleOutputWriter() : this(Console.Out, Console.Error) { }

    /// <summary>Creates a writer targeting the specified output streams (for testing).</summary>
    public SampleOutputWriter(TextWriter @out, TextWriter error)
    {
        _out = @out;
        _error = error;
    }

    /// <summary>Writes a success summary to stdout.</summary>
    public void WriteSuccess(SampleRunOutcome outcome, bool showSessionDetails)
    {
        _out.WriteLine();
        _out.WriteLine("=== ETP Session Established ===");
        _out.WriteLine($"  Endpoint : {outcome.EndpointHost}");
        _out.WriteLine($"  Encoding : {outcome.MessageEncoding}");
        _out.WriteLine($"  State    : {outcome.FinalState}");

        if (showSessionDetails)
        {
            _out.WriteLine($"  Server   : {outcome.ServerApplicationName} {outcome.ServerApplicationVersion}");
            _out.WriteLine($"  Instance : {outcome.ServerInstanceId}");
        }

        _out.WriteLine("================================");
        _out.WriteLine();
    }

    /// <summary>Writes a failure summary to stderr.</summary>
    public void WriteFailure(SampleRunOutcome outcome)
    {
        _error.WriteLine();
        _error.WriteLine("=== ETP Sample Failed ===");
        _error.WriteLine($"  State    : {outcome.FinalState}");
        _error.WriteLine($"  Category : {outcome.FailureCategory}");
        _error.WriteLine($"  Message  : {outcome.FailureMessage}");
        _error.WriteLine("=========================");
        _error.WriteLine();
    }
}
