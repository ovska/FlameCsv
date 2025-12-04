using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Callback for custom handling of parsing errors.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
/// <returns><c>true</c> if the exception can be ignored.</returns>
[PublicAPI]
public delegate bool CsvExceptionHandler<T>(CsvExceptionHandlerArgs<T> args)
    where T : unmanaged, IBinaryInteger<T>;

/// <summary>
/// Arguments for <see cref="CsvExceptionHandler{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
public readonly ref struct CsvExceptionHandlerArgs<T>
    where T : unmanaged, IBinaryInteger<T>
{
    private readonly CsvRecordRef<T> _record;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvExceptionHandlerArgs{T}"/>.
    /// </summary>
    internal CsvExceptionHandlerArgs(
        in CsvRecordRef<T> record,
        ImmutableArray<string> header,
        Exception exception,
        int lineIndex,
        long position,
        int? expectedFieldCount
    )
    {
        _record = record;
        Header = header;
        Line = lineIndex;
        Position = position;
        Exception = exception;
        ExpectedFieldCount = expectedFieldCount;
    }

    /// <summary>
    /// Returns the header record, or empty if no headers in CSV.
    /// </summary>
    public ImmutableArray<string> Header { get; }

    /// <summary>
    /// The current CSV record.
    /// </summary>
    [UnscopedRef]
    public ref readonly CsvRecordRef<T> Record => ref _record;

    /// <summary>
    /// Expected number of fields, if known.
    /// </summary>
    public int? ExpectedFieldCount { get; }

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
    public ReadOnlySpan<T> RawRecord => _record.Raw;

    /// <summary>
    /// Options instance.
    /// </summary>
    public CsvOptions<T> Options => _record._owner.Options;

    /// <summary>
    /// 1-based line number.
    /// </summary>
    public int Line { get; }

    /// <summary>
    /// 0-based character position in the data, measured from the start of the record.
    /// </summary>
    public long Position { get; }
}
