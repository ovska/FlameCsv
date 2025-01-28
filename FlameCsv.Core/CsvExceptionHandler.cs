using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Callback for custom handling of parsing errors.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <returns><see langword="true"/> if the exception can be ignored.</returns>
[PublicAPI]
public delegate bool CsvExceptionHandler<T>(in CsvExceptionHandlerArgs<T> args) where T : unmanaged, IBinaryInteger<T>;

/// <summary>
/// Arguments for <see cref="CsvExceptionHandler{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
public readonly ref struct CsvExceptionHandlerArgs<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvLine<T> _line;

    internal CsvExceptionHandlerArgs(
        CsvLine<T> line,
        ReadOnlySpan<string> header,
        Exception exception,
        int lineIndex,
        long position)
    {
        _line = line;
        Header = header;
        Line = lineIndex;
        Position = position;
        Exception = exception;
    }

    /// <summary>
    /// Returns the header record, or empty if no headers in CSV.
    /// </summary>
    public ReadOnlySpan<string> Header { get; }

    /// <inheritdoc cref="ICsvRecordFields{T}.FieldCount"/>
    public int FieldCount => _line.FieldCount;

    /// <summary>
    /// Exception thrown.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// The current CSV record (unescaped/untrimmed).
    /// </summary>
    public ReadOnlySpan<T> Record => _line.Record.Span;

    /// <summary>
    /// Options instance.
    /// </summary>
    public CsvOptions<T> Options => _line.Parser.Options;

    /// <summary>
    /// 1-based line number.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// 0-based character position in the data, measured from the start of the unescaped record.
    /// </summary>
    public long Position { get; }

    /// <summary>
    /// Returns the value of a field.
    /// </summary>
    /// <param name="index">0-based field index</param>
    /// <param name="raw">Don't unescape the value</param>
    /// <returns>Value of the field</returns>
    public ReadOnlySpan<T> GetField(int index, bool raw = false) => _line.GetField(index, raw);
}
