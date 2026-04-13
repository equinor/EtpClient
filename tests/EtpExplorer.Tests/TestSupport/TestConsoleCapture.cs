using System.Text;

namespace EtpExplorer.Tests.TestSupport;

/// <summary>
/// Captures lines written to a <see cref="TextWriter"/> during test execution.
/// Useful for asserting what was printed to the console.
/// </summary>
public sealed class TestConsoleCapture : TextWriter
{
    private readonly List<string> _lines = new();
    private readonly StringBuilder _current = new();

    public override Encoding Encoding => Encoding.Unicode;

    /// <inheritdoc/>
    public override void Write(char value)
    {
        if (value == '\n')
        {
            _lines.Add(_current.ToString());
            _current.Clear();
        }
        else if (value != '\r')
        {
            _current.Append(value);
        }
    }

    /// <inheritdoc/>
    public override void Write(string? value)
    {
        if (value is null) return;
        foreach (var c in value)
            Write(c);
    }

    /// <inheritdoc/>
    public override void WriteLine(string? value)
    {
        Write(value);
        Write('\n');
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _current.Length > 0)
            _lines.Add(_current.ToString());
        base.Dispose(disposing);
    }

    /// <summary>Returns all captured lines.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Returns the full captured output as a single string.</summary>
    public string AllOutput => string.Join(Environment.NewLine, _lines);

    /// <summary>Clears captured output.</summary>
    public void Reset()
    {
        _lines.Clear();
        _current.Clear();
    }
}
