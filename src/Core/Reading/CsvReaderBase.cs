namespace FlameCsv.Reading;

/// <summary>
/// Base class for types that provide ownership of CSV records.
/// </summary>
public abstract class RecordOwner<T>
    where T : unmanaged, IBinaryInteger<T>
{
    /// <summary>
    /// CSV options associated with this record owner.
    /// </summary>
    public CsvOptions<T> Options { get; }

    private protected RecordOwner(CsvOptions<T> options)
    {
        options.MakeReadOnly();
        Options = options;
        _dialect = new Dialect<T>(options);
    }

    internal readonly Dialect<T> _dialect;

    /// <summary>
    /// Returns a buffer that can be used for unescaping field data.
    /// The buffer is not valid after disposing the reader.
    /// </summary>
    internal abstract Span<T> GetUnescapeBuffer(int length);
}
