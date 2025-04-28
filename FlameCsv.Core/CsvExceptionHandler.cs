using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FlameCsv.Extensions;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Callback for custom handling of parsing errors.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <returns><see langword="true"/> if the exception can be ignored.</returns>
[PublicAPI]
public delegate bool CsvExceptionHandler<T>(CsvExceptionHandlerArgs<T> args) where T : unmanaged, IBinaryInteger<T>;

/// <summary>
/// Arguments for <see cref="CsvExceptionHandler{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
public readonly struct CsvExceptionHandlerArgs<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvFields<T> _fields;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvExceptionHandlerArgs{T}"/>.
    /// </summary>
    public CsvExceptionHandlerArgs(
        in CsvFields<T> fields,
        ImmutableArray<string> header,
        Exception exception,
        int lineIndex,
        long position)
    {
        if (Unsafe.IsNullRef(in fields)) Throw.ArgumentNull(nameof(fields));
        Throw.IfDefaultStruct(header.IsDefault, typeof(ImmutableArray<string>));
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lineIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(position);

        _fields = fields;
        Header = header;
        Line = lineIndex;
        Position = position;
        Exception = exception;
    }

    /// <summary>
    /// Returns the <see cref="CsvFields{T}"/> instance.
    /// </summary>
    /// <remarks>
    /// The instance is only valid until the handler returns, and should not be stored for further use.
    /// </remarks>
    public readonly CsvFields<T> Fields => _fields;

    /// <summary>
    /// Returns the header record, or empty if no headers in CSV.
    /// </summary>
    public ImmutableArray<string> Header { get; }

    /// <inheritdoc cref="ICsvFields{T}.FieldCount"/>
    public int FieldCount => _fields.FieldCount;

    /// <summary>
    /// Exception thrown.
    /// </summary>
    /// <remarks>
    /// <see cref="Exceptions.CsvFormatException"/> is unrecoverable, and not handled by the callback.
    /// </remarks>
    public Exception Exception { get; }

    /// <summary>
    /// The current CSV record (unescaped/untrimmed).
    /// </summary>
    public ReadOnlySpan<T> Record => _fields.Record.Span;

    /// <summary>
    /// Options instance.
    /// </summary>
    public CsvOptions<T> Options => _fields.Reader.Options;

    /// <summary>
    /// 1-based line number.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// 0-based character position in the data, measured from the start of the record.
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
