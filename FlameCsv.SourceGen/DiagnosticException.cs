namespace FlameCsv.SourceGen;

internal sealed class DiagnosticException : Exception
{
    public DiagnosticException(string message) : base(message) { }
}
