using System.Runtime.CompilerServices;
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
    private readonly ref readonly CsvFields<T> _fields;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvExceptionHandlerArgs{T}"/>.
    /// </summary>
    public CsvExceptionHandlerArgs(
        ref readonly CsvFields<T> fields,
        ReadOnlySpan<string> header,
        Exception exception,
        int lineIndex,
        long position)
    {
        if (Unsafe.IsNullRef(in fields)) ThrowHelper.ArgumentNull(nameof(fields));
        ArgumentNullException.ThrowIfNull(exception);

        _fields = ref fields;
        Header = header;
        Line = lineIndex;
        Position = position;
        Exception = exception;
    }

    /// <summary>
    /// Returns the header record, or empty if no headers in CSV.
    /// </summary>
    public ReadOnlySpan<string> Header { get; }

    /// <inheritdoc cref="ICsvFields{T}.FieldCount"/>
    public int FieldCount => _fields.FieldCount;

    /// <summary>
    /// Exception thrown.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// The current CSV record (unescaped/untrimmed).
    /// </summary>
    public ReadOnlySpan<T> Record => _fields.Record.Span;

    /// <summary>
    /// Options instance.
    /// </summary>
    public CsvOptions<T> Options => _fields.Parser.Options;

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
    public ReadOnlySpan<T> GetField(int index, bool raw = false) => _fields.GetField(index, raw);
}

file static class ThrowHelper
{
    public static void ArgumentNull(string paramName) => throw new ArgumentNullException(paramName);
}
