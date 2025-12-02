using System.Collections.Immutable;
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
    private readonly CsvSlice<T> _slice;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvExceptionHandlerArgs{T}"/>.
    /// </summary>
    internal CsvExceptionHandlerArgs(
        in CsvSlice<T> slice,
        ImmutableArray<string> header,
        Exception exception,
        int lineIndex,
        long position,
        int? expectedFieldCount
    )
    {
        _slice = slice;
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

    /// <inheritdoc cref="ICsvRecord{T}.FieldCount"/>
    public int FieldCount => _slice.FieldCount;

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
    public ReadOnlySpan<T> RawRecord => _slice.RawValue;

    /// <summary>
    /// Options instance.
    /// </summary>
    public CsvOptions<T> Options => _slice.Reader.Options;

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
    public ReadOnlySpan<T> GetField(int index, bool raw = false)
    {
        CsvRecordRef<T> recordRef = new(in _slice);
        return raw ? recordRef.GetRawSpan(index) : recordRef[index];
    }
}
