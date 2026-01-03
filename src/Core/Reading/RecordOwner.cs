using FlameCsv.Reading.Internal;

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

    private protected RecordOwner(CsvOptions<T> options, RecordBuffer recordBuffer)
    {
        options.MakeReadOnly();
        Options = options;
        _dialect = new Dialect<T>(options);
        _recordBuffer = recordBuffer;
    }

    internal readonly Dialect<T> _dialect;
    internal readonly RecordBuffer _recordBuffer;

    /// <summary>
    /// Indicates whether the reader has been disposed.
    /// </summary>
    public abstract bool IsDisposed { get; }

    /// <summary>
    /// Returns a buffer that can be used for unescaping field data.
    /// The buffer is not valid after disposing the reader.
    /// </summary>
    internal abstract Span<T> GetUnescapeBuffer(int length);
}
