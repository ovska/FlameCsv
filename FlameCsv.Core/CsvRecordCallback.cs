using FlameCsv.Reading;
using JetBrains.Annotations;

namespace FlameCsv;

/// <summary>
/// Arguments for <see cref="CsvRecordCallback{T}"/>.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
public readonly ref struct CsvRecordCallbackArgs<T> where T : unmanaged, IBinaryInteger<T>
{
    private readonly ref readonly CsvLine<T> _line;
    private readonly ref bool _skip;
    private readonly ref bool _headerRead;

    internal CsvRecordCallbackArgs(
        ref readonly CsvLine<T> line,
        ReadOnlySpan<string> header,
        int lineIndex,
        long position,
        ref bool skip,
        ref bool headerRead)
    {
        _line = ref line;
        Line = lineIndex;
        Position = position;
        Header = header;
        _skip = ref skip;
        _headerRead = ref headerRead;
    }

    /// <summary>
    /// Returns true if the record has a length of 0.
    /// </summary>
    /// <remarks>
    /// An empty record's <see cref="FieldCount"/> is not zero,
    /// is considered to have exactly one field with length of 0.
    /// </remarks>
    public bool IsEmpty => _line.RecordLength == 0;

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
    public int Line { get; init; }

    /// <summary>
    /// 0-based character position in the data, measured from the start of the unescaped record.
    /// </summary>
    public long Position { get; init; }

    /// <summary>
    /// Set to true to skip this record.
    /// </summary>
    public bool SkipRecord
    {
        get => _skip;
        set => _skip = value;
    }

    /// <summary>
    /// Whether the header has been read.
    /// If this is set to false when the callback returns, the header is re-read.
    /// </summary>
    /// <remarks>
    /// If the set to false while reading headered CSV, this record will be considered the header unless
    /// <see cref="SkipRecord"/> is also true, in which case it will be first unskipped record.
    /// </remarks>
    public bool HeaderRead
    {
        get => _headerRead;
        set => _headerRead = value;
    }

    /// <summary>
    /// Returns the header record if <see cref="HeaderRead"/> is true, empty otherwise.
    /// </summary>
    public ReadOnlySpan<string> Header { get; }

    /// <summary>
    /// Number of fields in the record.
    /// </summary>
    public int FieldCount => _line.FieldCount;

    /// <summary>
    /// Returns the value of a field.
    /// </summary>
    /// <param name="index">0-based field index</param>
    /// <param name="raw">Don't unescape the value</param>
    /// <returns>Value of the field</returns>
    public ReadOnlySpan<T> GetField(int index, bool raw = false) => _line.GetField(index, raw);
}

/// <summary>
/// Callback called for every record (line) before it is returned or parsed into an object/struct.
/// </summary>
/// <typeparam name="T">Token type</typeparam>
[PublicAPI]
public delegate void CsvRecordCallback<T>(ref readonly CsvRecordCallbackArgs<T> args)
    where T : unmanaged, IBinaryInteger<T>;
